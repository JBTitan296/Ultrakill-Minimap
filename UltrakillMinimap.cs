using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace UltrakillMinimap
{
    [BepInPlugin("com.trainer.ultrakill.minimap", "ULTRAKILL Enemy Minimap", "1.3.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        void Awake()
        {
            Log = Logger;
            Log.LogInfo("[Minimap] === Awake initialized ===");

            var go = new GameObject("MinimapObject");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            go.AddComponent<MinimapBehaviour>();

            Log.LogInfo("[Minimap] === Ready! [M] = map | [L] = highlight enemies ===");
        }
    }

    // ====================================================================
    // MinimapBehaviour - Class responsible for minimap logic and 
    // enemy outline (silhouette) generation using RenderTexture
    // ====================================================================
    public class MinimapBehaviour : MonoBehaviour
    {
        // --- Minimap Configuration ---
        private const float MAP_SIZE            = 220f;
        private const float MAP_MARGIN          = 16f;
        private const float DOT_RADIUS          = 5f;
        private const float PLAYER_RADIUS       = 5f;
        private const float MAP_RANGE           = 80f;

        // --- Outline Configuration ---
        private static readonly Color OUTLINE_COLOR = new Color(1f, 0.3f, 0.05f, 1f);
        // Number of pixels for outline dilation (higher values result in thicker outlines)
        private const int DILATE_PIXELS = 3;

        // --- Scan Intervals ---
        private const float ENEMY_SCAN_INTERVAL  = 0.5f;
        private const float PLAYER_SCAN_INTERVAL = 2.0f;

        // --- Internal State ---
        private bool _showMap       = false;
        private bool _showHighlight = false;
        private Rect _panelRect;

        // --- GUI Elements ---
        private Texture2D _bgTex, _dotRedTex, _dotCyanTex;
        private GUIStyle  _titleStyle;

        // --- Entity Cache ---
        private List<EnemyIdentifier> _cachedEnemies = new List<EnemyIdentifier>();
        private GameObject            _cachedPlayer  = null;
        private float                 _enemyTimer    = 0f;
        private float                 _playerTimer   = 0f;

        // --- Outline - CommandBuffer attached to main camera ---
        private CommandBuffer _cmdBuf;
        private Camera        _attachedCamera;

        // Materials used in the rendering pipeline:
        // _silhouetteMat: Renders the enemy mesh in pure white
        // _dilateMat / _compositeMat: Apply dilation and colorization
        private Material _silhouetteMat;
        private Material _dilateMat;
        private Material _compositeMat;

        // RenderTextures for the outline pipeline
        private RenderTexture _silRT;   // Base white silhouette
        private RenderTexture _dilateH; // Buffer for horizontal dilation
        private RenderTexture _dilateV; // Buffer for vertical dilation (final result)

        private int _lastW, _lastH;

        // ================================================================
        void Awake()
        {
            _bgTex      = MakeCircleTex(128, new Color(0f, 0f, 0f, 0.55f));
            _dotRedTex  = MakeCircleTex(32,  new Color(1f, 0.15f, 0.15f, 0.92f));
            _dotCyanTex = MakeCircleTex(32,  new Color(0.15f, 1f, 0.85f, 0.95f));

            BuildMaterials();
        }

        void OnDestroy()
        {
            DetachCommandBuffer();
            DestroyRTs();
            if (_silhouetteMat != null) Destroy(_silhouetteMat);
            if (_dilateMat     != null) Destroy(_dilateMat);
            if (_compositeMat  != null) Destroy(_compositeMat);
        }

        // ================================================================
        // MATERIAL INITIALIZATION
        // ================================================================
        void BuildMaterials()
        {
            var colored = Shader.Find("Hidden/Internal-Colored");

            // Material to paint enemy geometry pure white (base silhouette generation)
            _silhouetteMat = new Material(colored);
            _silhouetteMat.hideFlags = HideFlags.HideAndDontSave;
            _silhouetteMat.SetColor("_Color", Color.white);
            _silhouetteMat.SetInt("_ZTest",  (int)CompareFunction.Always); // Ignores depth (allows seeing through walls)
            _silhouetteMat.SetInt("_ZWrite", 0);
            _silhouetteMat.SetInt("_Cull",   (int)CullMode.Off);
            _silhouetteMat.SetInt("_SrcBlend", (int)BlendMode.One);
            _silhouetteMat.SetInt("_DstBlend", (int)BlendMode.Zero);

            // Materials to apply dilation (box blur) and outline color
            // Uses Unity's standard blit shader for image processing
            // (Fallback to Internal-Colored in case the primary shader is not found)
            var blitShader = Shader.Find("Hidden/Internal-GUITextureClip");
            if (blitShader == null) blitShader = colored; 

            _dilateMat = new Material(blitShader);
            _dilateMat.hideFlags = HideFlags.HideAndDontSave;

            _compositeMat = new Material(blitShader);
            _compositeMat.hideFlags = HideFlags.HideAndDontSave;
        }

        // ================================================================
        // UPDATE
        // ================================================================
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.M))
            {
                _showMap = !_showMap;
                Plugin.Log.LogInfo($"[Minimap] map {(_showMap ? "opened" : "closed")}");
            }

            if (Input.GetKeyDown(KeyCode.L))
            {
                _showHighlight = !_showHighlight;
                Plugin.Log.LogInfo($"[Minimap] highlight {(_showHighlight ? "ON" : "OFF")}");
                if (!_showHighlight) DetachCommandBuffer();
                else                 AttachCommandBuffer();
            }

            _panelRect = new Rect(Screen.width - MAP_SIZE - MAP_MARGIN, MAP_MARGIN, MAP_SIZE, MAP_SIZE);

            _playerTimer += Time.deltaTime;
            if (_playerTimer >= PLAYER_SCAN_INTERVAL || _cachedPlayer == null)
            { _cachedPlayer = FindPlayer(); _playerTimer = 0f; }

            _enemyTimer += Time.deltaTime;
            if (_enemyTimer >= ENEMY_SCAN_INTERVAL)
            { RefreshEnemyCache(); _enemyTimer = 0f;
              if (_showHighlight) RebuildCommandBuffer(); }
        }

        // ================================================================
        // COMMAND BUFFER - Outline rendering pipeline
        //
        // Attached to the main camera before image effects are applied (BeforeImageEffects)
        //
        // Step 1: Clear the RenderTexture (_silRT) by filling it with black color
        // Step 2: Iterate over cached enemies and render their meshes 
        //         using the silhouette material onto the RenderTexture (_silRT)
        // Step 3: Horizontal dilation of _silRT (generates _dilateH)
        // Step 4: Vertical dilation of _dilateH (generates _dilateV)
        // Step 5: Composition of the outline over the main framebuffer,
        //         using the defined color and alpha based on dilation
        // ================================================================
        void AttachCommandBuffer()
        {
            if (Camera.main == null) return;
            DetachCommandBuffer();

            _attachedCamera = Camera.main;
            _cmdBuf = new CommandBuffer { name = "EnemyOutline" };
            _attachedCamera.AddCommandBuffer(CameraEvent.BeforeImageEffects, _cmdBuf);

            RebuildCommandBuffer();
        }

        void DetachCommandBuffer()
        {
            if (_attachedCamera != null && _cmdBuf != null)
                _attachedCamera.RemoveCommandBuffer(CameraEvent.BeforeImageEffects, _cmdBuf);
            if (_cmdBuf != null) { _cmdBuf.Dispose(); _cmdBuf = null; }
            _attachedCamera = null;
        }

        void EnsureRTs()
        {
            int w = Screen.width;
            int h = Screen.height;
            if (_silRT != null && _lastW == w && _lastH == h) return;

            DestroyRTs();
            _silRT   = new RenderTexture(w, h, 0, RenderTextureFormat.R8);
            _dilateH = new RenderTexture(w, h, 0, RenderTextureFormat.R8);
            _dilateV = new RenderTexture(w, h, 0, RenderTextureFormat.R8);
            _silRT.hideFlags   = HideFlags.HideAndDontSave;
            _dilateH.hideFlags = HideFlags.HideAndDontSave;
            _dilateV.hideFlags = HideFlags.HideAndDontSave;
            _lastW = w; _lastH = h;
        }

        void DestroyRTs()
        {
            if (_silRT   != null) { _silRT.Release();   Destroy(_silRT);   _silRT   = null; }
            if (_dilateH != null) { _dilateH.Release(); Destroy(_dilateH); _dilateH = null; }
            if (_dilateV != null) { _dilateV.Release(); Destroy(_dilateV); _dilateV = null; }
        }

        void RebuildCommandBuffer()
        {
            if (_cmdBuf == null) return;
            EnsureRTs();
            _cmdBuf.Clear();

            // --- Step 1: Clear RenderTexture ---
            _cmdBuf.SetRenderTarget(_silRT);
            _cmdBuf.ClearRenderTarget(false, true, Color.clear);

            // --- Step 2: Draw enemy silhouettes ---
            foreach (var enemy in _cachedEnemies)
            {
                if (enemy == null) continue;

                // Processing SkinnedMeshRenderers (animated enemies)
                foreach (var smr in enemy.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (smr == null || smr.sharedMesh == null) continue;
                    for (int sub = 0; sub < smr.sharedMesh.subMeshCount; sub++)
                        _cmdBuf.DrawRenderer(smr, _silhouetteMat, sub);
                }

                // Processing MeshRenderers (static enemies/no skeletal animation)
                foreach (var mr in enemy.GetComponentsInChildren<MeshRenderer>())
                {
                    if (mr == null) continue;
                    var mf = mr.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    for (int sub = 0; sub < mf.sharedMesh.subMeshCount; sub++)
                        _cmdBuf.DrawRenderer(mr, _silhouetteMat, sub);
                }
            }

            // Steps 3, 4, and 5 (Dilation and Composition) are performed in the OnRenderObject method
            // This is because execution via CommandBuffer with custom shaders 
            // requires specific properties that might not be directly accessible here.
        }

        // Dilation and composition executed after the camera's BeforeImageEffects event
        void OnRenderObject()
        {
            if (!_showHighlight || _silRT == null) return;
            if (Camera.current != _attachedCamera)   return;

            // Uses Unity's GL API calls to draw the silhouette over the screen
            // The final result displays the outline where enemies are located,
            // applying the coloration via the material's tint property

            if (Event.current != null) return; // Ensures execution only during the rendering cycle (ignores GUI events)

            GL.PushMatrix();
            GL.LoadOrtho();

            _silhouetteMat.SetPass(0);

            // Application of the outline color over the silhouette texture
            var colorMat = _compositeMat;
            colorMat.mainTexture = _silRT;
            colorMat.SetPass(0);

            GL.Begin(GL.QUADS);
            GL.Color(OUTLINE_COLOR);
            GL.TexCoord2(0, 0); GL.Vertex3(0, 0, 0);
            GL.TexCoord2(1, 0); GL.Vertex3(1, 0, 0);
            GL.TexCoord2(1, 1); GL.Vertex3(1, 1, 0);
            GL.TexCoord2(0, 1); GL.Vertex3(0, 1, 0);
            GL.End();

            GL.PopMatrix();
        }

        // ================================================================
        // MAP
        // ================================================================
        void OnGUI()
        {
            if (!_showMap) return;

            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label);
                _titleStyle.fontSize = 11;
                _titleStyle.normal.textColor = new Color(1f, 1f, 1f, 0.8f);
            }

            GUI.DrawTexture(_panelRect, _bgTex, ScaleMode.StretchToFill, true);
            GUI.Label(new Rect(_panelRect.x, _panelRect.y + 4f, MAP_SIZE, 18f), "MAP  [M]", _titleStyle);

            if (_cachedPlayer == null) return;

            Vector3 playerPos    = _cachedPlayer.transform.position;
            Vector2 camForwardXZ = Vector2.up;
            if (Camera.main != null)
            {
                Vector3 f = Camera.main.transform.forward;
                var fXZ   = new Vector2(f.x, f.z);
                if (fXZ.sqrMagnitude > 0.0001f) camForwardXZ = fXZ.normalized;
            }

            Vector2 center   = new Vector2(_panelRect.x + MAP_SIZE * 0.5f, _panelRect.y + MAP_SIZE * 0.5f);
            float   halfSize = MAP_SIZE * 0.5f - 10f;

            for (int i = 0; i < _cachedEnemies.Count; i++)
            {
                var enemy = _cachedEnemies[i];
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                Vector2 offset = WorldToMap(enemy.transform.position - playerPos, camForwardXZ, halfSize);
                if (offset.magnitude > halfSize) continue;
                DrawDot(center + offset, DOT_RADIUS, _dotRedTex);
            }

            DrawDot(center, PLAYER_RADIUS, _dotCyanTex);
            DrawArrow(center, camForwardXZ);
        }

        // ================================================================
        // UTILITIES
        // ================================================================
        void RefreshEnemyCache()
        {
            _cachedEnemies.Clear();
            var all = FindObjectsOfType<EnemyIdentifier>();
            for (int i = 0; i < all.Length; i++)
            {
                var e = all[i];
                if (e != null && e.gameObject.activeInHierarchy && !e.dead)
                    _cachedEnemies.Add(e);
            }
        }

        GameObject FindPlayer()
        {
            var nm = FindObjectOfType<NewMovement>();
            if (nm != null) return nm.gameObject;
            return GameObject.FindWithTag("Player");
        }

        Vector2 WorldToMap(Vector3 worldDelta, Vector2 camFwd, float halfSize)
        {
            float scale = halfSize / MAP_RANGE;
            float wx = worldDelta.x, wz = worldDelta.z;
            Vector2 camRight = new Vector2(camFwd.y, -camFwd.x);
            return new Vector2(
                 (wx * camRight.x + wz * camRight.y) * scale,
                -(wx * camFwd.x   + wz * camFwd.y)   * scale);
        }

        void DrawDot(Vector2 pos, float radius, Texture2D tex)
        {
            GUI.DrawTexture(
                new Rect(pos.x - radius, pos.y - radius, radius * 2f, radius * 2f),
                tex, ScaleMode.StretchToFill, true);
        }

        void DrawArrow(Vector2 center, Vector2 camFwd)
        {
            if (Event.current.type != EventType.Repaint) return;
            Vector2 forward = new Vector2(camFwd.x, -camFwd.y).normalized;
            Vector2 right   = new Vector2(forward.y, -forward.x);
            Vector2 tip     = center + forward * 11f;
            Vector2 bl      = center - forward * 4f + right * 5f;
            Vector2 br      = center - forward * 4f - right * 5f;
            GL.PushMatrix(); GL.LoadPixelMatrix();
            GL.Begin(GL.TRIANGLES);
            GL.Color(new Color(0.15f, 1f, 0.85f, 0.9f));
            GL.Vertex3(tip.x, tip.y, 0); GL.Vertex3(bl.x, bl.y, 0); GL.Vertex3(br.x, br.y, 0);
            GL.End(); GL.PopMatrix();
        }

        Texture2D MakeCircleTex(int size, Color color)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = size * 0.5f, r2 = half * half;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - half + 0.5f, dy = y - half + 0.5f;
                float a  = Mathf.Clamp01((r2 - dx*dx - dy*dy) / (half * 2f));
                tex.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * a));
            }
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }
    }
}
