# ULTRAKILL Minimap & Enemy Tracker

A specialized BepInEx plugin for **ULTRAKILL** that provides a real-time minimap and an enemy highlighting system to help you track targets during intense combat.

---

## 📋 Prerequisites (For Beginners)

If you have **never modded a game before**, you must install these core components first to make the plugin work:

1.  **The Game**: Ensure **ULTRAKILL** is installed on your system.
2.  **BepInEx 5.4.x**: This is the "mod loader." 
    * Download the **x64** version of BepInEx 5.4.
    * Extract all files from the BepInEx zip directly into your game's main folder (where `ULTRAKILL.exe` is located).
3.  **.NET Runtime**: 
    * **Players**: Download and install the [.NET Desktop Runtime 6.0](https://dotnet.microsoft.com/download/dotnet) or higher to ensure the plugin can run.
    * **Developers**: This project targets **.NET Standard 2.1**. You will need the [.NET SDK](https://dotnet.microsoft.com/download/dotnet) to compile the source code.

---

## 🚀 Installation (Step-by-Step)

### 1. Locate your Game Folder
* **Steam Users**: Right-click **ULTRAKILL** in your Steam Library > **Manage** > **Browse local files**.
* **Non-Steam / Pirated Users**: Open the folder where you manually extracted or installed the game.

### 2. Install the Plugin
* Navigate to the `BepInEx/plugins` folder inside your game directory.
* Copy the `UltrakillTrainerMap.dll` file into this folder.

### 3. Launch the Game
* Start ULTRAKILL. A console window should appear (if BepInEx is enabled), indicating that the "ULTRAKILL Enemy Minimap" plugin has loaded successfully.

---

## 🎮 How to Use (Hotkeys)

Once you are inside a level, use these keys to control the features:

* **[M]**: Toggle the **Minimap** (A circular UI showing enemy positions).
* **[L]**: Toggle **Enemy Highlights** (Displays silhouettes through walls).

---

## 🛠 For Developers & Custom Installations

If you are using a non-Steam version or your game is installed in a custom directory, you must update the project references to compile the code.

### Fixing "Missing References"
The project needs to link to the game's internal libraries (like `UnityEngine.dll` and `Assembly-CSharp.dll`) to build.

1.  Open `UltrakillTrainer.csproj` in a text editor (like Notepad) or Visual Studio.
2.  Find this line:
    `<UltrakillPath>C:\Program Files (x86)\Steam\steamapps\common\ULTRAKILL</UltrakillPath>`.
3.  **Change the path** inside the tags to match your actual game folder (e.g., `C:\Games\ULTRAKILL`).
4.  Save the file and build your solution.

---
## 📄 License
This project is released under the **MIT License**.
