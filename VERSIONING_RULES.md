# Conan Server Manager - Versioning & Dual-Platform Synchronization Rules

> **Status:** MANDATORY STRICT OPERATIONAL POLICY  
> **Applicable To:** All developers, contributors, and AI pair programmers working on the Conan Server Manager project.

---

## 1. Core Mandates

1. **INCREMENT ON EVERY COMPILE / BUILD ITERATION**:
   - On **every single build, compile, or feature addition**, the version number **MUST** be incremented.
   - Code changes that are built or packaged without bumping the version number are strictly prohibited.

2. **PERFECT DUAL-PLATFORM SYNCHRONIZATION**:
   - The **Windows Desktop Host Application** and the **Android Companion Mobile Application** must **ALWAYS** remain synchronized at the exact same version number and version code.
   - Under no circumstances may the Windows app be compiled or released with a version number that differs from the Android app, or vice versa. Both platforms must be compiled and verified together.

3. **ABSOLUTE PROHIBITION ON REMOTE / LIVE SERVER DEPLOYMENT**:
   - **NEVER** copy, upload, or deploy files, binaries, archives, or APKs directly to `\\192.168.0.5\ConanServerManager\` or any remote server.
   - All builds, binaries, packages, and APKs must remain strictly within the local development workspace (`F:\Projects\Conan Exiles Dedicated Server`). The user will handle deployments to the live server manually.

---

## 2. Version Numbering Scheme & Formulas

### 2.1 Semantic Version String
- Format: `MAJOR.MINOR.PATCH` (e.g. `1.2.1`)
  - **MAJOR**: Structural architectural redesigns or breaking protocol overhauls.
  - **MINOR**: Significant new features, new integrations, or major interface expansions.
  - **PATCH**: Incremental iterations, layout refinements, bug fixes, UI adjustments, and compile passes.

### 2.2 Android Version Code Formula
Android package management requires a monotonically increasing integer `versionCode`.
- Formula:
  $$\text{versionCode} = (\text{MAJOR} \times 10000) + (\text{MINOR} \times 100) + \text{PATCH}$$
- Examples:
  - `v1.1.12` $\rightarrow$ $1 \times 10000 + 1 \times 100 + 12 = 10112$
  - `v1.2.0` $\rightarrow$ $1 \times 10000 + 2 \times 100 + 0 = 10200$
  - `v1.2.1` $\rightarrow$ $1 \times 10000 + 2 \times 100 + 1 = 10201$
  - `v1.3.0` $\rightarrow$ $1 \times 10000 + 3 \times 100 + 0 = 10300$

---

## 3. The 7 Mandatory Synchronization Points

Before executing any build or compile, the following **7 files** must be updated simultaneously:

| # | File Path | Setting / Location | Expected Value Format |
|---|---|---|---|
| **1** | `version.txt` | Entire file contents (Line 1) | `MAJOR.MINOR.PATCH` (e.g. `1.2.1`) |
| **2** | `src/ServerEngine.cs` | `GetAppVersion()` fallback return | `"{MAJOR}.{MINOR}.{PATCH}"` |
| **3** | `src/MainWindow.xaml` | `TxtAppHeaderTitle` & `TxtAppHeaderVersionBadge` | `"Conan Enhanced Server Manager v{M.m.p}"` & `"✨ v{M.m.p}"` |
| **4** | `android/app/build.gradle` | Fallback in `getAppVersionName()` & `getAppVersionCode()` | `"{MAJOR}.{MINOR}.{PATCH}"` & `{VERSION_CODE}` |
| **5** | `android/app/src/main/assets/app.js` | Top constant `let APP_VERSION` | `"{MAJOR}.{MINOR}.{PATCH}"` |
| **6** | `android/app/src/main/assets/index.html` | `#appInstalledVersion` badge | `"v{MAJOR}.{MINOR}.{PATCH}"` |
| **7** | `android/app/src/main/java/com/conan/servermanager/MainActivity.java` | Fallback in `getAppVersion()` & `getAppVersionCode()` | `"{MAJOR}.{MINOR}.{PATCH}"` & `{VERSION_CODE}` |

---

## 4. Standard Build & Packaging Workflow

Whenever a compile or build is performed, follow this strict 5-step sequence:

### Step 1: Bump Version Across All 7 Files
Update all 7 files listed in Section 3 to the new synchronized version and version code.

### Step 2: Build & Publish Windows Desktop Application
```powershell
# 1. Verify build
dotnet build src/ConanServerManager.csproj -c Release

# 2. Publish self-contained win-x64 binary to local ServerManager/
dotnet publish src/ConanServerManager.csproj -c Release -r win-x64 --self-contained true -o ServerManager/

# 3. Create release archives locally
Compress-Archive -Path "ServerManager\*" -DestinationPath "ConanServerManager_v{MAJOR}.{MINOR}.{PATCH}.zip" -Force
Copy-Item "ConanServerManager_v{MAJOR}.{MINOR}.{PATCH}.zip" "ConanServerManager_DeployPackage.zip" -Force
```

### Step 3: Build Android Companion Application
```powershell
$env:JAVA_HOME = "C:\Program Files\Microsoft\jdk-17.0.20.101-hotspot"
$env:ANDROID_HOME = "C:\Android\android-sdk"
& "C:\Gradle\gradle-8.5\bin\gradle.bat" -p android assembleRelease

# Copy signed release APK to root
Copy-Item "android\app\build\outputs\apk\release\ConanServerManager-v{MAJOR}.{MINOR}.{PATCH}.apk" "ConanServerManager-v{MAJOR}.{MINOR}.{PATCH}.apk" -Force
```

### Step 4: Update Documentation
- Update `SOURCE_OF_TRUTH.md`:
  - Update top document version metadata.
  - Add milestone/changelog section documenting changes made under this version.

### Step 5: Git Commit & Push
```powershell
git add .
git commit -m "v{MAJOR}.{MINOR}.{PATCH}: <description of changes>"
git push origin main
```

### Step 6: Create & Push Git Tag to GitHub (MANDATORY)
```powershell
git tag -a v{MAJOR}.{MINOR}.{PATCH} -m "Release v{MAJOR}.{MINOR}.{PATCH}: <description>"
git push origin v{MAJOR}.{MINOR}.{PATCH}
```
*Never skip this step: The git tag is required for the user to upload release packages and publish GitHub Releases.*
