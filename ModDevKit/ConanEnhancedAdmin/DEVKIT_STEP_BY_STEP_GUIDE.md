# Conan Enhanced Admin Mod (`ConanEnhancedAdmin`) — DevKit Step-by-Step Setup Guide

Once your **Conan Exiles DevKit** download finishes in the Epic Games Launcher, follow these steps to initialize the mod project so we can wire Phase 1 (`Admin Panel + Warps + /home + Ranks`).

---

## Step 1: Launch the DevKit & Create the Mod
1. Open the **Epic Games Launcher**, go to **Library**, and launch **Conan Exiles DevKit**. *(Note: First launch takes 5–10 minutes while Unreal Engine 4.15 compiles base shaders).*
2. On the top toolbar of the DevKit editor, click the **`Steam` / `DevKit` (Mod Manager)** icon.
3. Click **`Create New Mod`** and enter:
   - **Mod Name**: `ConanEnhancedAdmin`
   - **Author**: Your name / handle
   - **Description**: `Modern Server Management, Warps, Homes, Kits, Chat, Clan Inspector & Economy for Conan Exiles Enhanced.`
4. Click **Create**. This creates the active mod folder at:
   `...\ConanExilesDevKit\Games\ConanSandbox\Content\Mods\ConanEnhancedAdmin\`

---

## Step 2: Create the 3 Data Structs in Content Browser
Inside your `Content\Mods\ConanEnhancedAdmin\` folder in the DevKit Content Browser, right-click → **Blueprints → Structure** and create these 3 Structs (matching our pre-built JSON files in `ModDevKit\ConanEnhancedAdmin\DataTables\`):

### 1. `ST_CEAdmin_Rank`
| Variable Name | UE4 Type | Default Value |
| :--- | :--- | :--- |
| `RankId` | `String` | `"Player"` |
| `DisplayName` | `String` | `"Exile"` |
| `ChatPrefix` | `String` | `""` |
| `ChatColorHex` | `String` | `"#E2E8F0"` |
| `Priority` | `Integer` | `10` |
| `CanUseAdminPanel` | `Boolean` | `False` |
| `CanTeleportToPlayers` | `Boolean` | `False` |
| `CanSummonPlayers` | `Boolean` | `False` |
| `CanKickBan` | `Boolean` | `False` |
| `CanManageWarps` | `Boolean` | `False` |
| `CanManageKits` | `Boolean` | `False` |
| `CanBypassCooldowns` | `Boolean` | `False` |
| `MaxHomesAllowed` | `Integer` | `1` |
| `HomeCooldownSeconds` | `Integer` | `600` |
| `WarpCooldownSeconds` | `Integer` | `120` |
| `PlaytimePayMultiplier` | `Float` | `1.0` |

### 2. `ST_CEAdmin_Warp`
| Variable Name | UE4 Type | Default Value |
| :--- | :--- | :--- |
| `WarpId` | `String` | `"hub"` |
| `DisplayName` | `String` | `"Hub"` |
| `Category` | `String` | `"General"` |
| `RequiredRank` | `String` | `"Player"` |
| `CostCopper` | `Integer` | `0` |
| `CooldownSeconds` | `Integer` | `120` |
| `LocationX` | `Float` | `0.0` |
| `LocationY` | `Float` | `0.0` |
| `LocationZ` | `Float` | `0.0` |
| `RotationYaw` | `Float` | `0.0` |
| `Description` | `String` | `""` |

### 3. `ST_CEAdmin_Kit`
| Variable Name | UE4 Type | Default Value |
| :--- | :--- | :--- |
| `KitId` | `String` | `"starter"` |
| `DisplayName` | `String` | `"Starter Kit"` |
| `RequiredRank` | `String` | `"Player"` |
| `OneTimeOnly` | `Boolean` | `True` |
| `CooldownSeconds` | `Integer` | `0` |
| `CostCopper` | `Integer` | `0` |
| `ItemsJson` | `String` | `"[]"` |
| `Description` | `String` | `""` |

---

## Step 3: Import the Pre-Built JSON DataTables (1 Click Each)
1. Drag and drop the 3 `.json` files from `F:\Projects\Conan Exiles Dedicated Server\ModDevKit\ConanEnhancedAdmin\DataTables\` directly into your DevKit Content Browser:
   - `DT_CEAdmin_DefaultRanks.json` → Choose Row Type: **`ST_CEAdmin_Rank`**
   - `DT_CEAdmin_SampleWarps.json` → Choose Row Type: **`ST_CEAdmin_Warp`**
   - `DT_CEAdmin_SampleKits.json` → Choose Row Type: **`ST_CEAdmin_Kit`**

---

## Step 4: Create the ModController & Player Component
1. In the DevKit top **Mod Manager** window, click **`Create ModController`** and name it **`BP_CEAdmin_ModController`**.
2. Right-click in your mod folder → **Blueprint Class** → choose **`ActorComponent`** and name it **`BP_CEAdmin_PlayerComponent`**.
3. Tell me as soon as you reach this point (or as soon as the DevKit finishes installing) and I will give you the exact Blueprint graph nodes and wire up the **Server Manager integration**!
