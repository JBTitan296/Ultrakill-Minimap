# ULTRAKILL Minimap & Enemy Tracker

[cite_start]A high-performance BepInEx plugin for **ULTRAKILL** that adds a real-time minimap system[cite: 1]. [cite_start]This tool is designed to help you track enemies through walls and navigate levels more effectively[cite: 1].

---

## 📋 Prerequisites

If you have **never modded a game before**, you must complete these steps first:

1.  [cite_start]**BepInEx 5.4.x (x64)**: This is the mod loader required to run the plugin[cite: 1].
    * Download the **x64** version of BepInEx 5.4.
    * Extract all files from the BepInEx `.zip` directly into your main ULTRAKILL folder (where `ULTRAKILL.exe` is located).
2.  **.NET Desktop Runtime 6.0**: 
    * Required for the plugin to run on your system. You can download it from the [official Microsoft website](https://dotnet.microsoft.com/download/dotnet/6.0).

---

## 🚀 Installation

### For Steam Users (Easiest)
1.  Go to the **Releases** section of this GitHub repository.
2.  Download the latest `UltrakillTrainerMap.dll`.
3.  Navigate to your game folder: `SteamLibrary\steamapps\common\ULTRAKILL\BepInEx\plugins`.
4.  Drop the `.dll` file into the `plugins` folder.

### For Non-Steam / Custom Installations
If you are using a different version of the game, the pre-compiled `.dll` might not find the correct paths. You will need to compile it yourself:
1.  [cite_start]Open the `UltrakillTrainer.csproj` file in a text editor (like Notepad)[cite: 2].
2.  [cite_start]Locate the line: `<UltrakillPath>C:\Program Files (x86)\Steam\steamapps\common\ULTRAKILL</UltrakillPath>`[cite: 2].
3.  [cite_start]Change that path to your actual game folder (e.g., `D:\Games\ULTRAKILL`)[cite: 2].
4.  Build the project using Visual Studio or the `.NET SDK`.

---

## 🎮 Controls & Commands

Once you are in a level, use these hotkeys to control the plugin:

| Key | Action |
| :--- | :--- |
| **[M]** | [cite_start]**Toggle Minimap**: Shows/hides the circular radar in the top corner[cite: 1]. |

---

## 🛠 Troubleshooting (Common Errors)

| Problem | Cause | Solution |
| :--- | :--- | :--- |
| **No console or map appears** | BepInEx is not installed correctly. | Ensure the `BepInEx` folder and `winhttp.dll` are in the same folder as `ULTRAKILL.exe`. |
| **"Missing References" during build** | Incorrect game path in `.csproj`. | [cite_start]Open `UltrakillTrainer.csproj` and update the `<UltrakillPath>` to your local folder[cite: 2]. |
| **The map is empty** | Plugin is working but can't find enemies. | [cite_start]The map updates every 0.5 seconds; ensure you are in a level with active enemies[cite: 1]. |
| **Game crashes on startup** | Incompatible version or missing .NET. | Ensure you have installed **.NET Desktop Runtime 6.0**. |

---

## 📦 Technical Dependencies
[cite_start]The plugin links to several Unity and game modules[cite: 2]:
* [cite_start]**UnityEngine.IMGUIModule**: Renders the Map UI[cite: 2].
* [cite_start]**UnityEngine.CoreModule**: Core game engine functions[cite: 2].
* [cite_start]**Assembly-CSharp**: Primary ULTRAKILL logic[cite: 2].
* [cite_start]**CommandBuffers**: Optimized rendering for the enemy outlines[cite: 1].

## 📄 License
This project is released under the **MIT License**.
