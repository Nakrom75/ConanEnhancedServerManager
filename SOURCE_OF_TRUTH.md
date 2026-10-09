# SOURCE OF TRUTH: Conan Exiles Dedicated Server & Manager Architecture

> **Document Version:** 1.3.0  
> **Target Application:** Conan Exiles Dedicated Server (AppID `443030`)  
> **Date:** October 2026  
> **Purpose:** Complete reverse-engineered architectural blueprint, specifications, protocol details, and engineering roadmap to build a custom, modern, highly reliable Conan Exiles Dedicated Server Manager.

---

## Table of Contents
1. [Executive Overview & Server Topology](#1-executive-overview--server-topology)
2. [Conan Exiles Server Architecture](#2-conan-exiles-server-architecture)
   - 2.1 [Executables & Steam App IDs](#21-executables--steam-app-ids)
   - 2.2 [Port Matrix & Networking Requirements](#22-port-matrix--networking-requirements)
   - 2.3 [Process Spawning & Command-Line Arguments](#23-process-spawning--command-line-arguments)
3. [Configuration & Persistence Layer](#3-configuration--persistence-layer)
   - 3.1 [INI Configuration Hierarchy](#31-ini-configuration-hierarchy)
   - 3.2 [SQLite Database (`game.db`) & Hot Backups](#32-sqlite-database-gamedb--hot-backups)
4. [Workshop & Modding Architecture](#4-workshop--modding-architecture)
   - 4.1 [Directory Structure & `.pak` Files](#41-directory-structure--pak-files)
   - 4.2 [Mod Loading Logic (`modlist.txt`)](#42-mod-loading-logic-modlisttxt)
   - 4.3 [SteamCMD Mechanics & Flaws in Official Launcher](#43-steamcmd-mechanics--flaws-in-official-launcher)
5. [RCON Protocol Specifications](#5-rcon-protocol-specifications)
   - 5.1 [Packet Structure & Handshake](#51-packet-structure--handshake)
   - 5.2 [Standard Commands & Workflows](#52-standard-commands--workflows)
6. [Detailed Analysis of Official `DedicatedServerLauncher`](#6-detailed-analysis-of-official-dedicatedserverlauncher)
   - 6.1 [Lifecycle & Operational Sequence](#61-lifecycle--operational-sequence)
   - 6.2 [Root Causes of Failures & Timeouts](#62-root-causes-of-failures--timeouts)
7. [Blueprint for a Superior Custom Server Manager](#7-blueprint-for-a-superior-custom-server-manager)
   - 7.1 [System Architecture](#71-system-architecture)
   - 7.2 [Module Specifications](#72-module-specifications)
   - 7.3 [Reference Implementations & Code Templates](#73-reference-implementations--code-templates)
8. [Implementation Roadmap](#8-implementation-roadmap)
9. [Progress Log & Implementation Status](#9-progress-log--implementation-status)
10. [Official Funcom DedicatedServerLauncher Specifications & Reference Manual](#10-official-funcom-dedicatedserverlauncher-specifications--reference-manual)
...
16. [Remote Client Persistence, Full Remote Sync & Steam Visibility Fixes (v1.0.4)](#16-remote-client-persistence-full-remote-sync--steam-visibility-fixes-v104)
17. [Hotfix: XAML Startup Lifecycle & Initialization Guards (v1.0.5)](#17-hotfix-xaml-startup-lifecycle--initialization-guards-v105)
18. [Application Icon Integration & Visual Polish (v1.0.6)](#18-application-icon-integration--visual-polish-v106)
19. [Permanent Players Sidebar & Web Interface Live Players (v1.0.7)](#19-permanent-players-sidebar--web-interface-live-players-v107)
20. [Remote Server Dropdown, Network & VM Discovery, and Custom Server Selection (v1.0.8)](#20-remote-server-dropdown-network--vm-discovery-and-custom-server-selection-v108)
21. [Android Mobile Remote Client App, Steam Workshop Live Mod Browser & Remote Management (v1.1.0)](#21-android-mobile-remote-client-app-apk-steam-workshop-live-mod-browser--remote-management-and-in-app-auto-updates-v110)
22. [Full-Screen Mobile Server Selection & Unified Versioning (v1.1.1)](#22-full-screen-mobile-server-selection--unified-versioning-v111)
23. [Windows In-App Self-Update Engine & Trampoline Orchestration (v1.1.2 - v1.1.4)](#23-windows-in-app-self-update-engine--trampoline-orchestration-v112---v114)
24. [SteamCMD Automated Deployment & Error Resilience (v1.1.5)](#24-steamcmd-automated-deployment--error-resilience-v115)
25. [Steam Workshop Mod Title Resolution & Local Cache (v1.1.6)](#25-steam-workshop-mod-title-resolution--local-cache-v116)
26. [Mod Steam Workshop Webpage Context Actions (v1.1.7)](#26-mod-steam-workshop-webpage-context-actions-v117)
27: [XAML Style Hierarchy & Startup Crash Elimination (v1.1.8)](#27-xaml-style-hierarchy--startup-crash-elimination-v118)
28. [Steam Master Server Announcement, FLS Registration & RCON Mapping Architecture (v1.1.9)](#28-steam-master-server-announcement-fls-registration--rcon-mapping-architecture-v119)
29. [Self-Contained Deployment & .NET Runtime Independence (v1.1.10)](#29-self-contained-deployment--net-runtime-independence-v1110)
30. [Zero-Data-Loss Architecture: Configuration Ingestion, Packaging Isolation & INI Integrity (v1.1.11)](#30-zero-data-loss-architecture-configuration-ingestion-packaging-isolation--ini-integrity-v1111)
31. [Instant Launch Controls, Hot Backup Multi-DB, Custom Paths & Auto-Restart Scheduler (v1.1.12)](#31-instant-launch-controls-hot-backup-multi-db-custom-paths--auto-restart-scheduler-v1112)
32. [In-App Steam Workshop Chromium & Mobile Browser Engine (v1.2.0)](#32-in-app-steam-workshop-chromium--mobile-browser-engine-v120)
33. [Strict Version Numbering & Synchronized Dual-Platform Build Policy](#33-strict-version-numbering--synchronized-dual-platform-build-policy)
34. [Full-Page Workshop Browser Overlay & Dual-Platform Synchronization (v1.2.1)](#34-full-page-workshop-browser-overlay--dual-platform-synchronization-v121)
35. [Background SteamCMD Mod Pre-Download & Cache Engine (v1.2.2)](#35-background-steamcmd-mod-pre-download--cache-engine-v122)
36. [Linux Fedora KDE Desktop Client & Cross-Platform Avalonia Architecture (v1.3.0)](#36-linux-fedora-kde-desktop-client--cross-platform-avalonia-architecture-v130)
37. [Future Roadmap & Upcoming Engineering Tasks (To-Do)](#37-future-roadmap--upcoming-engineering-tasks-to-do)

---

## 1. Executive Overview & Server Topology

> [!CAUTION]
> **CRITICAL OPERATIONAL CONSTRAINT — ZERO DIRECT LIVE DEPLOYMENTS:**  
> The agent must **NEVER** attempt to copy, deploy, overwrite, or update files on the live server (`\\192.168.0.5\ConanServerManager\` or any remote host environment).  
> All live server updates, file transfers, binary installations, and maintenance are handled strictly and exclusively by the user manually.  
> The agent's scope is strictly confined to the local development repository (`F:\Projects\Conan Exiles Dedicated Server`), producing local builds (`ServerManager/`, release ZIP archives, and release APKs).

Conan Exiles Dedicated Server is built on **Unreal Engine 4 (UE4)** with custom persistent game subsystems maintained by Funcom. The server runs as a headless Windows console/service application interacting with Steam via the **Steamworks SDK** and SteamCMD.

### High-Level System Topology
```
┌────────────────────────────────────────────────────────────────────────┐
│                      CUSTOM SERVER MANAGER                             │
│  - Web UI / Desktop App      - SteamCMD Orchestrator                   │
│  - Process Watchdog Engine   - Hot SQLite Backup System                │
│  - RCON Client & Automation  - Discord Webhook / Bot Service           │
└───────┬──────────────────────────┬────────────────────────────┬────────┘
        │                          │                            │
        ▼ (Process CLI)            ▼ (TCP / RCON)               ▼ (Filesystem)
┌─────────────────┐       ┌─────────────────┐       ┌────────────────────────┐
│    SteamCMD     │       │   RCON Client   │       │ File Management        │
│                 │       │                 │       │ - modlist.txt          │
│ - App 443030    │       │ - Broadcasts    │       │ - ServerSettings.ini   │
│ - Workshop      │       │ - Saves         │       │ - Engine.ini           │
│   440900        │       │ - Clean Stop    │       │ - game.db (SQLite)     │
└─────────────────┘       └────────┬────────┘       └────────────────────────┘
                                   │
                                   ▼ (Port 25575 TCP)
┌────────────────────────────────────────────────────────────────────────┐
│             ConanSandboxServer-Win64-Shipping.exe (UE4)                │
│  - Game UDP: 7777        - Steam Query UDP: 27015                      │
│  - Raw UDP: 7778         - RCON TCP: 25575                             │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Conan Exiles Server Architecture

### 2.1 Executables & Steam App IDs
* **Dedicated Server Steam AppID:** `443030`
* **Conan Exiles Client & Workshop AppID:** `440900`
* **Direct Executable Path:**  
  `DedicatedServerLauncher\ConanExilesDedicatedServer\ConanSandbox\Binaries\Win64\ConanSandboxServer-Win64-Shipping.exe`
* **SteamCMD Path:**  
  `DedicatedServerLauncher\SteamCMD.exe`

### 2.2 Port Matrix & Networking Requirements
The server requires specific inbound firewall and NAT forwarding rules:

| Port | Protocol | Purpose | Config Location |
| :--- | :--- | :--- | :--- |
| **7777** | UDP | Primary Game Client Traffic | `Engine.ini` (`Port`) / CLI `-Port=7777` |
| **7778** | UDP | Raw UDP Traffic (UE4 NetDriver) | Primary Game Port + 1 |
| **27015** | UDP | Steam Master Server Query | `Engine.ini` / CLI `-QueryPort=27015` |
| **25575** | TCP | RCON Remote Administration | `ServerSettings.ini` / CLI `-RconPort=25575` |
| **80** (or custom) | TCP | Launcher Web UI Status API | `DedicatedServerLauncher.ini` |

### 2.3 Process Spawning & Command-Line Arguments
To launch the dedicated server cleanly, the executable is invoked with command-line flags. Example launch string:

```bat
ConanSandboxServer-Win64-Shipping.exe -log -Port=7777 -QueryPort=27015 -RconPort=25575 -RconPassword="YourRconPassword" -MaxPlayers=40 -nosteamclient -game -server
```

Key CLI Arguments:
* `-log`: Enables stdout logging to `ConanSandbox\Saved\Logs\ConanSandbox.log`.
* `-Port=<int>`: Overrides primary game port.
* `-QueryPort=<int>`: Sets Steam server browser query port.
* `-RconPort=<int>`: Sets RCON listening port.
* `-RconPassword="<str>"`: Sets plaintext authentication password for RCON.
* `-MaxPlayers=<int>`: Caps max concurrent player sessions.
* `-MULTIHOME=<ip>`: Binds the server to a specific local IP address (useful on multi-NIC hosts).
* `-nosteamclient`: Prevents the binary from trying to attach to an active Steam desktop client.

---

## 3. Configuration & Persistence Layer

### 3.1 INI Configuration Hierarchy
Server configs reside in:  
`DedicatedServerLauncher\ConanExilesDedicatedServer\ConanSandbox\Saved\Config\WindowsServer\`

#### 1. `ServerSettings.ini`
Controls all in-game gameplay constants, server visibility, and mod lists:
```ini
[ServerSettings]
ServerName=My Conan Server
ServerPassword=
AdminPassword=SuperSecretAdminPassword
RconEnabled=True
RconPassword=SuperSecretRconPassword
RconPort=25575
RconMaxKarma=60
PlayerPVPMode=1
DedicatedServerLauncherModList=3722388367,3803465771,3723073788,3722270581
```

#### 2. `Engine.ini`
Controls Unreal Engine networking, tick rates, and socket bindings:
```ini
[URL]
Port=7777

[/Script/OnlineSubsystemUtils.IpNetDriver]
NetServerMaxTickRate=30
LanServerMaxTickRate=30

[/Script/Engine.GameEngine]
bSmoothFrameRate=false
```

#### 3. `Game.ini`
Controls map selection and player access control:
```ini
[/Script/Engine.GameSession]
MaxPlayers=40

[GameSettings]
; Maps:
; Exiled Lands: /Game/Maps/ConanSandbox/ConanSandbox
; Isle of Siptah: /Game/DLC_EXT/DLC_Siptah/Maps/DLC_Isle_of_Siptah
StartupMap=/Game/Maps/ConanSandbox/ConanSandbox
```

### 3.2 SQLite Database (`game.db`) & Hot Backups
* **Location:** `ConanSandbox\Saved\game.db`
* **Format:** Standard **SQLite 3 database**.
* **Contents:**
  * `characters`: Player character names, guilds, health, stats.
  * `character_skills`: Skill point allocations.
  * `buildings` & `item_inventory`: All placed structures, chests, items.
  * `guilds`: Clan associations and ranks.

> [!WARNING]
> **Database Locks & Corruption Risk:**  
> Conan Exiles holds an exclusive or WAL lock on `game.db` while running. Directly executing a raw filesystem copy while the server is writing transactions can cause file corruption or half-written journal files (`game.db-wal`, `game.db-shm`).

#### Safe Backup Strategy
1. **Option A (Safe Hot Backup via SQLite API):** Use SQLite Online Backup API (`sqlite3_backup_init` or Python's `sqlite3.connect('game.db').backup(backup_db)`). This safely acquires read locks in chunks without interrupting server execution.
2. **Option B (RCON Flush + Copy):** Send RCON command `save`, wait 3 seconds for disk flush, then perform the copy.
3. **Option C (Offline Copy):** Back up automatically during server restart cycles while the server process is verified stopped.

---

## 4. Workshop & Modding Architecture

### 4.1 Directory Structure & `.pak` Files
Mods are authored in the Unreal Editor and packaged into Unreal `.pak` archives. When SteamCMD downloads a mod for Conan Exiles, it stores them in:
```
<ServerDir>\steamapps\workshop\content\440900\<WORKSHOP_ID>\
    └── <ModName>.pak
```

For example:
* Mod ID: `3722388367`
* Download path: `...\steamapps\workshop\content\440900\3722388367\ModControlPanel.pak`

### 4.2 Mod Loading Logic (`modlist.txt`)
Conan Exiles does **not** discover mods automatically from folders. It strictly parses a single text file:
* **File Path:** `<ServerDir>\ConanSandbox\Mods\modlist.txt`
* **Format:** Absolute file path to each `.pak` file, one per line.
* **Order of Execution:** The server loads mods sequentially from **top to bottom**. Later mods override assets/classes from earlier mods.

#### Example `modlist.txt`:
```text
C:\ConanServer\steamapps\workshop\content\440900\3722388367\ModControlPanel.pak
C:\ConanServer\steamapps\workshop\content\440900\3803465771\double_storage_space.pak
C:\ConanServer\steamapps\workshop\content\440900\3723073788\Frequent_meteors.pak
C:\ConanServer\steamapps\workshop\content\440900\3722270581\Spawn_98%_Women.pak
C:\ConanServer\steamapps\workshop\content\440900\3723975720\Assets_Mod.pak
```

### 4.3 SteamCMD Mechanics & Flaws in Official Launcher

#### How SteamCMD Downloads Mods
```powershell
steamcmd.exe +force_install_dir "<ServerDir>" +login anonymous +workshop_download_item 440900 <MOD_ID> validate +quit
```

#### Why the Official Launcher Fails (The 5-Minute Bug)
The official `DedicatedServerLauncher1904.exe` generates a batch file (`SteamCMDLauncher.bat`) containing a single concatenated command:
```powershell
steamcmd.exe ... +workshop_download_item 440900 MOD1 validate +workshop_download_item 440900 MOD2 validate +workshop_download_item 440900 MOD3 validate ...
```
1. SteamCMD has an internal network/IO timeout threshold of **300 seconds (5 minutes)** per download chunk.
2. Large mods (such as `3723975720` - River Boats, which is ~3.8 GB) cannot always complete within 5 minutes depending on Steam content delivery network throttling.
3. Once SteamCMD encounters `ERROR! Timeout downloading item <ID>`, it immediately terminates with exit code 10 and discards the rest of the command line.
4. All subsequent mods in the command chain are skipped.
5. The launcher does not re-attempt download of failed items; it simply stops and only registers the successfully downloaded mods to `modlist.txt`.

---

## 5. RCON Protocol Specifications

Conan Exiles implements the standard **Valve/Source RCON Protocol** over TCP.

### 5.1 Packet Structure & Handshake
All packets are little-endian integers followed by null-terminated strings:

```
┌─────────────────┬────────────────────────────────────────────────────────┐
│ Field           │ Type / Description                                     │
├─────────────────┼────────────────────────────────────────────────────────┤
│ Size            │ 32-bit signed int (Length of remaining packet bytes)   │
│ ID              │ 32-bit signed int (Client request ID, reflected back)  │
│ Type            │ 32-bit signed int (Packet Type)                        │
│ Body            │ Null-terminated ASCII/UTF-8 string (Command or reply)  │
│ Empty Pad       │ 1-byte null terminator (0x00)                          │
└─────────────────┴────────────────────────────────────────────────────────┘
```

#### Packet Types:
* `3` = `SERVERDATA_AUTH` (Sent by client to authenticate)
* `2` = `SERVERDATA_AUTH_RESPONSE` (Sent by server: ID = client ID on success, -1 on auth failure)
* `2` = `SERVERDATA_EXECCOMMAND` (Sent by client to execute a command)
* `0` = `SERVERDATA_RESPONSE_VALUE` (Sent by server with command output string)

### 5.2 Standard Commands & Workflows
* `broadcast <message>`: Displays a prominent banner notification across all connected player screens.
* `save`: Forces the engine to flush all world state, player data, and building structures to `game.db`.
* `exit` or clean kill: Triggers immediate graceful shutdown.

#### Standard Restart Procedure:
```
Step 1: RCON -> broadcast Server will restart in 5 minutes for maintenance.
Step 2: Sleep 240s
Step 3: RCON -> broadcast Server restarting in 60 seconds! Find a safe area.
Step 4: Sleep 60s
Step 5: RCON -> save
Step 6: Sleep 3s
Step 7: RCON -> exit (or TerminateProcess if not responding within 15s)
```

---

## 6. Detailed Analysis of Official `DedicatedServerLauncher`

### 6.1 Lifecycle & Operational Sequence
1. **Startup:** Reads `DedicatedServerLauncher.ini` and `ServerSettings.ini`.
2. **IP & Port Validation:** Queries external web service to detect public IP and checks port availability.
3. **SteamCMD Invocation:** Writes `SteamCMDLauncher.bat` and runs it synchronously to update game files (`443030`) and workshop mods (`440900`).
4. **Modlist Assembly:** Scans workshop directory and writes `modlist.txt`.
5. **Process Spawning:** Spawns `ConanSandboxServer-Win64-Shipping.exe` in background.
6. **Watchdog Loop:** Polls the PID every few seconds. If PID vanishes, checks configured auto-restart policy.
7. **HTTP Server:** Runs an embedded Win32 socket web server on port 80 for remote status views.

### 6.2 Root Causes of Failures & Bottlenecks

| Problem | Root Cause | Impact |
| :--- | :--- | :--- |
| **Download Timeouts** | Monolithic `+workshop_download_item` chain in single SteamCMD execution. | Server fails to update mods > 1 GB. Server fails to boot with intended modlist. |
| **No Chunk Resumption** | Launcher treats SteamCMD exit code 10 as complete failure and doesn't retry cached `.patch` files. | Partial downloads are repeatedly redownloaded from scratch or abandoned. |
| **Brittle INI Serializer** | Launcher rewrites INI files directly; strips comments and sometimes corrupts custom UE4 section blocks. | Advanced server customizations get deleted on launcher save. |
| **Monolithic Win32 UI** | Single-threaded GUI thread frequently locks up when waiting on SteamCMD subprocess. | GUI freezes and shows "(Not Responding)" in Windows Task Manager. |

---

## 7. Blueprint for a Superior Custom Server Manager

### 7.1 System Architecture

```
┌────────────────────────────────────────────────────────────────────────┐
│                        FRONTEND (Web or Desktop)                       │
│  - Realtime Mod Browser (Steam Workshop API Integration)               │
│  - Live Server Console (WebSocket to stdout / RCON)                    │
│  - Player Session Table & Kick/Ban Controls                            │
│  - Automated Schedule & Backup Manager                                 │
└───────────────────────────────────▲────────────────────────────────────┘
                                    │ (REST / WebSockets)
┌───────────────────────────────────▼────────────────────────────────────┐
│                    CORE BACKEND (Python / Go / C#)                     │
│                                                                        │
│  ┌────────────────────────┐  ┌────────────────────────┐  ┌───────────┐ │
│  │ SteamCMD Orchestrator  │  │ Process Watchdog       │  │ RCON Hub  │ │
│  │ - Individual Mod DLs   │  │ - Process Lifetime     │  │ - Protocol│ │
│  │ - Exponential Backoff  │  │ - Heartbeat RCON Ping  │  │ - Queue   │ │
│  │ - Progress Parsing     │  │ - Crash Auto-Restart   │  │ - Announce│ │
│  └────────────────────────┘  └────────────────────────┘  └───────────┘ │
│  ┌────────────────────────┐  ┌────────────────────────┐  ┌───────────┐ │
│  │ Config Manager         │  │ Backup Manager         │  │ Discord   │ │
│  │ - AST INI Parser       │  │ - SQLite Online Backup │  │ - Webhook │ │
│  │ - Validation Rules     │  │ - 7-Day / 4-Week Rot.  │  │ - Bot API │ │
│  └────────────────────────┘  └────────────────────────┘  └───────────┘ │
└────────────────────────────────────────────────────────────────────────┘
```

### 7.2 Module Specifications

#### 1. SteamCMD Orchestrator with Resilient Download Engine
* **Rule:** Never execute multiple large mods in a single SteamCMD argument string.
* **Process:** Loop through configured Mod IDs sequentially.
* **Retry Engine:** If SteamCMD times out or returns code 10:
  * Check if the download file in `steamapps\workshop\downloads\440900\<ModID>` increased in size.
  * Re-run SteamCMD with `+workshop_download_item 440900 <ModID> validate`. SteamCMD will automatically resume from the partial cached chunk.
  * Maximum 5 retries per mod with 10-second exponential backoff.

#### 2. Automatic `modlist.txt` Generator
After all mods are validated:
1. Scan each `<ServerDir>\steamapps\workshop\content\440900\<ModID>\` directory.
2. Find the `.pak` file inside.
3. Write ordered absolute paths to `ConanSandbox\Mods\modlist.txt`.
4. Allow manual drag-and-drop reordering in the UI before server start.

#### 3. Heartbeat & Process Watchdog
* Don't just rely on `Process.HasExited`. A server can hang (infinite loop / deadlock) while the process is still alive.
* Send an RCON ping (e.g. `save` or empty command) every 60 seconds. If RCON fails to respond for 3 consecutive minutes, flag server as **hung** and trigger automated restart.

#### 4. Hot SQLite Backup Engine
* Use native SQLite backup API to snapshot `game.db` to `Backups/game_<timestamp>.db`.
* Automatic rotation scheme:
  * Keep hourly backups for 24 hours.
  * Keep daily backups for 7 days.
  * Keep weekly backups for 4 weeks.

---

### 7.3 Reference Implementations & Code Templates

Here are ready-to-use reference scripts in Python / PowerShell that you can drop directly into your project.

#### A. Resilient Mod Downloader & `modlist.txt` Generator (Python 3)
```python
import subprocess
import os
import glob
from pathlib import Path
import time

SERVER_DIR = Path(r"C:\ConanServer\DedicatedServerLauncher\ConanExilesDedicatedServer")
STEAMCMD_EXE = Path(r"C:\ConanServer\DedicatedServerLauncher\SteamCMD.exe")
WORKSHOP_APP_ID = "440900"

MOD_LIST = [
    "3722388367", # ModControlPanel
    "3803465771", # double_storage_space
    "3723073788", # Frequent_meteors
    "3722270581", # Spawn_98%_Women
    "3723975720", # River Boats (Large 3.8GB)
]

def download_single_mod(mod_id: str, max_retries: int = 5) -> bool:
    cmd = [
        str(STEAMCMD_EXE),
        "+force_install_dir", str(SERVER_DIR),
        "+login", "anonymous",
        "+workshop_download_item", WORKSHOP_APP_ID, mod_id, "validate",
        "+logoff", "+quit"
    ]
    
    for attempt in range(1, max_retries + 1):
        print(f"[*] Downloading mod {mod_id} (Attempt {attempt}/{max_retries})...")
        result = subprocess.run(cmd, capture_output=True, text=True)
        
        if result.returncode == 0 and "Success." in result.stdout:
            print(f"[+] Mod {mod_id} downloaded successfully.")
            return True
            
        print(f"[-] Mod {mod_id} attempt {attempt} failed. Retrying in 5 seconds...")
        time.sleep(5)
        
    print(f"[!] FAILED to download mod {mod_id} after {max_retries} attempts.")
    return False

def regenerate_modlist():
    workshop_content = SERVER_DIR / "steamapps" / "workshop" / "content" / WORKSHOP_APP_ID
    modlist_file = SERVER_DIR / "ConanSandbox" / "Mods" / "modlist.txt"
    modlist_file.parent.mkdir(parents=True, exist_ok=True)
    
    pak_paths = []
    for mod_id in MOD_LIST:
        mod_folder = workshop_content / mod_id
        pak_files = list(mod_folder.glob("*.pak"))
        if pak_files:
            pak_paths.append(str(pak_files[0].resolve()))
        else:
            print(f"[!] Warning: No .pak found for mod {mod_id}")
            
    with open(modlist_file, "w", encoding="utf-8") as f:
        for path in pak_paths:
            f.write(path + "\n")
            
    print(f"[+] Generated {modlist_file} with {len(pak_paths)} mods.")

if __name__ == "__main__":
    for mod in MOD_LIST:
        success = download_single_mod(mod)
        if not success:
            print(f"[!] Aborting: Could not download required mod {mod}")
            break
    regenerate_modlist()
```

#### B. Safe Hot SQLite Database Backup (Python 3)
```python
import sqlite3
import shutil
from datetime import datetime
from pathlib import Path

DB_PATH = Path(r"C:\ConanServer\DedicatedServerLauncher\ConanExilesDedicatedServer\ConanSandbox\Saved\game.db")
BACKUP_DIR = Path(r"C:\ConanServer\Backups")

def create_safe_hot_backup():
    BACKUP_DIR.mkdir(parents=True, exist_ok=True)
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    target_backup = BACKUP_DIR / f"game_{timestamp}.db"
    
    print(f"[*] Starting SQLite Online Backup: {DB_PATH} -> {target_backup}")
    
    # Open source database in read-only URI mode to prevent any locking issues
    src_conn = sqlite3.connect(f"file:{DB_PATH.resolve()}?mode=ro", uri=True)
    dst_conn = sqlite3.connect(str(target_backup.resolve()))
    
    with dst_conn:
        # Copies pages in chunks while the server is running without write interference
        src_conn.backup(dst_conn, pages=250, sleep=0.01)
        
    dst_conn.close()
    src_conn.close()
    print(f"[+] Backup completed safely: {target_backup} ({target_backup.stat().st_size:,} bytes)")

if __name__ == "__main__":
    create_safe_hot_backup()
```

#### C. Async Python RCON Client Implementation
```python
import socket
import struct
import time

class ValveRconClient:
    SERVERDATA_AUTH = 3
    SERVERDATA_AUTH_RESPONSE = 2
    SERVERDATA_EXECCOMMAND = 2
    SERVERDATA_RESPONSE_VALUE = 0

    def __init__(self, host: str, port: int, password: str):
        self.host = host
        self.port = port
        self.password = password
        self.sock = None
        self.request_id = 0

    def connect(self):
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.sock.settimeout(10.0)
        self.sock.connect((self.host, self.port))
        
        # Authenticate
        self._send_packet(self.SERVERDATA_AUTH, self.password)
        resp_id, resp_type, _ = self._read_packet()
        if resp_id == -1:
            raise ConnectionRefusedError("RCON Authentication Failed: Invalid Password")
        print("[+] RCON Authenticated Successfully")

    def _send_packet(self, packet_type: int, body: str):
        self.request_id += 1
        encoded_body = body.encode("ascii") + b"\x00\x00"
        packet_size = 4 + 4 + len(encoded_body)
        header = struct.pack("<iii", packet_size, self.request_id, packet_type)
        self.sock.sendall(header + encoded_body)

    def _read_packet(self):
        header_raw = self.sock.recv(12)
        if len(header_raw) < 12:
            raise ConnectionError("Incomplete RCON packet header")
        size, req_id, packet_type = struct.unpack("<iii", header_raw)
        remaining = size - 8
        body = b""
        while len(body) < remaining:
            chunk = self.sock.recv(remaining - len(body))
            if not chunk:
                break
            body += chunk
        return req_id, packet_type, body[:-2].decode("ascii", errors="replace")

    def execute(self, command: str) -> str:
        self._send_packet(self.SERVERDATA_EXECCOMMAND, command)
        _, _, response = self._read_packet()
        return response

    def close(self):
        if self.sock:
            self.sock.close()

if __name__ == "__main__":
    rcon = ValveRconClient("127.0.0.1", 25575, "YourRconPassword")
    rcon.connect()
    print("Response:", rcon.execute("broadcast Server test announcement!"))
    print("Response:", rcon.execute("save"))
    rcon.close()
```

---

## 8. Implementation Roadmap

Follow this step-by-step roadmap when developing your custom manager on your development PC:

```
┌─────────────────────────────────────────────────────────────┐
│ PHASE 1: Core Engine & SteamCMD Wrapper (CLI)               │
│ - Implement robust sequential mod downloader with retries   │
│ - Implement automatic modlist.txt generator                 │
│ - Implement clean server process launch & termination       │
└──────────────────────────────┬──────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────┐
│ PHASE 2: Protocol & Health Watchdog                         │
│ - Build RCON client (broadcast, save, clean shutdown)       │
│ - Build watchdog process loop (PID monitor + RCON ping)     │
│ - Implement SQLite Online Backup API routine                │
└──────────────────────────────┬──────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────┐
│ PHASE 3: Configuration & Web Dashboard API                  │
│ - Build FastAPI or Go HTTP REST / WebSocket server          │
│ - Parse and expose ServerSettings.ini options to UI         │
│ - Realtime console streaming via WebSocket                  │
└──────────────────────────────┬──────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────┐
│ PHASE 4: Frontend UI & Automation                           │
│ - Modern UI (Vue / React / Tailwind)                        │
│ - Workshop search & 1-click mod install                     │
│ - Automated cron restarts & Discord webhook alerts          │
└─────────────────────────────────────────────────────────────┘
```

---

## 9. Progress Log & Implementation Status

> **Last Updated:** September 21, 2026

### Completed Objectives:
1. **Isolated Local Server Host Application (`ConanServerManager.exe`)**:
   - Built standalone single-file binary using .NET 10 WPF (`win-x64`, self-contained executable).
   - Removed all hardcoded network share couplings. The application operates strictly in its local host directory (`./ConanExilesDedicatedServer`).
   - Automatically checks for `./SteamCMD.exe` (or `./DedicatedServerLauncher/SteamCMD.exe`) and downloads/extracts `steamcmd.zip` from Valve CDN if not found.

2. **Embedded HttpListener Web Server & REST API**:
   - Zero-dependency embedded `HttpListener` web server hosting live HTTP REST endpoints (`/api/status`, `/api/logs`, `/api/control/*`, `/api/control/rcon`) on port `8080` (or user configured port).
   - Enhanced `App.xaml.cs` with recursive `InnerException` logging to `crash.log` and `TaskScheduler` exception handling.
   - Provides full remote access for external clients (Desktop Client, Web Browsers, Mobile Apps).

3. **Mobile Phone Web Dashboard & PWA Console**:
   - Embedded HTML5/JS responsive web interface (`http://<server-ip>:8080`) optimized for Android phones, tablets, and desktop browsers.
   - Features real-time status badge (`RUNNING`, `STOPPED`, `UPDATING`), uptime timer, player count, active mods count, live log stream, RCON console, and quick touch action buttons (`Start`, `Stop`, `Restart`, `Backup`).

4. **Dual-Mode Desktop WPF App (Local Host & Remote Client)**:
   - Includes a mode toggle in the main window:
     - **Local Server Host Mode**: Directly manages local server files, SteamCMD, and process execution on the server host.
     - **Remote Client Mode**: Connects over network API (`http://<remote-server-ip>:8088`) to monitor logs, status, and execute remote server controls from an admin PC.
   - **Expanded Console & Editor Panel**: Re-proportioned layout using Grid row star height (`*`) so the log textboxes (`TxtServerLog`, `TxtErrorLog`, `TxtSteamCmdLog`, `TxtLauncherLog`, INI editors) dynamically stretch to fill the full window height. Tightened top configuration cards to eliminate unused vertical padding.

5. **SteamCMD Resilient Mod Downloading & Hot Backups**:
   - Sequential mod downloader with retry loops (up to 5 retries per mod) to eliminate the Funcom 5-minute concatenated timeout bug.
   - Pure C# Source RCON protocol client and SQLite Online Hot Backup (`Microsoft.Data.Sqlite`).

6. **1:1 Funcom 1.9.4 Layout Parity & Space Reorganization**:
   - **Compact 4-Column Card Grid**: Re-organized top controls into 4 tight columns with glassmorphic styling, eliminating wasted vertical space.
   - **Maximized Log Window Height**: Main multi-tab panel explicitly bound to `RowDefinition Height="*"` with `VerticalAlignment="Stretch"` textboxes, expanding log views to fill over 500px of vertical space.
   - **Network & Diagnostics Toolbar**: Added NIC selector, Public External IP detector, MAC address display, 1-click Connection String Clipboard Copy button (`Server Name`, `Direct Connect IP:Port`, `Password`), and Multi-Port TCP/UDP Accessibility Tester (`Game 7777`, `Raw 7778`, `Query 27015`, `RCON 25575`, `Web API 8088`).
   - **Shutdown Warning Sequence & Countdown Timers**: Implemented 3 customizable countdown warnings (10m, 5m, 2m) with RCON broadcast messages and fast-restart on 0 active players.

7. **Visual LED Port Status Indicators & Crash Diagnostic**:
   - **LED Status Indicators**: Replaced log output port accessibility reporting with 5 visual LED lights (`LedGamePort`, `LedRawPort`, `LedQueryPort`, `LedRconPort`, `LedWebPort`) placed directly next to each port label. Green (`#10B981`) indicates bound/accessible; Red (`#EF4444`) indicates not bound/closed.
   - **Crash Diagnosis & Fix**: Resolved XAML initialization order exception (`NullReferenceException` in `get_IsRemoteMode()`) by instantiating `_engine` before `InitializeComponent()`. Resolved socket unobserved task exception (`SocketException`) by switching to synchronous socket handle wait and kernel UDP/TCP listener enumeration (`IPGlobalProperties.GetActiveUdpListeners()`).

8. **Live SteamCMD Download Progress Bar & Speed/ETA Engine**:
   - **Real-Time Stdout Parsing**: Built regex stdout parser (`progress: X% (bytes / total)`) tracking download percentage, MB/s transfer speed, and estimated time remaining (ETA).
   - **Desktop UI & Web API Integration**: Added a live progress bar card (`PnlDownloadProgress`) in the WPF Desktop GUI and exposed real-time progress metrics (`downloadPercent`, `downloadSpeedMBs`, `downloadEtaString`) on the embedded Web API (`/api/status`) for phone dashboards.
   - **Instant Abort on Stop**: Updated `StopServerAsync()` so clicking **"■ Stop Server"** during an active update immediately terminates any running SteamCMD process tree.
   - **Zero Warnings / Zero Errors Build**: Resolved package vulnerability warnings (`NU1903`) by updating `SQLitePCLRaw.lib.e_sqlite3` to version `2.1.11` and configuring `<NoWarn>`. Built as a fully self-contained application (`--self-contained true`).

9. **Zero-Latency Stream Reader & Guaranteed Failsafe Shutdown**:
   - **Unbuffered Stdout Streaming (0-ms Latency)**: Replaced `.NET`'s newline-buffered `OutputDataReceived` with a character-by-character stream reader splitting on both `\r` and `\n`. Eliminates the 3–4 minute output refresh freeze when SteamCMD sends carriage-return progress lines.
   - **Verified 3-Step Server Process Shutdown**: Upgraded `StopServerAsync()` with a 3-tier shutdown protocol:
     1. Send RCON `broadcast`, `save`, and `exit`.
     2. Wait up to 10 seconds for `ServerProcess.WaitForExit(10000)`. If still running, force kill PID.
     3. Perform Windows Task Manager failsafe scan (`KillAllConanServerProcesses()`) targeting `ConanSandboxServer-Win64-Shipping` to guarantee zero orphan background processes remain running.

10. **Application-Level Remote Management & REST Endpoints**:
   - **Manager-Level Decoupling**: The embedded C# Web API server runs independently inside `ConanServerManager.exe` on port `8088`. Remote connections function at the manager application level, whether the game server process is running, stopped, or updating.
   - **Full Remote API Suite**: Added `/api/config` (GET/POST) and `/api/ini` (GET/POST) endpoints allowing remote clients, mobile apps, and web browsers to boot/stop/restart servers, edit `ServerSettings.ini`, `Engine.ini`, and `Game.ini`, manage workshop mods, and trigger SQLite hot backups completely offline.

11. **Admin Password UI, Auto-Start, Watchdog/Zombie Check & Clean Build**:
   - **INI-Free Admin Password UI & Instant Tab Refresh**: Admin Password input field added to Column 0 of the Desktop UI, synced to `ServerSettings.ini` (`AdminPassword=...`) and appended to `ConanSandboxServer-Win64-Shipping.exe` launch arguments (`-AdminPassword="..."`). Updated `SaveConfigFromUi()` and INI tab save handlers to trigger `LoadIniFilesToTabs()` immediately, ensuring the `ServerSettings.ini`, `Engine.ini`, and `Game.ini` tabs refresh in real time upon pressing **Save Configuration** without needing an application restart.
   - **Auto-Start on Launch (`AutoStartOnAppLaunch`)**: Checkbox setting added to Column 3 ("Branch & Process Scaling"). When checked, launching `ConanServerManager.exe` automatically triggers the update and server startup sequence.
   - **Background Watchdog & Zombie Deadlock Auto-Restart (`ZombieCheckEnabled`)**: Implemented a 30-second background watchdog loop (`StartWatchdogTimer()`). Monitors `ServerProcess.HasExited` for unexpected process crashes and issues periodic RCON heartbeat probes (`help`). If 3 consecutive 30-second RCON probes fail (90s deadlock/zombie freeze), the watchdog automatically force-kills the hung process and restarts the server.
   - **Zero Warnings / Zero Errors Build**: Built with **0 Errors and 0 Warnings** (`dotnet build` and `dotnet publish`). Published as a standalone self-contained release build (`ServerManager/`) and copied cleanly to project root (`F:\Projects\Conan Exiles Dedicated Server\`).

12. **Out-of-the-Box Server Deployment Package**:
   - Created clean deployment folder `ConanServerManager_DeployPackage` and standalone zip archive `ConanServerManager_DeployPackage.zip` (65.7 MB).
   - Contains all self-contained `.NET 10` runtime assemblies, `ConanServerManager.exe`, `SteamCMD.exe`, initial `manager_config.json`, batch launcher (`START_SERVER_MANAGER.bat`), and deployment instructions (`README_DEPLOYMENT.txt`). Excludes all heavy game data files, logs, and backups for clean server-to-server migration.

13. **Universal `TcpListener` Socket HTTP Server & Remote Connection LED Indicator**:
   - **Replaced `HttpListener` with Socket `TcpListener` (`0.0.0.0:8088`)**: Completely eliminated `HTTP Error 400. The request hostname is invalid.`. `TcpListener` binds to all IPv4 socket interfaces, bypassing Windows HTTP.sys URLACL strict hostname restrictions and non-Admin permission blocks. Accepts connections from any IP, hostname, or domain without configuration.
   - **Remote Connection LED & Diagnostic Indicator (`LedRemoteConnection` / `TxtRemoteStatusText`)**: Added a live connection status LED light and text indicator to Column 0. Displays green `ONLINE (CONNECTED to HostName)` when reachable and red `UNREACHABLE (Error / Timeout)` with explicit connection diagnostic feedback when disconnected.

14. **GitHub Auto-Updater & Semantic Versioning (v1.0.0 -> v1.0.3)**:
   - **Semantic Versioning**: Centralized in `<Version>1.0.3</Version>` in `ConanServerManager.csproj` and `ServerEngine.CurrentAppVersion`. Displayed prominently in the desktop header, log outputs, and web dashboard with an active `✨ v1.0.3` version badge.
   - **Zero-Downtime Process Adoption & Duplicate Prevention (`DetectAndAdoptRunningServerProcess`)**: Solved game server conflict during manager updates and restarts. When `ConanServerManager.exe` starts or updates, it scans for running `ConanSandboxServer-Win64-Shipping` or `ConanSandboxServer` processes. If found, it automatically adopts the active process, hooks monitoring, sets `ServerStatus` to `RUNNING`, and blocks duplicate launches in `RunFullUpdateAndStartAsync()` and `StartServerProcess()`. The game server remains online with 0 seconds of player downtime while the manager updates and re-attaches seamlessly.
   - **Continuous 4-Hour Background Check & Deferred Updates**: Added recurring 4-hour background timer (`_appUpdateTimer`) with initial 10s delay. If `AutoInstallAppUpdates` is ticked, downloads and installs updates headlessly with zero dialog prompts. Defers update if game server is actively updating mods (`UPDATING`).
   - **GitHub Releases REST API Integration**: Queries `https://api.github.com/repos/Nakrom75/ConanEnhancedServerManager/releases/latest` to parse tag versions, asset packages, release notes, and download links.
   - **Desktop Notification Banner & Action Controls**: Added `PnlAppUpdateBanner` with **📥 Download & Update Now**, dismiss button, and Action Bar button **🔄 Check App Updates**. Configurable checkboxes `ChkAutoCheckAppUpdates` and `ChkAutoInstallAppUpdates` control automatic checking and background installation.
   - **Self-Updating Trampoline (`update_helper.bat`) with Config Protection**: Solves Windows OS file locks on running `.exe` files. Downloads the update zip asset, extracts to `Updates/staged/`, strips any incoming `manager_config.json` to guarantee local server credentials/passwords are never overwritten, spawns `update_helper.bat` with PID monitoring, terminates the app, replaces binaries via `xcopy`, relaunches `ConanServerManager.exe`, and cleans up all temporary staged files.
   - **Remote & Web API Endpoints**: Added `/api/control/check-update` and `/api/control/apply-update` for headless remote updating over LAN/WAN.

15. **Steam Master Server Visibility & A2S_INFO Query Protocol (`SteamQueryHelper`)**:
   - **Standard Valve A2S_INFO Protocol**: Implemented non-blocking UDP client (`SteamQueryHelper.cs`) targeting `QueryPort` (default `27015`). Handles Steam challenge handshake tokens (`0x41`), parses server name, active map, live connected player count, maximum player capacity, and ping latency in milliseconds.
   - **Multi-State Visual Indicator (`BadgeSteamVisibility` & `LedSteamVisibility`)**:
     - `STEAM: OFFLINE` (Gray `#64748B`): Server process is stopped.
     - `STEAM: STARTING UP...` (Amber `#F59E0B`): Process is running, but game engine is compiling shaders, mounting mods, or initializing `game.db`.
     - `STEAM: ONLINE (X/Y Players)` (Bright Green `#10B981`): Game server has bound UDP 27015, registered with Steam Master Server, and is confirmed ready for player connections.
     - `STEAM: UNRESPONSIVE` (Red `#EF4444`): Process is alive for 4+ minutes without answering Steam queries (detects mod compile freezes, infinite loops, and database deadlocks).
   - **Web API & Remote Management**: Exposed via `/api/status` (`steamOnline`, `steamPlayers`, `steamMaxPlayers`, `steamPing`, `steamError`) and rendered in real-time in Remote Mode.

---

## 10. Official Funcom DedicatedServerLauncher Specifications & Reference Manual

> **Source:** Official Funcom Forums (`https://forums.funcom.com/t/conan-exiles-dedicated-server-launcher-official-version-1-9-4-beta-1-9-7/21699`)

### 10.1 Feature & Configuration Breakdown

#### 1. Core Deployment & SteamCMD Integration
- **Transparent SteamCMD Execution**: Automatically downloads, updates, and validates Conan Exiles server base (`AppID 443030`).
- **Mod Manager & Workshop Sync**: Automatically downloads and validates Steam Workshop mods (`AppID 440900`) specified by Workshop IDs (e.g., `3722388367`) or absolute `.pak` file paths.
- **Resilient Sequential Downloading**: Prevents SteamCMD chunk download timeouts on large mods.
- **Automatic `modlist.txt` Generation**: Writes absolute `.pak` file paths to `<ServerDir>\ConanSandbox\Mods\modlist.txt` in top-to-bottom load order.
- **Mod List INI Entry**: Stores mod IDs in `ServerSettings.ini` under `DedicatedServerLauncherModList=...`.

#### 2. Network Configuration & Multi-Homing
- **Port Mapping**:
  - `Game Client Port` (Default UDP `7777`) -> `Engine.ini` `[URL] Port=7777`
  - `Raw UDP Port` (Default UDP `7778`) -> Primary Game Port + 1
  - `Source Query Port` (Default UDP `27015`) -> `Engine.ini` `[OnlineSubsystemSteam] GameServerQueryPort=27015`
  - `RCON Port` (Default TCP `25575`) -> `ServerSettings.ini` `[ServerSettings] RconPort=25575`
  - `Web Page Port` (Default TCP `8088` / `80`) -> Web status dashboard port
- **Multi-Home IP Binding**:
  - `-MULTIHOME=<IP>` command line argument when `UseMultihome` is enabled.
  - Adapter IP selection list.
- **External IP & Port Accessibility Testing**:
  - Detects Public/External IP address.
  - Displays Network Adapter MAC/Hardware Address with copy-paste connection string builder.
  - Tests inbound TCP/UDP port accessibility to verify router NAT forwarding.

#### 3. Automatic Restarts, Watchdog & Warnings
- **Daily Restart Timer**: Configurable daily restart time (e.g., `06:00:00`).
- **Minimum Uptime Check**: Prevents premature restart cycles before minimum uptime (e.g., `02:00:00`) has elapsed.
- **Zombie Process Check**: Monitors server IO and CPU activity; automatically kills and restarts unresponsive / deadlocked server processes.
- **Shutdown Warning Sequence**:
  - Sends 3 customizable RCON broadcast messages at configurable countdown intervals (e.g. 10m, 5m, 2m).
  - Sends RCON `save` command 3 seconds prior to process exit.
  - Early restart: If 0 connected players are detected during the final countdown, server restarts immediately without waiting.

#### 4. Discord Webhook Integration
- **Discord Bot Notifications**: Sends Webhook JSON embeds for server lifecycle events:
  - Server Ready (`Server is up and running!`)
  - Server Manual Restart / Auto Restart
  - Server Shutdown
  - Timestamp formatting toggle.

#### 5. Backup & Automation
- **SQLite Database Hot Backup**: Safely backups `game.db` to `./Backups/game_<timestamp>.db`.
- **Retention Trim Limit**: Automatically deletes database backups older than `N` days (default `7` days).
- **Pre / Post Script Execution**:
  - Option to execute custom batch scripts (`Run .BAT on Startup`, `Run .BAT before Backups`, `Run .BAT after Backups`).

#### 6. Performance & CPU Affinity
- **Process Priority Class**: `Unmanaged`, `Normal Priority`, `Above Normal Priority`, `High Priority`.
- **CPU Thread Affinity Grid**: 64-bit affinity mask (`0` to `63`) mapping process threads to dedicated CPU cores.
- **`-useallavailablecores`**: UE4 command-line argument for multi-core scaling.


#### 7. issues found (added by Nakrom) - RESOLVED in v1.0.5

1. **Remote client connection does not save the address and defaults back to localhost on application restart or startup.**
   - *Status: Resolved in v1.0.5.* `RemoteServerUrl` and `IsRemoteClientMode` are now saved to `manager_config.json`. On application launch or restart, `LoadUiFromConfig()` automatically restores the remote server URL and activates Remote Client mode.
2. **Information of the various fields do not get populated with the remote servers information.**
   - *Status: Resolved in v1.0.5.* Implemented full two-way synchronization via `FetchAndPopulateRemoteConfigAsync()`. When connecting to a remote server, the application automatically pulls and populates all fields (Server Name, Passwords, Ports, Max Players, Tick Rate, Region, BattlEye, VAC, Restart Timers, Discord, Mods) from `GET /api/config`, and fetches the contents of `ServerSettings.ini`, `Engine.ini`, and `Game.ini` from `GET /api/ini`. Clicking **Save Configuration** or any of the INI tab save buttons while in Remote Mode cleanly posts the updates back to the remote server over `POST /api/config` and `POST /api/ini`.
3. **The STEAM: badge does not update or refresh correctly to reflect the actual server's state.**
   - *Status: Resolved in v1.0.5.* Fixed the race condition in `MainWindow.xaml.cs` where `UpdateStatusUi()` was unconditionally overwriting the remote server's Steam status with the local client machine's offline status. In addition, `ServerEngine.StartSteamQueryTimer()` now automatically targets `MultihomeIp` whenever multihome is enabled, ensuring the A2S_INFO query always hits the active network adapter.

---

### 16. Remote Client Persistence, Full Remote Sync & Steam Visibility Fixes (v1.0.4)
- **Settings Persistence**: Added `RemoteServerUrl` and `IsRemoteClientMode` properties to `ManagerConfig`. Whenever a remote connection is initiated or mode is toggled, these values are written to `manager_config.json`.
- **Full Remote GUI Population**:
  - `FetchAndPopulateRemoteConfigAsync(baseUrl)` queries `GET /api/config` and `GET /api/ini?file=...` upon initial connection or clicking **Connect**.
  - All form controls, checkboxes, comboboxes, and `LstMods` are populated with the remote server's actual running parameters.
  - All 3 INI editors (`ServerSettings.ini`, `Engine.ini`, `Game.ini`) are populated with the remote server's INI files.
- **Two-Way Remote Management**:
  - Clicking **Save Configuration** in Remote Mode posts the updated configuration to `POST /api/config` on the remote host, where `_engine.UpdateConfig()` persists the settings and syncs the INIs.
  - Clicking **Save ServerSettings.ini**, **Save Engine.ini**, or **Save Game.ini** in Remote Mode posts the editor text to `POST /api/ini`, updates the remote file, and triggers remote INI re-import.
- **Steam Badge Overwrite & Multihome Fix**:
  - Guarded local Steam status update in `UpdateStatusUi` with `if (!IsRemoteMode)`. In Remote Mode, only the remote server's query response controls the badge.
  - In `ServerEngine.cs`, `StartSteamQueryTimer` queries `MultihomeIp` when `UseMultihome` is enabled, resolving UDP packet drops on multi-NIC setups.
  - Remote uptime (`uptimeSeconds`) is parsed in `PollRemoteServerAsync` to correctly distinguish between active server startup and unresponsive hangs.

---

### 17. Hotfix: XAML Startup Lifecycle & Initialization Guards (v1.0.5)
- **Startup Crash Fix**: Fixed a `NullReferenceException` during `InitializeComponent()` caused by `RadMode_Checked` executing prematurely when the XAML parser initialized `IsChecked="True"` on `RadLocalMode` before subsequent UI controls were instantiated.
- **Lifecycle Guards**: Added `_isInitialized` boolean flag to `MainWindow.xaml.cs` ensuring event handlers and UI population routines are ignored until the visual tree is fully constructed.
- **Verification**: Verified launch and stability with zero errors and zero warnings.

---

### 18. Application Icon Integration & Visual Polish (v1.0.6)
- **Lossless Icon Extraction**: Extracted the authentic multi-resolution Conan Exiles icon group (9 frames ranging from 16x16 up to 256x256) directly from the PE resource tables of `DedicatedServerLauncher1904.exe`.
- **Executable Icon**: Configured `<ApplicationIcon>app.ico</ApplicationIcon>` in `src/ConanServerManager.csproj` so the Windows binary `.exe` displays the Conan icon in Windows Explorer, taskbar, desktop shortcuts, and Alt-Tab switcher.
- **WPF Window Icon & UI Branding**:
  - Embedded `app.ico` and `app.png` as assembly resources.
  - Configured `Icon="app.ico"` on `<Window>` in `MainWindow.xaml` for native title bar icon and taskbar grouping.
  - Added Conan brand badge icon to the application title bar in `MainWindow.xaml`.
- **Version Bump**: Bumped to version `v1.0.6` across project files, `ServerEngine.CurrentAppVersion`, and UI badges.

---

### 19. Permanent Players Sidebar & Web Interface Live Players (v1.0.7)
- **Permanent Left-Side Players Panel**:
  - Moved the connected players display out of the tab control at the bottom into a dedicated, permanently visible left sidebar (Row 3, Column 0) with a draggable `GridSplitter`.
  - Header displays live player count badge (`TxtPlayersCountBadge`: e.g. `2 / 40`).
  - Dark-themed `ListBox` rendering character names, connection duration, score, and ping badges with empty state placeholder when no players are connected.
  - Interactive player management: 1-click **Refresh** and **Kick** actions via RCON.
- **A2S_PLAYER Query & Multi-Tier Player Discovery**:
  - Implemented Valve UDP `A2S_PLAYER` protocol (`0x55` request with challenge handshake) in `SteamQueryHelper.cs`.
  - Added fallback discovery via RCON `listplayers` and session tracking.
  - Automatic placeholder synthesis when query count is reported by Steam Master Server.
- **Web Console Enhancements (`http://<ip>:8088`)**:
  - Added visible version badge (`v1.0.7`) to the web interface header and browser title.
  - Added **Players Online** metric card (`valPlayers`: `X / Y`) to the top statistics grid.
  - Added dedicated **Connected Players** card displaying live connected player rows (name, duration, score, ping) or empty state.
  - Exposed `/api/players` endpoint and added `players` array to `/api/status`.
- **Version Bump**: Centralized version `1.0.7` across project configurations, UI badges, and deployment packages.

### 20. Remote Server Dropdown, Network & VM Discovery, and Custom Server Selection (v1.0.8)
- **Remote Server Dropdown Selection (`CmbRemoteServers`)**:
  - Added a dedicated server selection dropdown to the Connection Mode card, providing quick 1-click access to target server hosts without needing to manually copy/paste or remember URLs.
  - Dropdown automatically categorizes target servers:
    1. `🖥️ Local Host (http://127.0.0.1:8088)`: Default local server host.
    2. `🌐 [LAN] ...` / `🌐 [VM] ...`: Active managers dynamically discovered across the local network and virtual machine subnets.
    3. `⭐ Saved Server (http://...)`: Historically saved servers from previous successful connections (`RecentRemoteServers`).
    4. `✏️ Custom Server (Enter IP / URL below)`: Allows freeform entry of custom VM IPs (e.g. VirtualBox host-only `192.168.56.x`, VMware `172.16.x.x`, Hyper-V, or isolated subnets). Selecting Custom automatically focuses and highlights the URL input field for immediate editing.
- **LAN & VM Server Discovery Engine (`DiscoveryHelper.cs` & UDP 8089)**:
  - Implemented lightweight, non-blocking UDP broadcast discovery protocol on port `8089` using `ReuseAddress` socket options to prevent port collisions.
  - Broadcasts discovery probes across both global `255.255.255.255` and all active physical and virtual network adapter subnet masks (e.g., VirtualBox, VMware, Hyper-V).
  - WebServer background discovery listener answers with server name, web port, and game port.
  - Initial HTTP probing detects local instances running on standard ports.
  - Added interactive **"🔍 Scan"** button (`BtnDiscoverServers`) in the UI that scans on demand with visual loading feedback (`⏳ Scanning...`).
- **Auto-Persistence for Custom & VM Servers (`RecentRemoteServers`)**:
  - Whenever a remote client successfully connects to a custom server (whether in a VM, across subnets, or over VPN), the URL is automatically added to `RecentRemoteServers` in `manager_config.json`.
  - Saved servers persist across restarts and are automatically populated into the dropdown with a star badge (`⭐`).
- **Two-Way Dynamic Selection**:
  - Selecting any discovered or saved server in the dropdown instantly fills `TxtRemoteUrl` and, if already in Remote Client mode, initiates immediate polling and synchronization.
  - Fully compatible with `GET /api/config` and two-way remote configuration editing.
- **Version Bump**: Centralized version `1.0.8` across project configurations, UI badges, `ServerEngine.CurrentAppVersion`, and deployment packages.

### 21. Android Mobile Remote Client App (`.apk`), Steam Workshop Live Mod Browser & Remote Management, and In-App Auto-Updates (v1.1.0)
- **Major Feature Milestone (Version 1.1.0)**:
  - Upgraded semantic version to `v1.1.0` reflecting the major expansion into cross-platform mobile server administration and live Steam Workshop integration.
- **Standalone Android APK (`ConanServerManager-v1.1.0.apk`)**:
  - Created a dedicated, standalone Android client application (`android/`) targeting Android 14 (API 34) with backward compatibility to Android 7.0 (API 24).
  - Built with Gradle 8.5, Android Gradle Plugin 8.2.2, and OpenJDK 17.
  - Packaged and pre-signed with APK Signature Scheme v2 via `android/conan-release.keystore` (4.62 MB release APK).
  - Designed for on-the-road server management over cellular networks, WAN IPs, Dynamic DNS (DDNS), VPNs (Tailscale, WireGuard), or local home WiFi.
  - Native Android app features:
    - Custom app launcher icons generated across all mipmap densities (mdpi, hdpi, xhdpi, xxhdpi, xxxhdpi) matching the authentic Conan icon.
    - Android JavaScript Bridge (`AndroidBridge` in `MainActivity.java`) exposing native toast notifications, haptic vibrations, persistent `SharedPreferences` server storage, and automated APK downloads and installs.
    - Full cleartext traffic and network security configuration (`network_security_config.xml`) allowing seamless connections to non-SSL local IP addresses and home DDNS endpoints.

- **Steam Workshop Mod Search Engine & Remote Installer**:
  - Implemented `SteamWorkshopHelper.cs` providing real-time queries against Steam Community Workshop for Conan Exiles (`appid=440900`):
    - Web scraping parser that queries `https://steamcommunity.com/workshop/browse/?appid=440900&searchtext=<query>` to extract mod IDs, titles, and preview thumbnail images directly from embedded JSON objects without requiring an API key.
    - Valve Remote Storage API integration via `https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/` to retrieve mod metadata, titles, subscriptions, file sizes, and descriptions.
  - Server REST API Endpoints added to `WebServer.cs`:
    - `GET /api/workshop/search?query=...`: Searches Steam Workshop and returns mod results, highlighting whether each mod is already installed on the server.
    - `GET /api/workshop/details?id=...`: Fetches detailed metadata for a specific Workshop mod.
    - `GET /api/mods`: Returns list of currently active mods with titles, IDs, and positions.
    - `POST /api/mods/add`: Appends a new mod ID to server configuration, regenerates `modlist.txt` in server root, and updates `ServerSettings.ini`.
    - `POST /api/mods/remove`: Removes a mod ID, rewrites `modlist.txt`, and updates configuration.
    - `POST /api/mods/reorder`: Persists reordered mod load sequences.

- **Remote Server Configuration & Identity Control**:
  - Fully integrated remote settings editor allowing administrators to rename the server, update server passwords, update admin passwords, adjust max players, and set server tick rates remotely from an Android device or web browser.
  - Changes are validated, saved to `manager_config.json`, and written directly to `DefaultServerSettings.ini` / `ServerSettings.ini`.

- **Direct APK Distribution & In-App Auto-Update System**:
  - Embedded `WebServer.cs` serves the compiled Android APK directly at `GET /conan.apk` and `GET /api/download/apk` with proper `application/vnd.android.package-archive` MIME type and `Content-Disposition: attachment; filename="ConanServerManager-v1.1.0.apk"`.
  - Android app includes built-in update checker that compares the running app version with the latest GitHub release.
  - 1-tap in-app update: `MainActivity.java` initiates Android `DownloadManager` to fetch the new release `.apk`, stores it in the app's external files directory, and launches Android's native package installer via `FileProvider` (`com.conan.servermanager.fileprovider`) and `Intent.ACTION_VIEW` (`FLAG_GRANT_READ_URI_PERMISSION`).

- **Mobile Client UI & Capabilities**:
  - Intuitive handheld UI optimized for smartphones:
    - **Server Switcher**: Quick connection to local host, saved servers, or custom remote IPs/DDNS with port and status badges.
    - **📊 Server Dashboard**: Real-time status badge (Stopped, Starting, Running, Updating), Steam master server visibility indicator with ping, uptime counter, and player count.
    - **🎮 Server Controls**: 1-tap Start Server, Stop Server, Restart Server, and Hot Backup creation.
    - **🧩 Steam Workshop Browser**: Search bar, instant mod thumbnail cards, and 1-tap **"➕ Add to Server"** button.
    - **📦 Active Mods Manager**: View current load order, delete mods, and trigger server refreshes.
    - **⚙️ Settings Editor**: Modify server name and core rules on the fly.
    - **👥 Online Players**: Live list of connected characters, connection duration, score, and 1-tap RCON kick.
    - **💻 Live Console & RCON**: Real-time log monitoring with custom RCON command terminal.
    - **✨ Update Checker**: Status badge indicating whether the app is up to date, with 1-tap APK update button.

- **Deployment Packages Updated**:
  - Packaged and verified `ConanServerManager_v1.1.0.zip` and `ConanServerManager_DeployPackage.zip` containing the updated Windows server manager binaries and the pre-signed `ConanServerManager-v1.1.0.apk`.

### 22. Full-Screen Mobile Server Selection & Unified Versioning (v1.1.1)
- **Version Bump to `v1.1.1`**:
  - Incremented version to `v1.1.1` across Windows Server Manager, Embedded Web Console, and Android Companion App.
- **Full-Screen Mobile Server Selection & Management Window**:
  - Replaced the cramped 80vh bottom sheet modal in the Android app with a dedicated full-screen interface (`.modal-fullscreen`: `100vw x 100vh`, fixed viewport).
  - Designed for smooth touchscreen navigation that adapts dynamically when the virtual keyboard pops up without obscuring buttons or content.
  - Safe area inset support (`padding-top: env(safe-area-inset-top)` / `padding-bottom: env(safe-area-inset-bottom)`) for modern notch and gesture displays.
  - Added dedicated navigation bar with `← Back` and top-right `✕` buttons, fully integrated with Android's physical and gesture back button.
  - **Currently Active Server Banner**: Displays active server name, URL, and live `ONLINE` / `OFFLINE` status.
  - **Quick-Fill Shortcut Chips**: 1-tap helper pills for `🏠 Localhost`, `➕ :8088`, `🌐 http://`, `📶 192.168.1.`, and `✕ Clear`.
  - **Server Nicknames**: Added support for naming connections (e.g., *"Home Server"*, *"Living Room PC"*, *"Cloud VM"*).
  - **Saved Servers Manager**: Spacious cards displaying server nicknames, endpoints, `ACTIVE` indicator, 1-tap `▶ Connect`, and `🗑️ Delete`.
  - **Local Network (LAN) Discovery**: Added on-demand subnet scanning button (`🔍 Scan LAN`) that automatically detects active Conan Dedicated Server Managers on local subnets.
- **Single Source of Truth (`version.txt`)**:
  - Root [`version.txt`](file:///F:/Projects/Conan%20Exiles%20Dedicated%20Server/version.txt) serves as the canonical source of truth for all projects.
  - [`src/ConanServerManager.csproj`](file:///F:/Projects/Conan%20Exiles%20Dedicated%20Server/src/ConanServerManager.csproj) evaluates `version.txt` dynamically at build time via MSBuild property functions.
  - [`src/ServerEngine.cs`](file:///F:/Projects/Conan%20Exiles%20Dedicated%20Server/src/ServerEngine.cs) extracts assembly informational version dynamically at runtime.
  - [`src/MainWindow.xaml.cs`](file:///F:/Projects/Conan%20Exiles%20Dedicated%20Server/src/MainWindow.xaml.cs) sets window title and version badge at launch.
  - [`src/WebServer.cs`](file:///F:/Projects/Conan%20Exiles%20Dedicated%20Server/src/WebServer.cs) injects the current version into the web console HTML and serves `ConanServerManager-v{version}.apk`.
  - [`android/app/build.gradle`](file:///F:/Projects/Conan%20Exiles%20Dedicated%20Server/android/app/build.gradle) reads `version.txt`, sets `versionName`, computes `versionCode`, and outputs `ConanServerManager-v${appVerName}.apk`.
  - [`MainActivity.java`](file:///F:/Projects/Conan%20Exiles%20Dedicated%20Server/android/app/src/main/java/com/conan/servermanager/MainActivity.java) exposes native `getAppVersion()` via `PackageManager`.
- **Release Packages Updated**:
  - **Android APK**: [`ConanServerManager-v1.1.1.apk`](file:///F:/Projects/Conan%20Exiles%20Dedicated%20Server/ConanServerManager-v1.1.1.apk) (4.41 MB).
  - **Windows Release Zip**: [`ConanServerManager_v1.1.1.zip`](file:///F:/Projects/Conan%20Exiles%20Dedicated%20Server/ConanServerManager_v1.1.1.zip).
  - **Deployment Package Zip**: [`ConanServerManager_DeployPackage.zip`](file:///F:/Projects/Conan%20Exiles%20Dedicated%20Server/ConanServerManager_DeployPackage.zip).

---

### 23. Windows In-App Self-Update Engine & Trampoline Orchestration (v1.1.2 - v1.1.4)
- **Automated Update Polling**:
  - Implemented `CheckForAppUpdateAsync()` in [`src/ServerEngine.cs`](file:///F:/Projects/Conan%20Exiles%20Dedicated%20Server/src/ServerEngine.cs) executing asynchronous GitHub Releases API calls to `https://api.github.com/repos/Nakrom75/ConanEnhancedServerManager/releases/latest`.
  - Parses semver release tags (e.g. `v1.1.4`) and matches against `ServerEngine.CurrentAppVersion`.
  - Configurable update options in `manager_config.json`: `AutoCheckAppUpdates` (periodic hourly check) and `AutoInstallAppUpdates`.
- **Streaming Package Downloader**:
  - `DownloadAndApplyAppUpdateAsync(AppUpdateInfo)` fetches release asset ZIPs with streaming `HttpClient` and invokes real-time percentage progress delegates.
- **Trampoline Process Architecture (`update_helper.bat`)**:
  - Windows file locks prevent overwriting running executables and loaded assemblies (`ConanServerManager.exe`, `System.Private.CoreLib.dll`).
  - Implemented an out-of-process batch trampoline (`update_helper.bat`):
    1. Receives target install path and caller process PID as arguments.
    2. Polls `tasklist /fi "PID eq <PID>"` in a non-blocking loop until the manager process terminates cleanly.
    3. Executes `xcopy "%~dp0Updates\staged\*" "<TARGET>\" /E /Y /I /Q`.
    4. Automatically launches the updated `ConanServerManager.exe`.
    5. Cleans up temporary staging artifacts and self-deletes via `rd /s /q Updates` and `(goto) 2>nul & del "%~f0"`.
- **WPF UI Update Banner**:
  - Added modern animated update notification bar in [`src/MainWindow.xaml`](file:///F:/Projects/Conan%20Exiles%20Dedicated%20Server/src/MainWindow.xaml) featuring update notes, download progress bar, and 1-click **"⚡ Update & Restart"** action.

---

### 24. SteamCMD Automated Deployment & Error Resilience (v1.1.5)
- **Problem & Official Launcher Bottleneck**:
  - If `SteamCMD.exe` was missing, corrupt, or running in an unprivileged subdirectory, server startup stalled indefinitely without descriptive diagnostics.
- **Valve CDN Direct Bootstrapping**:
  - In [`src/ServerEngine.cs`](file:///F:/Projects/Conan%20Exiles%20Dedicated%20Server/src/ServerEngine.cs), implemented `EnsureSteamCmdDownloadedAsync()`: downloads `https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip` directly to disk and unpacks `SteamCMD.exe`.
- **Multi-Path Discovery Chain**:
  - Implemented `FindSteamCmdExe()` scanning ordered candidate directories:
    1. Local app working directory (`AppWorkingDir\SteamCMD.exe`)
    2. Legacy launcher directory (`AppWorkingDir\DedicatedServerLauncher\SteamCMD.exe`)
    3. Server root directory (`ServerRootDir\SteamCMD.exe`)
    4. Application base directory (`BaseDir\SteamCMD.exe`)
- **Process Orchestration & Exit Code Trapping**:
  - Traps and parses SteamCMD standard error, standard output, and process return codes (0 = Success, 7 = Fatal Error, 8 = Out of Memory).
  - Routes real-time stdout streams to both `OnSteamCmdLog` and main UI diagnostics tab.

---

### 25. Steam Workshop Mod Title Resolution & Local Cache (v1.1.6)
- **Problem**:
  - Dedicated server configuration and Unreal Engine `modlist.txt` identify mods exclusively by Steam PublishedFileId numbers (e.g., `3722388367`, `3803465771`).
  - Server administrators were forced to cross-reference web URLs or memorize 10-digit IDs.
- **Valve Remote Storage Batch API**:
  - Implemented [`src/SteamWorkshopHelper.cs`](file:///F:/Projects/Conan%20Exiles%20Dedicated%20Server/src/SteamWorkshopHelper.cs) utilizing Valve's official API:
    `POST https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/`
  - Encodes multi-item form payloads (`itemcount=N`, `publishedfileids[0]=...`) to resolve up to 100 mods in a single HTTP request.
- **Resilient Fallback Web Scraping**:
  - When Steam Web API endpoints are rate-limited or unauthenticated, falls back to parsing embedded OpenGraph metadata and JSON structures on `https://steamcommunity.com/sharedfiles/filedetails/?id=<ID>`.
- **Thread-Safe Local Metadata Cache (`workshop_cache.json`)**:
  - Persists resolved mod titles, author names, preview URLs, and file sizes to `workshop_cache.json`.
  - Employs cache-aside pattern: instant offline retrieval upon application launch without stalling the UI.
- **Two-Line Rich Mod Presentation Model (`ModDisplayItem`)**:
  - WPF and Android Web UI render items with Mod Title in bold primary text and Mod ID in secondary muted styling.

---

### 26. Safe Mod Webpage Navigation via Context Menu & Long-Press (v1.1.7)
- **Problem**:
  - Admins needed quick access to mod documentation, update histories, and Steam Workshop pages without risking accidental browser launches while clicking or scrolling.
- **Desktop WPF Context Menu**:
  - Implemented contextual right-click menu on mod list items:
    - **"🌐 Open Mod Page on Steam Workshop"**: Launches default web browser directly to `https://steamcommunity.com/sharedfiles/filedetails/?id={modId}` via `ProcessStartInfo { UseShellExecute = true }`.
    - **"📋 Copy Mod ID"**: Copies raw numeric ID to the Windows clipboard.
- **Mobile Android Long-Press Gesture**:
  - In `android/app/src/main/assets/app.js`, implemented deliberate long-press touch handler with a 500ms duration threshold:
    - Normal clicks/taps select the item for reordering or deletion without opening links.
    - Sustained long press triggers a modal popup displaying mod thumbnail, title, ID, and explicit buttons:
      - *"🌐 Open Steam Workshop Page"* (delegates to external Android browser).
      - *"📋 Copy Mod ID"* (copies to Android clipboard with haptic feedback).
      - *"✕ Cancel"*.

---

### 27. XAML Style Hierarchy & Startup Crash Elimination (v1.1.8)
- **Startup Crash Root Cause**:
  - Immediately following mod context menu integration, applications experienced `XamlParseException` / `InvalidOperationException` crashes on startup (`crash.log`).
  - In WPF, declaring a `ContextMenu` directly inside `ListView.ItemContainerStyle` `Style.Setters` causes parser conflicts with the element's visual parent when using custom `ItemTemplate` hierarchies.
- **Architectural Fix**:
  - Declared `ContextMenu` as an independent named resource within `<Window.Resources>` or directly on the container.
  - Attached event triggers programmatically in code-behind [`src/MainWindow.xaml.cs`](file:///F:/Projects/Conan%20Exiles%20Dedicated%20Server/src/MainWindow.xaml.cs).
  - Integrated global exception handling in `App.xaml.cs` trapping `DispatcherUnhandledException`, writing comprehensive diagnostic logs, inner exceptions, and stack traces to `crash.log`.

---

### 28. Steam Master Server Announcement, FLS Registration & RCON Mapping Architecture (v1.1.9)
- **Problem & Root Cause**:
  - Dedicated server processes started successfully, but remained invisible in the in-game server browser, failed to register with Steam Master Server, output red diagnostic errors, and dropped RCON connections.
- **Unreal Engine 4 & Funcom Live Services (FLS) Multi-INI Hierarchy**:
  - Unlike standalone engines where settings reside in a single file, Conan Exiles strictly partitions server parameters across three separate INI files in `Saved\Config\WindowsServer\`:
    1. **`ServerSettings.ini` (`[ServerSettings]`)**:
       - Primary gameplay mechanics: Harvest multipliers, XP rates, PvP schedules, NPC damage, nudity, and mod lists (`DedicatedServerLauncherModList`).
       - Region code (`serverRegion`).
    2. **`Engine.ini` (`[OnlineSubsystem]`, `[OnlineSubsystemSteam]`, `[URL]`)**:
       - Master server identity: `[OnlineSubsystem] ServerName` and `ServerPassword`.
       - Port bindings: `[URL] Port=7777` (game traffic).
       - Steam Master Server Query Port: `[OnlineSubsystemSteam] GameServerQueryPort=27015`. If omitted, the engine fails to announce to Valve's Master Server list.
       - Network tick rate: `[/Script/OnlineSubsystemUtils.IpNetDriver] NetServerMaxTickRate=30`.
       - Multihome IP bindings (`DedicatedServerLauncherMultihomeEnabled`, `DedicatedServerLauncherMultihomeIP`).
    3. **`Game.ini` (`[/Script/Engine.GameSession]`, `[RconPlugin]`)**:
       - Max player limits: `[/Script/Engine.GameSession] MaxPlayers=40`. (Writing `MaxPlayers` to `ServerSettings.ini` is ignored by Unreal Engine!).
       - RCON configuration: `[RconPlugin] RconEnabled=True`, `RconPort=25575`, `RconPassword=...`. (Writing RCON settings to `ServerSettings.ini` causes RCON to remain disabled!).
- **Multi-File INI Synchronization Engine**:
  - Upgraded `SyncIniSettings()` in [`src/ServerEngine.cs`](file:///F:/Projects/Conan%20Exiles%20Dedicated%20Server/src/ServerEngine.cs) to synchronize each parameter to its authentic Unreal Engine INI destination.
  - Upgraded `AutoDetectAndImportIniSettings()` to inspect all three files on launch.

---

### 29. Self-Contained Deployment & .NET Runtime Independence (v1.1.10)
- **Problem**:
  - Launching the server manager on clean host machines or Windows Server VMs triggered a Windows modal error: *"You must install .NET Desktop Runtime to run this application"*, even when partial or newer .NET runtimes existed.
- **Self-Contained Deployment (`win-x64`)**:
  - Added `<RollForward>Major</RollForward>` to [`src/ConanServerManager.csproj`](file:///F:/Projects/Conan%20Exiles%20Dedicated%20Server/src/ConanServerManager.csproj).
  - Configured publication pipeline for standalone execution:
    `dotnet publish src/ConanServerManager.csproj -c Release -r win-x64 --self-contained true -o ServerManager/`
  - Embeds the .NET CoreCLR, WPF presentation frameworks, and native graphics libraries (`wpfgfx_cor3.dll`, `vcruntime140_cor3.dll`).
  - Zero runtime dependencies: the application launches out-of-the-box on clean installations of Windows 10, 11, and Windows Server 2016/2019/2022.

---

### 30. Zero-Data-Loss Architecture: Configuration Ingestion, Packaging Isolation & INI Integrity (v1.1.11)
- **Problem**:
  - Users reported that updates repeatedly wiped out `ServerSettings.ini`, resetting it to a minimal 8–9 line skeleton and removing hundreds of custom gameplay multipliers.
  - Server names and passwords were continually reset to generic test strings ("Test Server v1.1.5", "Antigravity Conan Server", "AdminPassword456").
- **Root Cause Analysis**:
  1. *Packaging Contamination*: The build routine executed `Compress-Archive -Path "ServerManager\*"` without filtering. Local runtime files created during development testing (`manager_config.json` containing test credentials, and a 418-byte dummy `ServerSettings.ini` inside `ServerManager\ConanExilesDedicatedServer`) were packaged directly into `ConanServerManager_v1.1.X.zip`. Extracting an update unzipped these files over the user's real configs.
  2. *Class Defaults*: `ManagerConfig` initialized properties with dummy strings (`"Antigravity Conan Server"`, `"SuperSecretAdminPassword123!"`).
  3. *Unchecked Skeleton Generation*: When `UpdateIniKey()` could not find `ServerSettings.ini`, it created a brand-new file containing only the 8 keys managed by the launcher, stripping the 220+ default gameplay settings.
- **Architectural Solution & Safeguards**:
  - **Zero-Config Packaging Isolation**:
    - Build process strictly purges `manager_config.json`, `workshop_cache.json`, `ConanExilesDedicatedServer/`, and all `*.ini` files from `ServerManager/` and `ConanServerManager_DeployPackage/` prior to archiving.
    - Only [`manager_config.example.json`](file:///F:/Projects/Conan%20Exiles%20Dedicated%20Server/manager_config.example.json) is distributed in release archives.
  - **Neutral Defaults & Legacy Placeholder Purging**:
    - Property defaults in `ManagerConfig` initialized to empty strings (`""`).
    - Implemented `IsPlaceholderServerName()` and `IsPlaceholderPassword()` to automatically detect, sanitize, and discard legacy test strings.
  - **Server INIs as the Source of Truth**:
    - When `ServerSettings.ini` or `Engine.ini` exist, their non-empty values are treated as the definitive source of truth.
    - `AutoDetectAndImportIniSettings()` populates `Config` before any sync operation. Startup never overwrites existing non-empty values with blank or placeholder strings.
  - **Skeleton File Prohibition**:
    - `UpdateIniKey()` explicitly checks `Path.GetFileName(filePath).Equals("ServerSettings.ini")`. If the file does not exist, it aborts write operations, ensuring Conan Sandbox Server can generate its full 220+ default settings on first boot without premature truncation.
  - **Updater Trampoline Scrubbing**:
    - `DownloadAndApplyAppUpdateAsync()` aggressively scrubs any staged `manager_config.json`, `ConanExilesDedicatedServer`, `workshop_cache.json`, `Backups`, or `*.ini` files from `stagedDir` before spawning the updater trampoline script.

---

## 31. Instant Launch Controls, Hot Backup Multi-DB, Custom Paths & Auto-Restart Scheduler (v1.1.12)

### 1. Instant Launch & Restart Controls (Bypassing SteamCMD Downloads)
- **Problem**:
  - Previously, all Start and Restart actions called `RunFullUpdateAndStartAsync()`, forcing an unskippable SteamCMD check/download for both the server base files and every registered workshop mod.
  - On servers with large modlists (e.g. 18+ mods) or during rapid configuration iterations, this caused multi-minute launch delays even when game files were already up-to-date.
- **Architectural Solution**:
  - Implemented decoupled execution pathways:
    - `RunFullUpdateAndStartAsync()`: Validates and downloads server files and Steam Workshop mods via SteamCMD, then launches the server.
    - `StartServerWithoutUpdateAsync()`: Immediately validates executable presence, synchronizes INIs (`SyncIniSettings()`), generates `modlist.txt`, and spawns `ConanSandboxServer-Win64-Shipping.exe` without invoking SteamCMD.
    - `RestartServerWithoutUpdateAsync()`: Gracefully halts active server via RCON and process termination, pauses 2.5s, then immediately launches via `StartServerWithoutUpdateAsync()`.
    - `RestartServerAsync()`: Gracefully halts active server, then invokes update mode according to `Config.AutoUpdateRestartMode`.
  - Added dedicated UI controls across all platforms:
    - **Windows Desktop WPF**: Action buttons bar updated to a responsive `WrapPanel` featuring:
      - `🚀 Update & Start Server` (`BtnStart`)
      - `▶ Start (No Update)` (`BtnStartNoUpdate`)
      - `🔄 Update & Restart` (`BtnRestart`)
      - `⚡ Restart (No Update)` (`BtnRestartNoUpdate`)
      - `🛑 Stop Server` (`BtnStop`)
      - `💾 Hot SQLite Backup` (`BtnBackup`)
    - **Embedded Web Console & Android Companion App**:
      - Added `/api/control/start-noupdate` and `/api/control/restart-noupdate` endpoints.
      - Integrated matching control buttons into the web console dashboard and Android mobile UI (`index.html`, `app.js`).

### 2. Hot SQLite Backup Multi-DB Discovery (`game_0.db` & Dynamic Resolution)
- **Problem**:
  - The hot backup routine hardcoded the database path to `ConanSandbox/Saved/game.db`.
  - Live Conan Exiles servers (e.g. `\\192.168.0.5\ConanServerManager\`) utilize `game_0.db` (along with SQLite WAL/SHM files and rolling backups `game_0_backup_*.db`).
  - Because `game.db` did not exist, the backup aborted with `"Backup skipped: game.db does not exist yet."`
- **Architectural Solution**:
  - Implemented `GetActiveGameDbPath()` in `ServerEngine.cs`:
    1. Checks if `game_0.db` exists (primary Conan Exiles dedicated database).
    2. Checks if `game.db` exists (legacy/fallback name).
    3. Scans `Saved/` directory for any active `*.db` file, strictly filtering out rolling backups (`*_backup_*.db`), upgrade markers (`*_upgrade_tags_*.db`), and WAL/SHM temporary journals, selecting the most recently modified database.
  - Refactored `CreateHotBackupAsync()` to back up dynamically to `{dbPrefix}_{timestamp}.db` (e.g. `game_0_20261007_184500.db`).
  - Online hot backup uses `Microsoft.Data.Sqlite.SqliteConnection` in `ReadOnly` mode with the SQLite Online Backup API (`srcConn.BackupDatabase(destConn)`), guaranteeing atomic, crash-consistent point-in-time snapshots even while the game server is actively reading and writing transactions in WAL mode.

### 3. Custom Backup Directory Support
- **Problem**:
  - Backups defaulted strictly to `<appWorkingDir>\Backups`, with no configuration option to point backups to external drives, secondary volumes, or network-attached storage (NAS/UNC paths).
- **Architectural Solution**:
  - Added `CustomBackupDir` to `ManagerConfig` (defaulting to empty string).
  - Resolved `BackupDir` property dynamically: returns `Config.CustomBackupDir` when configured; falls back to `<AppWorkingDir>\Backups`.
  - Added Windows WPF UI controls in the Discord & Backup Automation panel:
    - Text input `TxtCustomBackupDir` supporting local paths (e.g. `D:\ConanBackups`) and UNC network shares (e.g. `\\NAS\backups`).
    - Dedicated browse button `BtnBrowseBackupDir` invoking `Microsoft.Win32.OpenFolderDialog`.
  - Mobile architecture clarification: The Android companion client triggers server-side backups via `POST /api/control/backup` without downloading massive 1 GB+ databases over cellular/mobile connections. The server executes the hot backup to its configured `BackupDir` and returns the file name and size confirmation in a mobile toast alert.

### 4. Automated Auto-Restart Scheduler & Countdown Warnings
- **Problem**:
  - Settings for daily restarts (`EnableDailyRestart`, `DailyRestartTime`, `MinimumUptime`, `RestartsPerDay`, `FirstWarningTime`, `SecondWarningTime`, `ThirdWarningTime`, `FastRestartZeroPlayers`) existed in configuration and UI, but were never evaluated by an active background timer.
- **Architectural Solution**:
  - Implemented `StartAutoRestartTimer()` in `ServerEngine.cs`, polling every 20 seconds.
  - Features:
    - **Minimum Uptime Guard**: Checks `DateTime.Now - ServerStartTime >= MinimumUptime` to prevent restart loops.
    - **Multi-Slot Daily Scheduling**: Calculates scheduled restart slots across yesterday, today, and tomorrow based on `DailyRestartTime` and `RestartsPerDay` (e.g. 1, 2, 4 restarts/day).
    - **RCON Countdown Broadcasts**: Parses user-defined warning countdowns (e.g. `10:00`, `05:00`, `02:00` or `00:10`) and broadcasts corresponding in-game warning messages via RCON at each milestone.
    - **Fast Restart on Zero Players**: If `FastRestartZeroPlayers` is enabled and 0 players are currently connected during the countdown window, triggers the restart sequence immediately rather than keeping the server running until the final countdown.
    - **Update Mode Awareness**: Evaluates `Config.AutoUpdateRestartMode`. If configured as `"Don't Auto-Update on Restart"`, triggers `RestartServerWithoutUpdateAsync()`; otherwise runs standard `RestartServerAsync()`.

### 5. Standalone Android APK Pipeline & Version Synchronization
- **Problem**:
  - The Android companion client was stuck displaying `v1.1.8` because past release packaging steps had renamed the pre-existing APK rather than running a fresh compilation through the Android toolchain.
- **Architectural Solution**:
  - Located and configured the local Android build toolchain:
    - OpenJDK 17: `C:\Program Files\Microsoft\jdk-17.0.20.101-hotspot`
    - Android SDK: `C:\Android\android-sdk` (API 34, Build-Tools 34.0.0)
    - Gradle 8.5: `C:\Gradle\gradle-8.5\bin\gradle.bat`
  - Synchronized version numbering across all project components to `v1.1.12`:
    - `version.txt` -> `1.1.12`
    - `src/ServerEngine.cs` -> `CurrentAppVersion = "1.1.12"`
    - `src/MainWindow.xaml` -> `v1.1.12`
    - `android/app/build.gradle` -> reads `version.txt`, computes `versionCode = 10112`
    - `android/app/src/main/assets/app.js` -> `APP_VERSION = "1.1.12"`
    - `android/app/src/main/assets/index.html` -> badge `v1.1.12`
    - `MainActivity.java` -> fallback `1.1.12` / `10112`
  - Compiled and verified release APK (`assembleRelease`), generating fresh, pre-signed `ConanServerManager-v1.1.12.apk` (4.63 MB).
  - Deployed to project root, `ServerManager/`, `ConanServerManager_DeployPackage/`, and the live server `\\192.168.0.5\ConanServerManager\conan.apk`.

---

## 32. In-App Steam Workshop Chromium & Mobile Browser Engine (v1.2.0)

> **Milestone Version:** 1.2.0  
> **Release Target:** Full-featured in-app web browsing with 1-click mod installation across Windows Desktop and Android Mobile Companion.

### 1. Overview & Architectural Motivation
Prior to v1.2.0, discovering and adding Steam Workshop mods required users to switch to an external web browser, search the Steam Community Workshop, copy the 9-to-10 digit Workshop `PublishedFileId` from the address bar, switch back to the Conan Server Manager (or Android Companion App), and paste the ID into the input field.

With **v1.2.0**, both the Windows Desktop Host application and the Android Companion Client feature native, integrated web browsing engines specifically tailored to the Conan Exiles Steam Workshop (`appid=440900`), featuring real-time mod detection and zero-copy 1-click installation.

---

### 2. Windows Desktop Architecture (`Microsoft.Web.WebView2` Full-Page Overlay Experience)
- **Full-Page Overlay Experience (`PnlFullPageBrowser`)**:
  - Replaced the cramped bottom console tab (`TabModBrowser`) with a dedicated full-page overlay covering the entire application window (`Grid.Row="0" Grid.RowSpan="5" Panel.ZIndex="1000"`).
  - Eliminates the need to constantly scroll in a restricted sub-panel, providing an expansive, immersive Steam Workshop browsing experience matching the full-page presentation of the Android companion app.
  - Includes a prominent `◀ Back to Server Manager` button (`BtnCloseFullPageBrowser`) in the top toolbar to immediately return to the server dashboard.
  - Native `Escape` key shortcut (`Window_KeyDown`) instantly closes the browser overlay and returns to the main manager dashboard.
  - Accessible from multiple intuitive locations:
    - Top Window Header (Row 0, next to Steam visibility badge): `BtnOpenWorkshopBrowser` (`🌐 Browse Workshop`)
    - Main Action Controls Bar (Row 2): `BtnOpenWorkshopBrowser` (`🌐 Workshop Browser`)
    - Mods Panel (Row 3): `BtnOpenModBrowser` (`🌐 Browse Workshop`)
    - Mod List Context Menu: `MnuViewInModBrowser` (`🌐 View in Workshop Browser`)
- **Startup Resilience & Lazy Initialization**:
  - To maintain instant WPF application launch times and prevent unnecessary Edge renderer subprocess initialization when the user is simply monitoring server logs, `EnsureCoreWebView2Async()` is invoked lazily on first invocation of `OpenFullPageBrowser()`.
  - Wrapped inside robust try/catch guards. If the Windows machine lacks the Microsoft Edge WebView2 runtime (legacy or custom Windows Server environments), the application gracefully displays an in-app error panel (`PnlWebView2Error`) offering an immediate official runtime download link (`https://go.microsoft.com/fwlink/p/?LinkId=2124703`) alongside an external browser fallback button.
- **Navigation Toolbar**:
  - `BtnCloseFullPageBrowser` (◀ Back to Server Manager): Closes overlay.
  - `BtnBrowserBack` (◀): Navigates backward through browsing history.
  - `BtnBrowserForward` (▶): Navigates forward through browsing history.
  - `BtnBrowserRefresh` (🔄): Reloads the current page.
  - `BtnBrowserHome` (🏠 Workshop): Returns directly to `https://steamcommunity.com/app/440900/workshop/`.
  - Address Bar (`TxtBrowserUrl` + `BtnBrowserGo`): Accepts direct URLs, search strings, or raw numeric mod IDs (auto-routing numeric inputs directly to `filedetails/?id={id}`).
  - Quick Search Bar (`TxtBrowserSearchQuery` + `BtnBrowserSearch`): Performs direct keyword searches across Conan Exiles Workshop items.
- **Smart Mod Detection Banner (`PnlBrowserModBanner`)**:
  - Subscribes to `SourceChanged` and `NavigationCompleted` events.
  - Parses the current URI using the pattern: `steamcommunity\.com/sharedfiles/filedetails/\?id=(?<id>\d+)`.
  - When the user navigates to any Workshop item detail page:
    - Banner slides into view showing `🧩 Workshop Mod Detected: Mod ID: <id>`.
    - Automatically checks `_engine.Config.Mods` and `LstMods` to determine installation status.
    - If already installed: displays `✅ Already installed on server` and renders `🗑️ Remove from Server`.
    - If not installed: displays `Not installed on server` and renders a prominent green `➕ Add This Mod to Server` button.
- **1-Click Mod Installation & Removal**:
  - Clicking `➕ Add This Mod to Server` automatically:
    1. Appends the mod ID to `_engine.Config.Mods`.
    2. Writes updated `manager_config.json`.
    3. Triggers `_engine.SyncIniSettings()`.
    4. Regenerates `modlist.txt` in server directory.
    5. Adds a new `ModDisplayItem` to the `LstMods` UI list.
    6. Asynchronously queries `SteamWorkshopHelper.GetModDetailsAsync(modId)` to resolve the real mod title and thumbnail preview.
    7. Updates the banner state immediately to `✅ Installed on server!`.
  - Clicking `🗑️ Remove from Server` cleanly removes the mod ID from `_engine.Config.Mods`, regenerates `modlist.txt`, and updates the UI.

---

### 3. Android Mobile Companion Architecture (`android.webkit.WebView`)
- **Native Full-Screen Overlay Dialog**:
  - Implemented `showWorkshopBrowser(String initialUrl)` within `MainActivity.java`.
  - Builds a programmatic full-screen dark-themed `Dialog` (`#0F172A`) containing:
    - Top Navigation Bar: Close button (`✕`), Back (`◀`), Forward (`▶`), URL/Title display, Reload (`🔄`), and Open in External Browser (`🌐`).
    - Core Mobile Browser: Hardware-accelerated `android.webkit.WebView` with DOM storage, database, pinch-to-zoom, and overview mode enabled.
    - Hardware Back-Button Handling: `setOnKeyListener` intercepts the device's physical back button, calling `workshopWebView.goBack()` if history exists, or cleanly dismissing the dialog when at the root page.
- **Smart Mobile Mod Detection Banner**:
  - `WebViewClient.onPageFinished` and `onPageStarted` evaluate loaded URLs against the regex `steamcommunity\.com/sharedfiles/filedetails/\?id=(\d+)`.
  - When on a Workshop item page, reveals a high-contrast indigo action bar (`#1E1B4B`) with:
    - Text: `🧩 Mod ID: <id>`
    - Button: `➕ Add to Server` (Emerald Green `#10B981`)
  - Tapping `➕ Add to Server` executes `webView.evaluateJavascript("addModToServer('" + detectedModId + "');")` on the companion web view, immediately posting `POST /api/mods/add` to the remote server host, displaying a native Android toast, and updating button feedback.
- **JavaScript Bridge Integration**:
  - Exposed `@JavascriptInterface public void openWorkshopBrowser(String url)` on `AndroidBridge`.
  - Added "🌐 Open Steam Workshop Browser" button inside `tab-mods` in `index.html` and wired mod detail modal actions in `app.js` to seamlessly trigger the in-app browser without ever leaving the companion app.

---

### 4. Unified Version Synchronization (`v1.2.0` / `10200`)
- Synchronized version numbering across all 7 project layers:
  1. `version.txt` -> `1.2.0`
  2. `src/ServerEngine.cs` -> `CurrentAppVersion = "1.2.0"`
  3. `src/MainWindow.xaml` -> `v1.2.0` (title and header badge)
  4. `android/app/build.gradle` -> reads `version.txt`, fallback `"1.2.0"`, `versionCode = 10200`
  5. `android/app/src/main/java/com/conan/servermanager/MainActivity.java` -> fallback `"1.2.0"`, `10200`
  6. `android/app/src/main/assets/app.js` -> `APP_VERSION = "1.2.0"`
  7. `android/app/src/main/assets/index.html` -> badge `v1.2.0`
- Compiled and verified release APK (`assembleRelease`), generating pre-signed `ConanServerManager-v1.2.0.apk` (4.63 MB) with versionCode `10200`.
- Published standalone win-x64 binaries (`dotnet publish`) to `ServerManager/`, packaged `ConanServerManager_v1.2.0.zip` and `ConanServerManager_DeployPackage.zip` locally.
- **Strict User-Only Deployment**: All release artifacts (ZIP packages, APKs) remain strictly in the local development workspace (`F:\Projects\Conan Exiles Dedicated Server\`) for manual deployment by the user. Direct deployment to the live server is strictly prohibited.

---

## 33. Strict Version Numbering & Synchronized Dual-Platform Build Policy

> **Policy File:** [`VERSIONING_RULES.md`](file:///F:/Projects/Conan%20Exiles%20Dedicated%20Server/VERSIONING_RULES.md) & [`GEMINI.md`](file:///F:/Projects/Conan%20Exiles%20Dedicated%20Server/GEMINI.md)  
> **Enforcement:** Mandatory on every single build, compile, bug fix, or feature addition.

### 1. Core Mandates
1. **Never Compile Without Bumping the Version**:
   - The version number **MUST ALWAYS** be incremented whenever a compile or build is run.
   - Building packages on identical or stale version numbers is strictly prohibited.
2. **Mandatory Synchronization Between Windows & Android**:
   - Both the Windows desktop manager and the Android mobile companion app **MUST ALWAYS** be synchronized at the exact same version number and version code.
   - When one platform is compiled or updated, the other platform must be updated and compiled in lockstep.
3. **Android Version Code Formula**:
   - Monotonically increasing formula:
     $$\text{versionCode} = (\text{MAJOR} \times 10000) + (\text{MINOR} \times 100) + \text{PATCH}$$
   - Example: `v1.2.1` $\rightarrow$ $1 \times 10000 + 2 \times 100 + 1 = 10201$.
4. **The 7 Mandatory Synchronization Points**:
   1. `version.txt`
   2. `src/ServerEngine.cs` (fallback in `GetAppVersion()`)
   3. `src/MainWindow.xaml` (`TxtAppHeaderTitle` & `TxtAppHeaderVersionBadge`)
   4. `android/app/build.gradle` (fallback in `getAppVersionName()` & `getAppVersionCode()`)
   5. `android/app/src/main/assets/app.js` (`let APP_VERSION`)
   6. `android/app/src/main/assets/index.html` (`#appInstalledVersion`)
   7. `android/app/src/main/java/com/conan/servermanager/MainActivity.java` (fallback in `getAppVersion()` & `getAppVersionCode()`)
5. **Mandatory Git Tag Creation & GitHub Push**:
   - Every compile and release must immediately produce an annotated Git tag pushed to GitHub:
     `git tag -a v{MAJOR}.{MINOR}.{PATCH} -m "Release v{MAJOR}.{MINOR}.{PATCH}: <summary>"`
     `git push origin v{MAJOR}.{MINOR}.{PATCH}`
   - This enables the user to upload the compiled Windows ZIP and Android APK directly to the GitHub release.

---

## 34. Full-Page Workshop Browser Overlay & Dual-Platform Synchronization (v1.2.1)

> **Milestone Version:** 1.2.1 (versionCode `10201`)  
> **Release Target:** Full-page Workshop browser overlay experience and synchronized dual-platform release pipeline.

### 1. Windows Desktop Full-Page Browser Overlay (`PnlFullPageBrowser`)
- **Immersive Full-Window Layout**:
  - Replaced the cramped bottom console tab (`TabModBrowser`) with a dedicated full-page overlay covering the entire 1400x960 window area (`Grid.Row="0" Grid.RowSpan="5" Panel.ZIndex="1000"`).
  - Eliminates vertical scrolling constraints and enables comfortable viewing of long Workshop descriptions, mod requirement lists, and changelogs.
- **Header & Action Bar Shortcuts**:
  - Window Header (Row 0): `BtnOpenWorkshopBrowser` (`🌐 Browse Workshop`) directly adjacent to the Steam status indicator.
  - Action Controls Bar (Row 2): `BtnOpenWorkshopBrowser` (`🌐 Workshop Browser`).
  - Mods Management Panel (Row 3): `BtnOpenModBrowser` (`🌐 Browse Workshop`).
  - Mod List Context Menu: `MnuViewInModBrowser` (`🌐 View in Workshop Browser`).
- **Seamless Navigation & Dismissal**:
  - `BtnCloseFullPageBrowser` (◀ Back to Server Manager) button pinned to the top navigation toolbar.
  - Native <kbd>Esc</kbd> key interceptor (`Window_KeyDown`) instantly dismisses the overlay and restores view of server logs and controls.
- **Smart Mod Detection & 1-Click Server Installation**:
  - Dynamically detects Workshop detail pages (`steamcommunity.com/sharedfiles/filedetails/?id=...`).
  - Slide-in action banner indicates if the mod is already installed on the server, with 1-click `➕ Add This Mod to Server` or `🗑️ Remove from Server`.

### 2. Android Mobile Companion App Synchronized Release (`ConanServerManager-v1.2.1.apk`)
- **Version Code 10201 Alignment**:
  - Built with OpenJDK 17, Android SDK API 34, and Gradle 8.5 (`assembleRelease`).
  - Verified pre-signed APK generated: `ConanServerManager-v1.2.1.apk` (4.63 MB).
  - Embedded WebView assets updated with synchronized `APP_VERSION = "1.2.1"` and badge `#appInstalledVersion`.

### 3. Local Release Artifacts
- **Windows Self-Contained Release**: Published to `ServerManager/` via `dotnet publish -c Release -r win-x64 --self-contained true`.
- **Release Archives**: Created `ConanServerManager_v1.2.1.zip` (121.6 MB) and refreshed `ConanServerManager_DeployPackage.zip`.
- **Zero Live Server Deployment**: All binaries, APKs, and archives remain strictly local in `F:\Projects\Conan Exiles Dedicated Server\`.

---

## 35. Background SteamCMD Mod Pre-Download & Cache Engine (v1.2.2)

> **Milestone Version:** 1.2.2 (versionCode `10202`)  
> **Release Target:** Headless background SteamCMD mod pre-downloading directly to server disk cache, live mod ready/download status indicators, and full dual-platform synchronization.

### 1. Problem Statement & Motivation
- **Restart Downtime Bottleneck:** In prior versions, adding new mods via the manager or mobile companion app appended the mod ID to `manager_config.json` and `modlist.txt`, but actual `.pak` download only occurred during the server start/update routine (`RunFullUpdateAndStartAsync`). For large mods (such as multi-gigabyte overhaul or asset mods), the server was kept offline while SteamCMD downloaded tens of gigabytes across slow content networks.
- **Zero Game Conflict Opportunity:** The dedicated server binary (`ConanSandboxServer-Win64-Shipping.exe`) only locks files for `.pak` files actively mounted in memory at boot. New mods residing in unmounted directories (`steamapps\workshop\content\440900\<ModID>\`) have zero file locks, allowing SteamCMD to safely download and validate mod archives in the background while players remain actively connected and playing.
- **Instant Subsequent Boot:** By pre-downloading mod files into `steamapps\workshop\content\440900\<ModID>\` in the background prior to server restarts, the subsequent startup update check completes in 1–2 seconds via SteamCMD checksum validation rather than waiting 15–30 minutes for fresh downloads.

### 2. Core Architecture (`src/ServerEngine.cs`)
- **Concurrency Semaphore (`SemaphoreSlim _steamCmdLock`)**:
  - Guards SteamCMD operations to prevent concurrent process collisions between background mod pre-downloads and the full server update routine.
  - Initialized with capacity `1, 1`.
- **Pre-Download Execution Engine (`PreDownloadModAsync`)**:
  - Headless SteamCMD invocation: `+force_install_dir "<ServerDir>" +login anonymous +workshop_download_item 440900 <modId> validate +quit`.
  - Up to 3 automatic retries with exponential backoff on download failures or network timeouts.
  - Automatically regenerates `ConanSandbox\Mods\modlist.txt` upon successful download completion, ensuring newly downloaded `.pak` files are registered.
  - Exposes `IsModPreDownloading`, `CurrentPreDownloadingModId`, and fires `OnModPreDownloadCompleted` event callbacks.
- **On-Disk Detection & Validation**:
  - `IsModDownloaded(string modId)`: Scans `steamapps\workshop\content\440900\<modId>` for any existing `.pak` file > 0 bytes.
  - `GetModPakPath(string modId)`: Returns the absolute path to the downloaded mod `.pak` file.
- **Update Prioritization & Clean Preemption**:
  - If the server administrator clicks **"Update & Start"** (`RunFullUpdateAndStartAsync`) while a mod is pre-downloading in the background, the pre-download task is cleanly aborted and its process terminated.
  - Full server update acquires `_steamCmdLock` with top priority, and SteamCMD automatically resumes from cached download chunks during the update phase.

### 3. API & Web Service Extensions (`src/WebServer.cs` & `src/SteamWorkshopHelper.cs`)
- **`GET /api/mods`**:
  - Enriched with `isDownloaded: bool` for every configured mod.
- **`POST /api/mods/add`**:
  - Automatically kicks off background pre-download (`_ = _engine.PreDownloadModAsync(modId)`) immediately upon mod addition.
- **`POST /api/mods/predownload`**:
  - Remote endpoint accepting `{"modId": "123456"}` to trigger on-demand background pre-downloading or file verification from companion clients.
- **`GET /api/status`**:
  - Exposes `isPreDownloading: bool` and `preDownloadingModId: string` to give mobile clients real-time visibility into server download tasks.

### 4. WPF Desktop UI Enhancements (`src/MainWindow.xaml` / `MainWindow.xaml.cs`)
- **Live Status Badges**:
  - Mod list items display `✅ Ready on Disk` (green) or `⏳ Pending Download` (amber) in their subtitle.
- **Direct Context Menu & Button Controls**:
  - Added **"📥 Pre-Download / Verify Mod Files Now"** to the Mod list right-click context menu (`MnuPreDownloadMod_Click`).
  - Added **"📥 Pre-Download Mod"** toolbar button (`BtnPreDownloadMod_Click`) in the Mods tab.
- **Automatic Browser Pre-Download**:
  - In the Full-Page Workshop Browser overlay, clicking **"➕ Add This Mod to Server"** automatically launches background pre-downloading for the mod.
  - Slide-in banner and status text indicate `📥 Mod added & pre-download started in background!`.
- **Manual Mod ID Prompt**:
  - Adding a mod via **"➕ Add Mod"** dialog prompts the administrator whether to immediately pre-download the mod files in the background.

### 5. Android Mobile Companion App Updates (`app.js` & `index.html`)
- **Visual Status Badges**:
  - `fetchInstalledMods()` renders `✅ On Disk` or `⏳ Pending Download` badges on every mod card.
- **Mod Details Modal Pre-Download Action**:
  - Added **"📥 Pre-Download / Validate on Server"** button in `modDetailsModal`.
  - Tapping invokes `onModalPreDownload()` calling `POST /api/mods/predownload` on the server host.
- **Automatic Browser Trigger**:
  - Adding a mod via the mobile Workshop browser informs the user that background pre-downloading has started on the server host.

### 6. Synchronized Dual-Platform Release (`v1.2.2` / `10202`)
- Synchronized all 7 core version points:
  1. `version.txt` -> `1.2.2`
  2. `src/ServerEngine.cs` -> `CurrentAppVersion = "1.2.2"`
  3. `src/MainWindow.xaml` -> `v1.2.2` (title and header badge)
  4. `android/app/build.gradle` -> `getAppVersionName() = "1.2.2"`, `getAppVersionCode() = 10202`
  5. `android/app/src/main/assets/app.js` -> `APP_VERSION = "1.2.2"`
  6. `android/app/src/main/assets/index.html` -> badge `v1.2.2`
  7. `android/app/src/main/java/com/conan/servermanager/MainActivity.java` -> fallback `"1.2.2"`, `10202`
- **Build Status**:
  - Windows: Self-contained `win-x64` build compiled with 0 Errors / 0 Warnings; packaged `ConanServerManager_v1.2.2.zip` and updated `ConanServerManager_DeployPackage.zip`.
  - Android: Generated pre-signed `ConanServerManager-v1.2.2.apk` (4.64 MB, versionCode `10202`).
- **Zero Remote Deployment**: All binaries and packages remain strictly local for manual administrator distribution.

---

## 36. Linux Fedora KDE Desktop Client & Cross-Platform Avalonia Architecture (v1.3.0)

> **Milestone Version:** 1.3.0 (versionCode `10300`)  
> **Release Target:** Native Linux GUI client for Fedora KDE (built with Avalonia UI), self-contained `linux-x64` packaging, local network UDP auto-discovery, and multi-platform synchronization across Windows, Android, and Linux.

### 1. Problem Statement & Motivation
- **Cross-Platform Administration**: Administrators managing Conan Exiles Dedicated Servers often run Linux workstations (such as Fedora Linux with KDE Plasma) while the game server itself runs on a dedicated Windows host (since Conan Sandbox Server has no native Linux binary).
- **WPF Limitations**: The primary Windows manager GUI is built with WPF (`net10.0-windows`), which is tied to the Windows DirectX/Win32 subsystem and cannot execute natively on Linux / X11 / Wayland without complex, brittle Wine configurations.
- **Avalonia UI Solution**: By implementing a dedicated cross-platform client with Avalonia UI 12.x (`client-linux/`), administrators on Fedora KDE get a true native Linux desktop application with hardware acceleration (Vulkan/OpenGL/Wayland/X11), native KDE Plasma window decorations, dark glass styling, and zero external runtime dependencies.

### 2. Linux Client Architecture (`client-linux/`)
- **Modern Avalonia UI 12.x GUI**:
  - Engineered with dark modern glass styling matching the Windows desktop and Android companion apps.
  - Full-featured header with live connection indicator, server address input, and 1-click **Scan LAN** auto-discovery.
- **Top Metrics Dashboard**:
  - Live Server Status badge (`RUNNING`, `STOPPED`, `UPDATING`), uptime timer, CPU usage %, RAM MB and %, player counter (`0 / 40`), and Steam Workshop mods count.
  - Active download progress bar showing real-time transfer speed (MB/s) and ETA during updates and mod pre-downloads.
- **Remote Server Lifecycle Controls**:
  - ▶ **Start Server** (`POST /api/control/start`)
  - ■ **Stop Server** (`POST /api/control/stop`)
  - 🔄 **Clean Restart** (`POST /api/control/restart` with automated player warnings and countdown sequence)
  - 💾 **Trigger Hot Backup** (`POST /api/control/backup`)
- **Integrated Multi-Tab Console**:
  - **Live Server Console**: Real-time streaming log output from `/api/logs` with auto-scroll and clipboard copy.
  - **RCON Terminal**: Interactive command prompt with instant execution (`/api/control/rcon`) and quick-access macros (`save`, `listplayers`, `broadcast`, `kick`).
  - **Steam Workshop Mod Manager**: Displays installed mods with on-disk pre-download badges (`✅ Ready on Disk` vs `⏳ Pending Download`), 1-click `➕ Add Mod ID`, `📥 Pre-Download / Validate on Server`, `🗑️ Remove Mod`, and `🌐 Open Steam Workshop in Browser`.
  - **Live INI Configuration Editor**: Fetches and saves `ServerSettings.ini`, `Engine.ini`, and `Game.ini` directly over the manager REST API (`/api/ini`).
  - **Remote Settings Form**: Visual editor for server name, passwords, ports, and player caps (`/api/config`).

### 3. UDP Local Network Auto-Discovery
- **Cross-Platform LAN Discovery** ([`DiscoveryHelper.cs`](file:///F:/Projects/Conan%20Exiles%20Dedicated%20Server/client-linux/DiscoveryHelper.cs)):
  - Broadcasts `CONAN_DISCOVER_REQUEST` on UDP port `8089` across all active network interfaces.
  - Conan Server Manager instances on the local network reply with server name, HTTP port, and game port (`CONAN_DISCOVER_RESPONSE|<name>|<webPort>|<gamePort>`).
  - Discovered servers populate the dropdown for instantaneous 1-click connection.
  - Remembers the last connected server address in `~/.config/conanservermanager/client_config.json`.

### 4. Fedora KDE Plasma Desktop Integration & Packaging
- **Self-Contained `linux-x64` Binary**:
  - Compiled and published with `--self-contained true -r linux-x64`.
  - Bundles the complete .NET runtime and native Linux graphics libraries. Runs immediately on any fresh Fedora KDE installation without requiring `dnf install dotnet`.
- **KDE Plasma Integration Files**:
  - `conan-server-manager.desktop`: Standard FreeDesktop / XDG desktop entry with application categories and icon association.
  - `install-kde-desktop.sh`: 1-click installer script copying desktop launcher to `~/.local/share/applications/` and icon to `~/.local/share/icons/hicolor/256x256/apps/`, refreshing KDE menu cache via `kbuildsycoca6` / `kbuildsycoca5`.
  - `run.sh`: Convenient executable launcher script.
- **Release Packages**:
  - `ConanServerManager-linux-x64-v1.3.0.tar.gz` (45.4 MB, preserves Linux executable permissions `+x`)
  - `ConanServerManager-linux-x64-v1.3.0.zip` (47.3 MB)

### 5. Multi-Platform Release Synchronization (`v1.3.0` / `10300`)
- Synchronized all platform layers:
  1. `version.txt` -> `1.3.0`
  2. `src/ServerEngine.cs` -> `CurrentAppVersion = "1.3.0"`
  3. `src/MainWindow.xaml` -> `v1.3.0`
  4. `android/app/build.gradle` -> `1.3.0`, `10300`
  5. `android/app/src/main/assets/app.js` -> `APP_VERSION = "1.3.0"`
  6. `android/app/src/main/assets/index.html` -> `v1.3.0`
  7. `android/app/src/main/java/com/conan/servermanager/MainActivity.java` -> `1.3.0`, `10300`
  8. `client-linux/` -> `v1.3.0`
- **Zero Remote Deployment**: All binaries, archives, and APKs remain strictly local in the project workspace for manual administrator deployment.

---

## 37. Future Roadmap & Upcoming Engineering Tasks (To-Do)

### To-Do: Automated Mod Dependency Resolution
- Investigate querying Steam Workshop item dependencies (e.g. required framework mods like Pippi, ModControlPanel) and prompt users with 1-click batch installation of prerequisite mods when installing an item.

---
*End of Source of Truth Document. Keep this file in your project repository as a complete architectural reference.*



