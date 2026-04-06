# ULTRAKILL Minimap & Enemy Tracker

[cite_start]A specialized BepInEx plugin for **ULTRAKILL** that provides a real-time minimap and an enemy highlighting system to help you track targets during intense combat[cite: 1].

---

## 📋 Prerequisites (For Beginners)

If you have **never modded a game before**, you must install these core components first to make the plugin work:

1.  **The Game**: Ensure **ULTRAKILL** is installed on your system.
2.  **BepInEx 5.4.x**: This is the "mod loader." 
    * Download the **x64** version of BepInEx 5.4.
    * Extract all files from the BepInEx zip directly into your game's main folder (where `ULTRAKILL.exe` is located).
3.  **.NET Runtime**: 
    * **Players**: Download and install the [.NET Desktop Runtime 6.0](https://dotnet.microsoft.com/download/dotnet) or higher to ensure the plugin can run.
    * [cite_start]**Developers**: This project targets **.NET Standard 2.1**[cite: 2]. You will need the [.NET SDK](https://dotnet.microsoft.com/download/dotnet) to compile the source code.

---

## 🚀 Installation (Step-by-Step)

### 1. Locate your Game Folder
* **Steam Users**: Right-click **ULTRAKILL** in your Steam Library > **Manage** > **Browse local files**.
* **Non-Steam / Pirated Users**: Open the folder where you manually extracted or installed the game.

### 2. Install the Plugin
* Navigate to the `BepInEx/plugins` folder inside your game directory.
* Copy the `UltrakillTrainerMap.dll` file into this folder.

### 3. Launch the Game
* Start ULTRAKILL. [cite_start]A console window should appear (if BepInEx is enabled), indicating that the "ULTRAKILL Enemy Minimap" plugin has loaded successfully[cite: 1].

---

## 🎮 How to Use (Hotkeys)

[cite_start]Once you are inside a level, use these keys to control the features[cite: 1]:

* [cite_start]**[M]**: Toggle the **Minimap** (A circular UI showing enemy positions)[cite: 1].
* [cite_start]**[L]**: Toggle **Enemy Highlights** (Displays silhouettes through walls)[cite: 1].

---

## 🛠 For Developers & Custom Installations

If you are using a non-Steam version or your game is installed in a custom directory, you must update the project references to compile the code.

### Fixing "Missing References"
[cite_start]The project needs to link to the game's internal libraries (like `UnityEngine.dll` and `Assembly-CSharp.dll`) to build[cite: 2].

1.  [cite_start]Open `UltrakillTrainer.csproj` in a text editor (like Notepad) or Visual Studio[cite: 2].
2.  Find this line:
    `<UltrakillPath>C:\Program Files (x86)\Steam\steamapps\common\ULTRAKILL</UltrakillPath>`[cite: 2].
3.  **Change the path** inside the tags to match your actual game folder (e.g., `C:\Games\ULTRAKILL`).
4.  Save the file and build your solution.

---

## 📦 Technical Dependencies
[cite_start]This plugin utilizes specific Unity modules for its functionality[cite: 2]:
* [cite_start]`UnityEngine.IMGUIModule`: Used for rendering the Minimap interface[cite: 2].
* [cite_start]`UnityEngine.CoreModule`: Core Unity functionality[cite: 2].
* [cite_start]`CommandBuffers`: Used for the high-performance silhouette pipeline[cite: 1].

## 📄 License
This project is released under the **MIT License**.
