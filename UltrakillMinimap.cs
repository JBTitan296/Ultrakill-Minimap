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
            Log.LogInfo("[Minimap] === Awake iniciado ===");

            var go = new GameObject("MinimapObject");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            go.AddComponent<MinimapBehaviour>();

            Log.LogInfo("[Minimap] === Pronto! [M] = mapa | [L] = highlight inimigos ===");
        }
    }

    // ====================================================================
    // MinimapBehaviour — mapa + silhouette outline via RenderTexture
    // ====================================================================
    public class MinimapBehaviour : MonoBehaviour
    {
        // --- Mapa ---
        private const float MAP_SIZE            = 220f;
        private const float MAP_MARGIN          = 16f;
        private const float DOT_RADIUS          = 5f;
        private const float PLAYER_RADIUS       = 5f;
        private const float MAP_RANGE           = 80f;

        // --- Outline ---
        private static readonly Color OUTLINE_COLOR = new Color(1f, 0.3f, 0.05f, 1f);
        // Numero de pixels de dilatacao do outline (quanto maior, mais grosso)
        private const int DILATE_PIXELS = 3;

        // --- Scan ---
        private const float ENEMY_SCAN_INTERVAL  = 0.5f;
        private const float PLAYER_SCAN_INTERVAL = 2.0f;

        // --- Estado ---
        private bool _showMap       = false;
        private bool _showHighlight = false;
        private Rect _panelRect;

        // GUI
        private Texture2D _bgTex, _dotRedTex, _dotCyanTex;
        private GUIStyle  _titleStyle;

        // Cache
        private List<EnemyIdentifier> _cachedEnemies = new List<EnemyIdentifier>();
        private GameObject            _cachedPlayer  = null;
        private float                 _enemyTimer    = 0f;
        private float                 _playerTimer   = 0f;

        // Outline — CommandBuffer na camera principal
        private CommandBuffer _cmdBuf;
        private Camera        _attachedCamera;

        // Material que pinta tudo a branco (para a silhueta)
        // Material que dilata + coloriza (blur horizontal/vertical)
        private Material _silhouetteMat;
        private Material _dilateMat;
        private Material _compositeMat;

        // RenderTextures para o pipeline de outline
        private RenderTexture _silRT;   // silhueta a branco
        private RenderTexture _dilateH; // dilatacao horizontal
        private RenderTexture _dilateV; // dilatacao vertical (final)

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
        // MATERIAIS
        // ================================================================
        void BuildMaterials()
        {
            var colored = Shader.Find("Hidden/Internal-Colored");

            // Pinta tudo a branco puro — usado para desenhar a silhueta dos inimigos
            _silhouetteMat = new Material(colored);
            _silhouetteMat.hideFlags = HideFlags.HideAndDontSave;
            _silhouetteMat.SetColor("_Color", Color.white);
            _silhouetteMat.SetInt("_ZTest",  (int)CompareFunction.Always); // ve atraves de paredes
            _silhouetteMat.SetInt("_ZWrite", 0);
            _silhouetteMat.SetInt("_Cull",   (int)CullMode.Off);
            _silhouetteMat.SetInt("_SrcBlend", (int)BlendMode.One);
            _silhouetteMat.SetInt("_DstBlend", (int)BlendMode.Zero);

            // Dilata a silhueta (blur box simples) e aplica a cor do outline
            // Usa o shader de blit do Unity (Hidden/BlitCopy nao faz nada util,
            // mas Hidden/Internal-Colored com uma textura como _MainTex funciona)
            var blitShader = Shader.Find("Hidden/Internal-GUITextureClip");
            if (blitShader == null) blitShader = colored; // fallback

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
                Plugin.Log.LogInfo($"[Minimap] mapa {(_showMap ? "aberto" : "fechado")}");
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
        // COMMAND BUFFER — pipeline de outline
        //
        // Evento: AfterForwardOpaque (roda depois da geometria, antes do post)
        //
        // Passo 1: limpa _silRT a preto
        // Passo 2: para cada inimigo, desenha todos os seus meshes com
        //          _silhouetteMat (ZTest Always) => silhueta branca no _silRT
        // Passo 3: dilata _silRT N pixels horizontalmente => _dilateH
        // Passo 4: dilata _dilateH N pixels verticalmente => _dilateV
        // Passo 5: compoe _dilateV por cima do framebuffer com a cor do outline
        //          usando alpha = valor do pixel dilatado (branco = outline visivel)
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

            // --- Passo 1: limpa silRT a preto ---
            _cmdBuf.SetRenderTarget(_silRT);
            _cmdBuf.ClearRenderTarget(false, true, Color.clear);

            // --- Passo 2: desenha silhueta de cada inimigo ---
            foreach (var enemy in _cachedEnemies)
            {
                if (enemy == null) continue;

                // SkinnedMeshRenderer (inimigos animados — a maioria no ULTRAKILL)
                foreach (var smr in enemy.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (smr == null || smr.sharedMesh == null) continue;
                    for (int sub = 0; sub < smr.sharedMesh.subMeshCount; sub++)
                        _cmdBuf.DrawRenderer(smr, _silhouetteMat, sub);
                }

                // MeshRenderer (inimigos estaticos)
                foreach (var mr in enemy.GetComponentsInChildren<MeshRenderer>())
                {
                    if (mr == null) continue;
                    var mf = mr.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    for (int sub = 0; sub < mf.sharedMesh.subMeshCount; sub++)
                        _cmdBuf.DrawRenderer(mr, _silhouetteMat, sub);
                }
            }

            // --- Passos 3+4+5: dilata e compoe via OnRenderObject ---
            // (feito em OnRenderObject porque o Blit com shaders custom
            //  no CommandBuffer requer shader properties que nao temos)
        }

        // Dilata e compoe em OnRenderObject (corre depois de BeforeImageEffects)
        void OnRenderObject()
        {
            if (!_showHighlight || _silRT == null) return;
            if (Camera.current != _attachedCamera)   return;

            // Dilata a silhueta com um box blur em CPU... na verdade vamos usar
            // Graphics.Blit com um material simples que apenas copia a textura
            // e depois desenhamos o outline colorido em GL

            // Abordagem final mais simples e que FUNCIONA em qualquer Unity:
            // Desenha a silRT como quad full-screen colorido com GL
            // O resultado e: outline branco onde os inimigos estao, ZTest Always
            // Aplicamos a cor do outline como tint

            if (Event.current != null) return; // so em render, nao em GUI

            GL.PushMatrix();
            GL.LoadOrtho();

            _silhouetteMat.SetPass(0);

            // Tint da cor do outline sobre a silhueta
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
        // MAPA
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
            GUI.Label(new Rect(_panelRect.x, _panelRect.y + 4f, MAP_SIZE, 18f), "MAPA  [M]", _titleStyle);

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
        // UTILITARIOS
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