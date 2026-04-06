# ULTRAKILL Minimap & Enemy Tracker

A **BepInEx** plugin for **ULTRAKILL** that adds a **real-time minimap** and **enemy tracker**.
This tool helps you **see enemies through walls** and navigate levels more efficiently.

---

![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg) ![BepInEx](https://img.shields.io/badge/BepInEx-5.4.x-blue.svg)

---

## 📋 Prerequisites

Before using this plugin, ensure you have:

1. **BepInEx 5.4.x (x64)** – the mod loader required to run the plugin:

   * Download the **x64 version** from [BepInEx 5.4.x Releases](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.21).
   * Extract all files **directly** into your main ULTRAKILL folder (where `ULTRAKILL.exe` is located).
2. **.NET Desktop Runtime 6.0** – required to run the plugin:

   * Download it from the [Microsoft .NET 6.0 download page](https://dotnet.microsoft.com/en-us/download/dotnet/6.0/runtime).

> ⚠ If you've never modded a game before, follow these steps carefully.

---

## 🚀 Installation

### Steam Users (Easiest)

1. Go to the **[Releases](https://github.com/JBTitan296/Ultrakill-Minimap/releases)** section of this GitHub repository.
2. Download the latest `UltrakillTrainerMap.dll`.
3. Navigate to your game folder:
   `SteamLibrary\steamapps\common\ULTRAKILL\BepInEx\plugins`
4. Place the `.dll` file inside the `plugins` folder.
5. Launch ULTRAKILL and press **M** to toggle the minimap.

---

### Non-Steam / Custom Installations

If your game is installed in a **different folder**, you need to **compile the plugin** yourself:

#### Step 1 – Edit the Project Path

1. Open the `UltrakillTrainer.csproj` file in a text editor (like **Notepad** or **VS Code**).
2. Find the line:

   ```xml
   <UltrakillPath>C:\Program Files (x86)\Steam\steamapps\common\ULTRAKILL</UltrakillPath>
   ```
3. Change it to the folder where your game is installed. Example:

   ```xml
   <UltrakillPath>D:\Games\ULTRAKILL</UltrakillPath>
   ```
4. Save the file.

#### Step 2 – Build the DLL

**Option 1 – Using Visual Studio:**

1. Open the `UltrakillTrainer.csproj` project in Visual Studio.

   * Download Visual Studio Community: [Visual Studio](https://visualstudio.microsoft.com/).
2. Click **Build → Build Solution**.
3. After building, locate your DLL in:

   ```
   UltrakillTrainer\bin\Release\net6.0\UltrakillTrainerMap.dll
   ```
4. Copy the DLL into your `BepInEx\plugins` folder.

**Option 2 – Using .NET SDK (Command Line):**

1. Open a terminal or Command Prompt.
2. Navigate to the project folder containing `UltrakillTrainer.csproj`.
3. Run:

   ```bash
   dotnet build UltrakillTrainer.csproj -c Release
   ```
4. Copy the resulting DLL as shown above into `BepInEx\plugins`.

---

## 🎮 Controls & Commands

Once in a level:

| Key   | Action                                           |
| :---- | :----------------------------------------------- |
| **M** | Toggle the **Minimap** on/off in the top corner. |

> The minimap updates every **0.5 seconds**, showing enemy positions in real time.

---

## 🛠 Troubleshooting

| Problem                               | Cause                                        | Solution                                                                                 |
| :------------------------------------ | :------------------------------------------- | :--------------------------------------------------------------------------------------- |
| **No console or map appears**         | BepInEx not installed correctly              | Ensure the `BepInEx` folder and `winhttp.dll` are in the same folder as `ULTRAKILL.exe`. |
| **"Missing References" during build** | Incorrect `<UltrakillPath>` in `.csproj`     | Open `UltrakillTrainer.csproj` and update the path to your ULTRAKILL folder.             |
| **Map is empty**                      | No enemies in level or plugin not detecting  | Ensure you are in a level with active enemies; map updates every 0.5s.                   |
| **Game crashes on startup**           | Missing .NET runtime or incompatible version | Install **.NET Desktop Runtime 6.0** and check ULTRAKILL version.                        |

---

## 📝 Step-by-Step Visual Guide

1. **Install BepInEx**
   Extract BepInEx into your ULTRAKILL folder:

   ```
   ULTRAKILL.exe
   ├─ BepInEx
   ├─ winhttp.dll
   ```

2. **Download the Plugin**

   * From GitHub Releases: `UltrakillTrainerMap.dll`

3. **Put DLL in Plugins Folder**

   ```
   BepInEx\plugins\UltrakillTrainerMap.dll
   ```

4. **Custom Installation** (if not Steam)

   * Edit `UltrakillTrainer.csproj` → `<UltrakillPath>`
   * Build DLL → Copy to `plugins` folder

5. **Launch Game**

   * Press **M** to toggle minimap.

---

## 📄 License

This project is released under the MIT License.
