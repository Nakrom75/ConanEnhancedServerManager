# Conan Enhanced Server Manager

A high-performance, native C# WPF management suite, watchdog supervisor, mod load-order orchestrator, and remote administration ecosystem (Windows Desktop, Web Dashboard & Android Companion App) for **Conan Exiles Dedicated Server** (Steam AppID `443030`).

Built to overcome the freezing, timeouts, mod update headaches, and process deadlocks of legacy server launchers through zero-latency SteamCMD streaming, live mod verification, smart load-order sorting, crash-loop forensics, RCON monitoring, and full remote administration.

---

## 📖 Complete User Manual

> **New to Conan Enhanced Server Manager?**
> Read the [**Complete User Manual (`USER_MANUAL.md`)**](USER_MANUAL.md) for step-by-step instructions on installation, router port forwarding, mod load-order sorting, crash loop recovery, hot backups, and connecting the Android / Remote PC clients!

---

## ✨ Key Features

- **Native Self-Contained Windows Engine (.NET 10 x64)**: Zero external runtime installations required.
- **4 Ways to Manage Your Server**:
  1. **Windows Local Host Mode**: Run directly on your dedicated server machine.
  2. **Windows Remote Client Mode**: Run the Windows desktop app on your gaming PC to remotely control your server over LAN/WAN.
  3. **Embedded Web Dashboard (`0.0.0.0:8088`)**: Responsive browser-based control panel accessible from any device.
  4. **Android Companion App (`.apk`)**: Native Android wrapper with LAN server discovery, live mod management, Workshop browser, RCON console, and OTA updates.
- **Intelligent Mod Management & Steam Workshop Integration**:
  - Built-in **Steam Workshop Browser** & instant search bar with one-click mod installation.
  - **Live Mod Load Verification**: Parses `ConanSandbox.log` in real time to display `🟢 Loaded & Active`, `📁 Ready on Disk`, `⬇️ Downloading`, `⏳ Pending Restart`, or `❌ Load Error` badges.
  - **Automatic Workshop Update Checker**: Polls Steam Workshop every 10 minutes for mod updates (`🔄 Update Available`) and reconciles `appworkshop_440900.acf` & `.pak` timestamps post-download (`✅ Updated!`).
  - **8-Tier Smart Load Order Auto-Sorting**: Automatically sorts mods according to the Conan Exiles community load order standard (Core Frameworks -> Map Extensions -> Total Overhauls -> Gameplay -> Building/Decor -> Cosmetics -> LBPR/Placement -> Lighting).
  - **Inactive / Disabled Mods Holding Area**: Temporarily disable mods without deleting their `.pak` files while waiting for mod authors to release updates.
- **Crash Loop Protection & Culprit Mod Forensics**:
  - Automatically pauses infinite restart loops if the server crashes 3 times within 10 minutes of startup.
  - Scans `ConanSandbox.log` before UE4 overwrites it to pinpoint the exact offending mod and provides a one-click **`🚫 Disable Offending Mod & Restart`** recovery button.
- **Zero-Latency SteamCMD Streaming**: Character-level stdout parser with live percentage, transfer speed (`MB/s`), and real-time `ETA`.
- **Failsafe 3-Step Process Termination & 30s RCON Watchdog**: Graceful RCON player countdowns, freeze/deadlock detection, and clean process shutdown.
- **Online SQLite Hot Backups**: Transactional live backups of `game.db`, `.ini` files, and `modlist.txt` while the server is running with zero downtime.
- **Real-Time INI Synchronization & Direct Code Editor**: Bidirectional sync with `ServerSettings.ini`, `Engine.ini`, and `Game.ini`, plus a built-in raw INI editor.

---

## 🚀 Quick Start Guide

### 1. Windows Server / PC Installation
1. Download the latest `ConanServerManager_vX.Y.Z.zip` (or `ConanServerManager_DeployPackage.zip`) from [GitHub Releases](https://github.com/Nakrom75/ConanEnhancedServerManager/releases).
2. Extract to a dedicated folder on your server drive (e.g., `C:\ConanServerManager\`).
3. Run **`ConanServerManager.exe`** (or `START_SERVER_MANAGER.bat`).
4. Configure your **Server Name**, **Passwords**, and **Ports** (click **`🛡️ Configure Firewall`** to automatically add Windows Firewall rules).
5. Click **`▶ Update & Start`**. The manager will automatically install SteamCMD, download the Conan Exiles Dedicated Server and any configured Workshop mods, and launch your server.

### 2. Android Companion App Installation
1. Download the latest `ConanServerManager-vX.Y.Z.apk` from [GitHub Releases](https://github.com/Nakrom75/ConanEnhancedServerManager/releases) (or directly from your running server at `http://<Server-IP>:8088/api/update/apk`).
2. Open the app, tap the top **Server Selector**, and tap **`🔍 Scan LAN`** (or enter `http://<Server-IP>:8088`) to connect.

---

## 🛠️ Building from Source

### Prerequisites
- Windows 10 / 11 / Windows Server 2016+
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- *(For Android APK)* JDK 17 + Android SDK Platform 34 + Gradle 8.5+

### Build Commands (Windows)
```powershell
# Restore and Publish Standalone Self-Contained win-x64 Package
dotnet publish src/ConanServerManager.csproj -c Release -r win-x64 --self-contained true -o ServerManager/
```

---

## 📚 Documentation & Technical Reference

- **[USER_MANUAL.md](USER_MANUAL.md)** — Complete step-by-step end-user guide, port forwarding table, mod load-order guide, and troubleshooting FAQ.
- **[SOURCE_OF_TRUTH.md](SOURCE_OF_TRUTH.md)** — Comprehensive architectural reference, protocol specifications, RCON packet schemas, and release history.

---

## 📄 License

Distributed under the MIT License. See `LICENSE` for more information.
