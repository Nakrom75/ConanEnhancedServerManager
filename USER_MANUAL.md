# Conan Enhanced Server Manager — Complete User Manual

Welcome to the official User Manual for **Conan Enhanced Server Manager**!

Whether you are hosting a private server for friends or running a heavily modded public community server, this guide walks you through installation, port forwarding, server configuration, mod load-order management, crash recovery, and remote administration from your PC, web browser, or Android phone.

---

## Table of Contents

1. [Overview & Ways to Use the Manager](#1-overview--ways-to-use-the-manager)
2. [Quick Start: First-Time Installation](#2-quick-start-first-time-installation)
3. [Network & Port Forwarding Setup](#3-network--port-forwarding-setup)
4. [Dashboard & Server Configuration Guide](#4-dashboard--server-configuration-guide)
5. [Starting, Stopping & Updating Your Server](#5-starting-stopping--updating-your-server)
6. [Mod Management & Steam Workshop Guide](#6-mod-management--steam-workshop-guide)
7. [Smart Mod Load Order & Auto-Sorting](#7-smart-mod-load-order--auto-sorting)
8. [Crash Loop Protection & Mod Diagnostics](#8-crash-loop-protection--mod-diagnostics)
9. [Backups, Database Restore & Direct INI Editing](#9-backups-database-restore--direct-ini-editing)
10. [Live Console, RCON & Player Management](#10-live-console-rcon--player-management)
11. [Remote Administration (PC Client, Web UI & Android App)](#11-remote-administration-pc-client-web-ui--android-app)
12. [Troubleshooting & Frequently Asked Questions (FAQ)](#12-troubleshooting--frequently-asked-questions-faq)

---

## 1. Overview & Ways to Use the Manager

**Conan Enhanced Server Manager** is a self-contained management suite for the **Conan Exiles Dedicated Server** (Steam AppID `443030`). It automatically installs and manages SteamCMD, downloads the Conan Exiles Dedicated Server and Steam Workshop mods, synchronizes Unreal Engine `.ini` files, monitors server health via RCON, and provides built-in remote management.

You can manage your server in **four ways**:

| Mode | How It Works | Best For |
| :--- | :--- | :--- |
| **1. Windows Local Host Mode** | Run `ConanServerManager.exe` directly on the Windows machine hosting the Conan Exiles server. | Setting up and running the dedicated server on your host PC or Windows Server. |
| **2. Windows Remote Client Mode** | Run `ConanServerManager.exe` on your main gaming PC and switch **Operation Mode** to `Remote Client Mode`. | Full desktop GUI control of a dedicated server box in another room or datacenter. |
| **3. Built-in Web Console** | Open `http://<Server-IP>:8088` in any desktop or mobile web browser. | Quick browser-based access without installing anything on client devices. |
| **4. Android Companion App** | Install `ConanServerManager-vX.Y.Z.apk` on your Android phone or tablet. | Full mobile management, live console logs, mod management, and status monitoring on the go. |

---

## 2. Quick Start: First-Time Installation

### Step 1: Download & Extract
1. Go to the [GitHub Releases Page](https://github.com/Nakrom75/ConanEnhancedServerManager/releases) and download the latest **`ConanServerManager_vX.Y.Z.zip`** (or `ConanServerManager_DeployPackage.zip`).
2. Create a dedicated folder on your server drive with plenty of free space (at least **25–40 GB** for the server + mods), for example:
   ```text
   C:\ConanServerManager\
   ```
   > **Important:** Do **not** extract the server manager inside `C:\Program Files` or `C:\Windows`, as Windows UAC permissions can block SteamCMD and Unreal Engine from writing `.ini` and `.db` files.
3. Extract all files from the `.zip` archive into that folder.

### Step 2: Launch the Application
1. Double-click **`ConanServerManager.exe`** (or `START_SERVER_MANAGER.bat`).
2. No `.NET` runtime installation is required—the Windows build is 100% self-contained.
   - *Optional:* If you want to use the built-in **Steam Workshop Browser** window inside the desktop app, ensure the standard **Microsoft Edge WebView2 Runtime** is installed on Windows (pre-installed on Windows 11 and most Windows 10 systems). If WebView2 is not installed on a barebones Windows Server OS, the manager will automatically open Workshop pages in your default web browser instead.

### Step 3: Folder Structure Created Automatically
When you first click **▶ Update & Start**, the manager automatically downloads SteamCMD and installs the Conan Exiles Dedicated Server inside your base directory:
```text
YourFolder\
 ├── ConanServerManager.exe     (The Server Manager application)
 ├── manager_config.json        (Saved manager settings, mod list & metadata)
 ├── steamcmd\                  (Automatically downloaded & managed SteamCMD)
 ├── ConanExilesServer\         (Automatically downloaded Conan Exiles Server files)
 │    └── ConanSandbox\Saved\Config\WindowsServer\  (ServerSettings.ini, Engine.ini, Game.ini)
 │    └── ConanSandbox\Mods\    (modlist.txt & active mod files)
 └── Backups\                   (Automated & manual ZIP backups of your world database)
```
> **Already have an existing Conan Exiles server?**
> Click the **📂 Browse Base Path** button in the top configuration area to point the manager to your existing parent folder, or place the manager inside that folder.

---

## 3. Network & Port Forwarding Setup

For players outside your home network (or on Steam) to see and join your server, you must open the required ports in **Windows Firewall** and forward them in your **Router**.

### Step 1: Configure Windows Firewall (One Click)
Inside the manager's action bar, click **`🛡️ Configure Firewall`**.
- The manager will request Administrator privileges and automatically create inbound/outbound Windows Firewall rules for your configured ports.

### Step 2: Router Port Forwarding Table
Log into your router's admin page and forward the following ports to the **Local LAN IP address** of your server machine (e.g., `192.168.1.100`):

| Port Name | Default Port | Protocol | Required For |
| :--- | :---: | :---: | :--- |
| **Game Port** | `7777` | **UDP** | Primary Unreal Engine game traffic (Required to join) |
| **Raw UDP Port** | `7778` | **UDP** | Always `Game Port + 1` (Required by Conan Exiles) |
| **Steam Query Port** | `27015` | **UDP** | Steam Master Server browser listing & A2S status queries |
| **RCON Port** | `25575` | **TCP** | *Local Only* (Do **not** port-forward to the public internet unless needed; used by the manager to talk to the server) |
| **Web / Remote API Port** | `8088` | **TCP** | *LAN or VPN recommended* (Used by the Android App, Web Console, and Remote PC Client) |

### Step 3: Checking the Steam Visibility Indicator
In the top-right header of the application, you will see the **Steam Master Server Visibility Badge**:
- **`⚪ STEAM: OFFLINE`**: The server is stopped, still booting up, or UDP Query Port (`27015`) is not yet responding.
- **`🟢 STEAM: ONLINE`**: The server is actively responding to Steam A2S Query packets on your Query Port (`27015`) and is ready for players!

---

## 4. Dashboard, Server Configuration & Gameplay Settings (`ServerSettings.ini`)

### Top Configuration Cards (Row 2)
The upper configuration area is organized into **4 quick-access cards**:

#### Card 1: Server Configuration & Identity
- **Server Name**: The public name displayed in the Conan Exiles server browser.
- **Startup Map**:
  - **ConanSandbox (Exiled Lands)** — Standard base-game map.
  - **Isle of Siptah (DLC_EXT)** — Expansion map (`/Game/DLC_EXT/DLC_Siptah/Maps/DLC_Isle_of_Siptah`).
  - **Savage Wilds (Mod Map)** — Pre-configured `/Game/Mods/Savage_Wilds/Savage_Wilds` path.
  - **Custom Map Path...** — Enter any custom mod map path directly.
- **Server Join Password**:
  - **Leave completely blank** to run a **Public Server** that anyone can join.
  - Enter a password to run a **Private Server**.
- **Admin Password**: Used in-game via `Settings -> Server Settings -> Make Me Admin` to unlock the Admin Panel (`Shift + Insert`).
- **Server Region** (`EU`, `US`, `Asia`, `OC`, `SA`, `JP`), **Max Players** (default `40`), **Idle Kick Time**, and **PvP Enabled** / **VAC Enabled** checkboxes.

#### Card 2: Network, Ports & RCON
- **Game Port (`7777`)**, **Raw UDP Port (`7778`)**, and **Query Port (`27015`)**.
- **Enable RCON (`25575`) & RCON Password**: Keep RCON enabled! The manager uses RCON for graceful shutdowns, in-game broadcast announcements, live player lists, and freeze/deadlock detection.
- **Enable Web UI & Mobile Port (`8088`)**: Hosts the built-in Web Console and Remote/Android REST API.
- **Bind Specific IP (`MULTIHOME`)**: Leave unchecked on standard single-NIC servers; only enable if binding to a specific local adapter IP.

#### Card 3: Branch & Process Scaling
- **Steam Branch (`public` or `testlive`)** and optional Beta Password.
- **Process Priority** (`Normal`, `AboveNormal`, `High`, `Realtime`) and **All Cores (`UseAllAvailableCores`)**.
- **Custom Server Dir (`📁` Browse)**: Leave blank to use the default `ConanExilesDedicatedServer` subfolder, or browse to an existing server installation directory anywhere on disk.
- **Operation Mode**: Switch between **Local Server Host** and **Remote Client Mode** (with Remote Host URL, API Password, and `🔗 Connect` button).

#### Card 4: Automation, Backups & Alerts
- **Auto-Restart on Crash (Watchdog)** & **Update on Restart**: Monitors process health and RCON heartbeats, automatically recovering from crashes or hangs.
- **Daily Scheduled Restart**: Automatically broadcasts in-game countdown warnings (`10m`, `5m`, `2m`, `1m`) and restarts at the configured 24h time (e.g., `04:00`).
- **Pre-Backup / Startup `.BAT` Script Mode**: Optionally run `pre_backup.bat` before backups or `pre_start.bat` before server startup, plus **Keep Backups (Days)** automatic retention pruning.
- **Discord Webhook URL & `Test` Button**: Receive rich Discord notifications on server start, stop, update, or crash.

---

### The **`⚙️ Gameplay Settings`** Tab (Visual `ServerSettings.ini` Configurator)
You no longer need to log into the game as an admin to configure server multipliers or world rules! Switch to the **`⚙️ Gameplay Settings`** tab (`Tab 5`) to visually configure **45 Conan Exiles `ServerSettings.ini` parameters** across 8 categories:

1. **⚔️ Combat Mode & General Rules**:
   - **Quick Combat Mode Preset**: One-click presets for **`PvE (Cooperative)`**, **`PvE-Conflict`**, **`PvP (Full Raiding)`**, or **`Custom`**.
   - **PvP Enabled (`PVPEnabled`)**, **Building Damage (`CanDamagePlayerOwnedStructures`)**, **Dynamic Building Damage (Raid Protection)**, **Sandstorm Enabled**, **Max Nudity** (`None`, `Partial`, `Full`), **Max Clan Size**, and **Max Allowed Ping**.
2. **💀 Death, Loot & Ownership Rules**:
   - **Drop Equipment on Death**, **Drop Shortcut Bar on Death**, **Drop Backpack on Death** *(uncheck all three to let players keep all items on death)*.
   - **Everyone Can Loot Corpse**, **Logged-Out Bodies Remain in World**, **Containers Ignore Ownership**, and **No Ownership**.
3. **🏰 Building Decay, Land Claim, Purge & Avatars**:
   - **Disable Building Abandonment (No Decay)**, **Building Decay Time Multiplier**, **Land Claim Radius Multiplier**, **Build Anywhere**, **Enable Purge**, **Purge Level (`0`–`6`)**, and **Disable Avatars (Gods)**.
4. **✨ Experience (XP) Multipliers**:
   - **Overall XP Rate (`PlayerXPRateMultiplier`)**, **Kill XP**, **Harvest XP**, **Crafting XP**, and **Passive Time XP**.
5. **⛏️ Harvesting, Crafting, Thralls & Resources**:
   - **Harvest Amount Multiplier**, **Crafting Time Multiplier** (`< 1.0` = faster), **Thrall Conversion Time** (`< 1.0` = faster breaking), **Animal Pen Taming Time**, **Resource Respawn Speed**, **Food Spoil Rate Scale**, **Fuel Burn Time Multiplier**, **Crafting Cost Multiplier**, and **Player Encumbrance Multiplier**.
6. **☀️ Day / Night Cycle Speed**:
   - **Overall Day Cycle Speed**, **Daytime Speed**, **Nighttime Speed**, and **Dawn / Dusk Speed**.
7. **🛡️ Stamina, Durability, Thirst & Hunger**:
   - **Stamina Cost Multiplier**, **Item Durability Multiplier**, **Shield Durability Multiplier**, **Active/Idle Thirst Multipliers**, and **Active/Idle Hunger Multipliers**.
8. **🗡️ Combat Damage Multipliers (Player, NPC & Thrall)**:
   - **Player Damage Dealt/Taken**, **NPC/Monster Damage Dealt/Taken**, and **Thrall Damage to Players/NPCs**.

**Gameplay Toolbar Actions:**
- **`🔄 Reload from INI`**: Re-imports current values directly from `ServerSettings.ini` on disk.
- **`↺ Reset to Vanilla (1.0x)`**: Resets all gameplay multipliers and rules back to official Funcom defaults.
- **`💾 Save & Apply Gameplay Settings`**: Saves your settings to `manager_config.json` and immediately writes all 45 keys to `ServerSettings.ini` (and syncs remotely if in Remote Client Mode).

---

## 5. Starting, Stopping & Updating Your Server

The **Action Control Bar** provides full control over the server lifecycle and system maintenance:

| Button | What It Does |
| :--- | :--- |
| **`▶ Update & Start`** | Saves config, runs **SteamCMD** to verify/update the Conan Exiles Dedicated Server and download/update all active Workshop mods, syncs `modlist.txt` & `ServerSettings.ini`, and launches the server. |
| **`⚡ Start (No Update)`** | Skips SteamCMD verification and launches the server immediately using the files currently on disk. *(Missing mods are still downloaded automatically).* |
| **`🔄 Restart & Update`** | Broadcasts a graceful countdown to online players via RCON, saves the world, stops the server, runs SteamCMD updates for server + mods, and starts the server back up. |
| **`⚡ Quick Restart`** | Gracefully stops the server and restarts it immediately without running SteamCMD updates. |
| **`⏹ Stop Server`** | Sends a graceful RCON shutdown announcement and `exit` command, waits for the database to flush cleanly, and ensures no zombie processes remain. |
| **`💾 Save Config`** | Immediately saves all UI settings and synchronizes `ServerSettings.ini`, `Engine.ini`, `Game.ini`, and `modlist.txt`. |
| **`📦 Hot Backup DB`** | Creates an online SQLite backup (`game.db` + `.ini` configs) in `Backups\` without stopping the server, and prunes backups older than `Keep Backups (Days)`. |
| **`📂 Restore DB`** | Opens a backup selector dialog to restore any `.zip` or `.db` backup (automatically stopping the server and creating a pre-restore safety backup first). |
| **`🛡️ Configure Firewall`** | Automatically creates Windows Firewall inbound rules for UDP (`GamePort`, `RawUdpPort`, `QueryPort`) and TCP (`RconPort`, `WebPagePort`). |
| **`🌐 Steam Workshop Browser`** | Opens the built-in Chromium Steam Workshop browser for 1-click mod installation. |

---

## 6. Mod Management & Steam Workshop Guide

Switch to the **`🧩 Mods & Steam Workshop`** tab (or view the Mod Manager panel) to manage your server's Steam Workshop mods.

### How to Add Mods (3 Easy Methods)

1. **Full-Page Steam Workshop Browser (`🌐 Steam Workshop Browser` button in top header)**:
   - Opens an integrated Steam Workshop browser directly inside the app.
   - Browse or search for any Conan Exiles mod on Steam.
   - When viewing a mod page, click the green **`➕ Install Mod to Server`** button at the top of the browser bar (or right-click any mod link).
   - The mod is immediately added to your server and queued for background download!
2. **Instant Workshop Search Bar**:
   - Type a mod name (e.g., `Pippi`, `EEWA`, `Emberlight`, `LBPR`) into the search box and click **Search**.
   - Click **`➕ Add`** next to any result to add it to your active mod list.
3. **Add by Workshop ID**:
   - Paste one or more numeric Workshop IDs (comma-separated, e.g., `880454836, 1369802940`) into the Mod ID box and click **Add Mod**.

### Understanding Mod Status Badges

Every mod in your list displays a real-time status badge so you never have to guess what is happening:

| Badge | Meaning |
| :--- | :--- |
| **`🟢 Loaded & Active`** | The server is running and `ConanSandbox.log` confirmed this `.pak` file was mounted and loaded without errors. |
| **`📁 Ready on Disk`** | The mod's `.pak` file is downloaded on disk and ready for when the server starts. |
| **`⬇️ Downloading`** | SteamCMD is actively downloading or updating this mod right now. |
| **`🔄 Update Available`** | The background Workshop checker detected a newer version of this mod on Steam Workshop. |
| **`✅ Updated!`** | The mod just finished downloading/updating and has been verified as 100% current with Steam Workshop! *(Automatically returns to normal status after 12 seconds).* |
| **`⏳ Pending Restart`** | This mod was added or updated while the server was already running; restart the server to load it. |
| **`❌ Load Error`** | Unreal Engine reported an error loading this mod in the server log. |
| **`⏸️ Inactive`** | The mod is in your **Inactive / Disabled Mods** list (excluded from the server, but files are kept on disk). |

### Right-Click Mod Context Menu & Pre-Downloading
Right-click any mod in your Active Mods list to:
- **`📥 Pre-Download / Validate Mod Now`**: Runs SteamCMD in the background to download or update just that specific mod and verifies it against Steam Workshop.
- **`🌐 Open Steam Workshop Page`**: Opens the mod's Workshop page to read patch notes or check mod dependencies.
- **`⏸️ Disable Mod (Move to Inactive)`**: Temporarily disables the mod without deleting its files.
- **`🗑️ Remove Mod`**: Removes the mod from your server.

### Inactive / Disabled Mods ("Holding Area")
When a major Conan Exiles patch drops, a single outdated mod can crash your server while you wait for the mod author to update it.
- Instead of deleting the mod and losing track of it, click the **`⏸️` (Disable)** button on the mod.
- The mod is moved to the **Inactive / Disabled Mods** panel at the bottom of the Mods tab and removed from `modlist.txt`.
- Even while disabled, the manager **still checks Steam Workshop for updates** on your inactive mods! Once you see the mod has been updated by its author, simply click **`▶️` (Enable)** to move it back into your active server load order.

---

## 7. Smart Mod Load Order & Auto-Sorting

In Conan Exiles, **mod load order is critical**. Mods lower in the list overwrite overlapping assets or blueprints from mods higher in the list. Improper load order is the #1 cause of missing recipes, broken UI, or server crashes.

### One-Click Auto-Sort (`🪄 Auto-Sort Load Order`)
Click the **`🪄 Auto-Sort Load Order`** button in the Mods tab to automatically classify and sort your installed mods according to the established **8-Tier Conan Exiles Community Load Order Standard**:

1. **Tier 1 — Core Frameworks & UI**: `Pippi` *(loaded early when required by dependent mods)*, `ModControlPanel`, `Tot ! Sudo`, UI frameworks.
2. **Tier 2 — Map Extensions & Dungeons**: Dungeon mods, new biomes, underworld extensions.
3. **Tier 3 — Total Conversions & Major Overhauls**: `Age of Calamitous (AoC)`, `EEWA`, `VAM`, `Underdark`.
4. **Tier 4 — Gameplay Mechanics, Magic & Leveling**: `Kerozards Paragon Leveling`, sorcery, combat & thrall AI mods.
5. **Tier 5 — Building Pieces, Placeables & Decor**: `Emberlight`, `Northern Timber`, `Dude's Delightful Decorations`.
6. **Tier 6 — Cosmetics, Armor, Weapons & Body**: `RA: Character Customisation`, `Iqol`, ` GrimProductions`, armor/weapon packs.
7. **Tier 7 — Building Placement & Admin Rules**: **`Less Building Placement Restrictions (LBPR)`** *(Must load after all building/decor mods!)*, `Pickup+`, `Unlock Plus`.
8. **Tier 8 — Lighting, Weather & Environment**: Weather overhauls, lighting tweaks, post-processing mods.

> **Manual Reordering:** You can also fine-tune any mod's position at any time using the **`▲ Up`** and **`▼ Down`** buttons. Every change is immediately written to `modlist.txt`.

---

## 8. Crash Loop Protection & Mod Diagnostics

If a broken or outdated mod causes the Conan Exiles server to crash repeatedly during startup, an ordinary server wrapper will endlessly restart the server in an infinite loop—overwriting the log file each time and hiding the cause of the crash.

**Conan Enhanced Server Manager includes intelligent Crash Loop Protection:**

1. **Automatic Loop Detection**: If the server crashes **3 times in a row** within 10 minutes of starting, the Watchdog **automatically pauses auto-restarts** so your server doesn't get stuck in an infinite reboot cycle and your crash logs are preserved.
2. **Automated Log Forensics**: Before Unreal Engine can overwrite `ConanSandbox.log`, the manager scans the crash log (`Saved\Logs\ConanSandbox.log` and backup logs) for:
   - Fatal UE4 callstacks (`Fatal error!`, `Access violation`, `Assertion failed`)
   - Failed `ModController` blueprint loads
   - Broken `.pak` mounts or corrupt asset references
   - The last mod initialized right before the crash
3. **Red Crash Loop Alert Banner & One-Click Recovery**:
   - A prominent red alert banner appears at the top of the **Windows App**, **Web Console**, and **Android App** identifying the **Suspected Culprit Mod** (e.g., `SUSPECT: PIPPI - USER & SERVER MANAGEMENT (#880454836)`).
   - Click **`🚫 Disable Offending Mod & Restart`** to automatically disable the culprit mod, re-sync `modlist.txt`, clear the crash state, and restart your server cleanly!

---

## 9. Backups, Database Restore & Direct INI Editing

### Creating a Backup (`📦 Hot Backup DB`)
- You can click **`📦 Hot Backup DB`** at any time—**even while the server is running and players are online!**
- The manager uses SQLite's online backup API to safely snapshot `game.db` (plus your `.ini` configs and `modlist.txt`) into a timestamped `.zip` archive inside the `Backups\` folder without corrupting active transactions or lagging the server.

### Restoring a Backup (`📂 Restore DB`)
1. Stop the server first (`⏹ Stop Server`).
2. Click **`📂 Restore DB`** and select any backup `.zip` file from your `Backups\` directory.
3. Confirm the prompt—your current `game.db` is safely backed up before the selected archive is restored.

### Direct INI Editor (`📝 Direct INI Editor` Tab)
Need to tweak an advanced or mod-specific setting not shown on the main dashboard?
1. Switch to the **`📝 Direct INI Editor`** tab.
2. Select the file you want to edit from the dropdown:
   - `ServerSettings.ini`
   - `Engine.ini`
   - `Game.ini`
   - `modlist.txt`
3. Click **`📂 Load File`**, make your edits in the built-in code editor, and click **`💾 Save File`**.

---

## 10. Live Console, RCON & Player Management

### Live Server Console (`💻 Live Console` Tab)
- Displays real-time output from the Conan Exiles server process and SteamCMD.
- During server or mod downloads, displays a live **Progress Bar** with download percentage, transfer speed (`MB/s`), and estimated time remaining (`ETA`).
- **Bottom RCON Command Bar**: Type any RCON command (such as `broadcast Server restarting in 5 minutes!` or `save`) and press **Enter** or click **Send RCON**.

### Active Players & Administration (`👥 Players & RCON` Tab)
- Shows all currently connected players, their character names, and connection details.
- **Broadcast Message**: Send a popup server announcement to all online players.
- **Kick Player**: Select a player and click **Kick** to remove them from the server via RCON.

---

## 11. Remote Administration (PC Client, Web UI & Android App)

Whenever `ConanServerManager.exe` is running in **Local Server Host** mode, its embedded Web & Remote API server listens on **Port `8088`** (`http://0.0.0.0:8088`).

### Method A: Built-in Web Console (Any Browser)
1. On any computer, phone, or tablet connected to your network, open a web browser.
2. Navigate to:
   ```text
   http://<Your-Server-IP>:8088
   ```
   *(Example: `http://192.168.1.100:8088`)*
3. From the Web Console, you can start/stop/restart the server, monitor CPU/RAM/Players, add/disable/sort mods, change gameplay settings, run hot backups, and send RCON commands.

### Method B: Windows App in "Remote Client Mode"
Want the full Windows desktop application experience on your gaming PC while your server runs on another machine?
1. Copy the `ConanServerManager` folder to your gaming PC and launch `ConanServerManager.exe`.
2. In **Card 4 (Automation & Remote Mode)**, change **Operation Mode** from `Local Server Host` to **`Remote Client Mode`**.
3. Enter your server's IP and port (e.g., `http://192.168.1.100:8088`) and optional Remote API Password, then click **`🔗 Connect`**.
4. The desktop app will synchronize with your remote server—allowing you to edit settings, manage mods, browse the Steam Workshop, and control the server as if you were sitting right in front of it!

### Method C: Android Companion App (`ConanServerManager.apk`)
1. Download the latest **`ConanServerManager-vX.Y.Z.apk`** from the [GitHub Releases Page](https://github.com/Nakrom75/ConanEnhancedServerManager/releases) (or directly from your server's Web Console at `http://<Server-IP>:8088/api/update/apk`) onto your Android phone or tablet.
2. Install and open **Conan Server Manager** on Android.
3. Tap the **Server Selector** bar at the top of the screen:
   - Tap **`🔍 Scan LAN`** to automatically discover your Conan Server Manager on your local Wi-Fi network, **or**
   - Type your server's address (`http://192.168.1.100:8088` or your DDNS/VPN address) and tap **Connect Server**.
4. **Android App Navigation Tabs**:
   - **📊 Dashboard**: Live server status, Steam Online badge, CPU/RAM/Uptime metrics, Start / Quick Start / Restart / Stop / Hot Backup controls, and Crash Loop recovery banner.
   - **🧩 Mods**: View live mod statuses (`🟢 Loaded`, `🔄 Update Available`, `✅ Updated!`), search Steam Workshop, open the built-in Workshop browser, Auto-Sort load order, reorder mods (`▲`/`▼`), disable/enable mods (`⏸️`/`▶️`), or **long-press any mod** to pre-download/validate it on the server.
   - **⚙️ Settings**: Adjust server name, passwords, ports, map, PvE/PvP mode, and multipliers remotely.
   - **👥 Players**: View online players, broadcast messages, or kick players.
   - **💻 Console**: Live scrolling server logs and RCON command input.
   - **✨ Updates**: One-tap check for new app releases with built-in OTA APK download and installation.

---

## 12. Troubleshooting & Frequently Asked Questions (FAQ)

### Q1: My server says `🟢 STEAM: ONLINE`, and friends can join, but I can't see it in my own in-game server list on the same network!
- **Why this happens**: Many home routers do not support **NAT Loopback (Hairpin NAT)**, meaning your router blocks packets that go out to your public IP and loop right back into your own home network.
- **Solution**:
  1. In Conan Exiles, click **Direct Connect** at the bottom of the server browser.
  2. Enter your server's **Local LAN IP and Game Port** (e.g., `192.168.1.100:7777` — or `127.0.0.1:7777` if playing on the same PC as the server).
  3. External players outside your home will still see your server normally in the public server list.

### Q2: How do I remove the server join password to make my server Public?
- Simply clear the **Server Join Password** box in **Card 1** so it is completely empty, then click **`💾 Save Config & Sync INI`** (or restart the server).
- The manager automatically strips old password entries from all sections of `ServerSettings.ini` and `Engine.ini` so Conan Exiles will not ask players for a password.

### Q3: A mod updated on Steam Workshop—how do I update it on my server?
- **If the server is running**: Click **`🔄 Restart & Update`** to gracefully announce a restart, update all mods via SteamCMD, verify them against Steam Workshop, and restart the server.
- **If you want to pre-download a single mod**: Right-click the mod in the Mods list (or long-press it on Android) and select **`📥 Pre-Download / Validate Mod Now`**. Once the download completes, the mod badge will briefly turn **`✅ Updated!`** to confirm it matches the latest Steam Workshop version.

### Q4: How do I update Conan Enhanced Server Manager itself when a new version comes out?
- **Windows**: When a new release is published on GitHub, an **Update Banner** appears at the top of the app. Click **Update Now** for an automatic in-place self-update, or download the latest `.zip` from GitHub and extract it over your existing folder (your `manager_config.json`, `ConanExilesServer\`, and `Backups\` are preserved).
- **Android**: Open the **✨ Updates** tab in the Android app and tap **`🔄 Check for App Updates`**, then tap **`📥 Download & Install APK Update`**.
