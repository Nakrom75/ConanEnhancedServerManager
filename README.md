# Conan Enhanced Server Manager

A high-performance, native C# WPF management suite, watchdog supervisor, and remote administration console for **Conan Exiles Dedicated Server** (Steam AppID `443030`).

Built to overcome the freezing, timeouts, and process deadlocks of legacy server launchers through zero-latency streaming, automated SteamCMD mod orchestration, robust RCON monitoring, and an embedded web dashboard.

---

## Key Features

- **Native C# .NET 10 Engine**: Self-contained x64 architecture. No external runtime installations required.
- **Zero-Latency SteamCMD Streaming**: Character-level stdout parser with live percentage, transfer speed (MB/s), and real-time ETA calculation.
- **Failsafe 3-Step Process Termination**: Combines graceful RCON countdowns, PID timeouts, and Windows kernel cleanup to guarantee zero lingering zombie processes.
- **30-Second Deadlock & Crash Watchdog**: Continuously monitors process status and sends periodic RCON heartbeats; automatically restarts frozen or crashed servers.
- **Embedded Web Server & Remote Console (0.0.0.0:8088)**: Socket-based HTTP REST API and responsive mobile/desktop web console bypassing Windows HTTP.sys restrictions.
- **Online SQLite Hot Backup**: Executes transactional live backups of `game.db` without taking the game server offline.
- **Automatic GitHub Updates**: Built-in release detection and self-updating engine via GitHub Releases.
- **Real-Time INI Synchronization**: Full bidirectional editing and real-time synchronization between GUI controls and `ServerSettings.ini`, `Engine.ini`, and `Game.ini`.

---

## Quick Start Guide

### Running from Pre-Compiled Release
1. Download the latest `ConanServerManager_DeployPackage.zip` from [Releases](https://github.com/Nakrom75/ConanEnhancedServerManager/releases).
2. Extract to a directory on your Windows Server machine (e.g. `C:\ConanServerManager`).
3. Run `START_SERVER_MANAGER.bat` (or `ConanServerManager.exe`).
4. Configure your Server Name, Admin Password, and Ports.
5. Click **▶ Update & Start Server**. The manager will automatically download the server files and mods via SteamCMD and start the server.

---

## Building from Source

### Prerequisites
- Windows 10 / 11 / Windows Server 2016+
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Build Commands
```powershell
# Navigate to source
cd src

# Restore and Build
dotnet build ConanServerManager.csproj -c Release

# Publish Standalone Self-Contained Package
dotnet publish ConanServerManager.csproj -c Release -r win-x64 --self-contained true -o ../ServerManager
```

---

## Architecture & Technical Reference

For comprehensive protocol specifications, port matrices, RCON packet schemas, and Unreal Engine 4 CLI argument breakdowns, see [SOURCE_OF_TRUTH.md](SOURCE_OF_TRUTH.md).

---

## License

Distributed under the MIT License. See `LICENSE` for more information.
