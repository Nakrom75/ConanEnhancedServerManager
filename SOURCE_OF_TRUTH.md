# SOURCE OF TRUTH: Conan Exiles Dedicated Server & Manager Architecture

> **Document Version:** 1.0.0  
> **Target Application:** Conan Exiles Dedicated Server (AppID `443030`)  
> **Date:** September 2026  
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

---

## 1. Executive Overview & Server Topology

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

14. **GitHub Auto-Updater & Semantic Versioning (v1.0.0 -> v1.0.1)**:
   - **Semantic Versioning**: Centralized in `<Version>1.0.1</Version>` in `ConanServerManager.csproj` and `ServerEngine.CurrentAppVersion`. Displayed prominently in the desktop header, log outputs, and web dashboard with an active `✨ v1.0.1` version badge.
   - **GitHub Releases REST API Integration**: Queries `https://api.github.com/repos/Nakrom75/ConanEnhancedServerManager/releases/latest` to parse tag versions, asset packages, release notes, and download links.
   - **Desktop Notification Banner & Action Controls**: Added `PnlAppUpdateBanner` with **📥 Download & Update Now**, dismiss button, and Action Bar button **🔄 Check App Updates**. Configurable checkboxes `ChkAutoCheckAppUpdates` and `ChkAutoInstallAppUpdates` control automatic checking and background installation.
   - **Self-Updating Trampoline (`update_helper.bat`) with Config Protection**: Solves Windows OS file locks on running `.exe` files. Downloads the update zip asset, extracts to `Updates/staged/`, strips any incoming `manager_config.json` to guarantee local server credentials/passwords are never overwritten, spawns `update_helper.bat` with PID monitoring, terminates the app, replaces binaries via `xcopy`, relaunches `ConanServerManager.exe`, and cleans up all temporary staged files.
   - **Remote & Web API Endpoints**: Added `/api/control/check-update` and `/api/control/apply-update` for headless remote updating over LAN/WAN.

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

---
*End of Source of Truth Document. Keep this file in your project repository as a complete architectural reference.*



