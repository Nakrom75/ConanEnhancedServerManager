# Conan Server Manager - AI Pair Programming Operational Rules

## STRICT MANDATORY RULES

### 1. VERSION NUMBERING MUST BE INCREMENTED ON EVERY SINGLE COMPILE / BUILD
- Whenever a build, compile, bug fix, or feature is implemented, the version number **MUST ALWAYS BE INCREMENTED**.
- **BOTH Windows and Android applications MUST ALWAYS be synchronized at the EXACT same version number and version code.**
- Never compile or release one platform without updating and compiling the other.
- The 7 synchronization files:
  1. `version.txt`
  2. `src/ServerEngine.cs` (fallback in `GetAppVersion()`)
  3. `src/MainWindow.xaml` (`TxtAppHeaderTitle` & `TxtAppHeaderVersionBadge`)
  4. `android/app/build.gradle` (fallback in `getAppVersionName()` & `getAppVersionCode()`)
  5. `android/app/src/main/assets/app.js` (`let APP_VERSION`)
  6. `android/app/src/main/assets/index.html` (`#appInstalledVersion`)
  7. `android/app/src/main/java/com/conan/servermanager/MainActivity.java` (fallback in `getAppVersion()` & `getAppVersionCode()`)
- Version Code Formula: `MAJOR * 10000 + MINOR * 100 + PATCH`.
- See `VERSIONING_RULES.md` for full details.

### 2. ABSOLUTE ZERO REMOTE DEPLOYMENT RULE
- **UNDER NO CIRCUMSTANCES should you ever deploy, copy, write, or touch the live server (`\\192.168.0.5\ConanServerManager\` or any remote server).**
- All builds, binaries, packages, and APKs must remain strictly local within the development workspace `F:\Projects\Conan Exiles Dedicated Server`. The user will handle all updates and deployments manually.

### 3. ALWAYS KEEP `SOURCE_OF_TRUTH.md` UP TO DATE
- On every version increment and architectural change, document the changes in `SOURCE_OF_TRUTH.md`.
