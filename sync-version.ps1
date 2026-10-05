<#
.SYNOPSIS
    Conan Enhanced Server Manager - Unified Version Synchronization & Build Script
    Synchronizes version numbers across Windows Server Manager (C#), Embedded Web Console,
    and Android Companion App, then builds and packages release deliverables.

.PARAMETER NewVersion
    Optional new SemVer version string (e.g. "1.1.0" or "1.2.0").
    If omitted, uses the current version from version.txt.

.EXAMPLE
    .\sync-version.ps1 -NewVersion "1.2.0"
    .\sync-version.ps1
#>

param(
    [Parameter(Position=0)]
    [string]$NewVersion = ""
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
if (-not $root) { $root = Get-Location }

# 1. Resolve & Validate Version
$versionFilePath = Join-Path $root "version.txt"
if ($NewVersion) {
    $NewVersion = $NewVersion.Trim().TrimStart('v')
    if ($NewVersion -notmatch '^\d+\.\d+\.\d+$') {
        Write-Error "Invalid version format '$NewVersion'. Must be SemVer (e.g. 1.1.0)."
        exit 1
    }
    Set-Content -Path $versionFilePath -Value $NewVersion -NoNewline
    Write-Host "[1/6] Updated version.txt -> $NewVersion" -ForegroundColor Green
}

if (-not (Test-Path $versionFilePath)) {
    Set-Content -Path $versionFilePath -Value "1.1.0" -NoNewline
}
$version = (Get-Content $versionFilePath).Trim()

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Conan Server Manager - Unified Release Sync (v$version)" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 2. Build & Publish C# Windows Server Manager
Write-Host "`n[2/6] Building and publishing C# Windows Manager (win-x64)..." -ForegroundColor Yellow
$dotnetArgs = @("publish", "src/ConanServerManager.csproj", "-c", "Release", "-r", "win-x64", "--self-contained", "true", "-o", "ServerManager/")
& dotnet @dotnetArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}
Write-Host "      Done. Output: ServerManager/" -ForegroundColor Green

# 3. Build & Sign Android Release APK
Write-Host "`n[3/6] Building signed Android Release APK with Gradle..." -ForegroundColor Yellow
$env:JAVA_HOME = "C:\Program Files\Microsoft\jdk-17.0.20.101-hotspot"
$env:ANDROID_HOME = "C:\Android\android-sdk"
$gradlePath = "C:\Gradle\gradle-8.5\bin\gradle.bat"

Push-Location (Join-Path $root "android")
try {
    & $gradlePath assembleRelease
    if ($LASTEXITCODE -ne 0) {
        Write-Error "gradle assembleRelease failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
} finally {
    Pop-Location
}

$apkSource = Join-Path $root "android\app\build\outputs\apk\release\ConanServerManager-v$version.apk"
if (-not (Test-Path $apkSource)) {
    # Fallback to app-release.apk if custom outputFileName was skipped
    $apkSource = Join-Path $root "android\app\build\outputs\apk\release\app-release.apk"
}

if (-not (Test-Path $apkSource)) {
    Write-Error "Compiled APK not found at: $apkSource"
    exit 1
}
Write-Host "      Done. Generated APK: $apkSource" -ForegroundColor Green

# 4. Deploy APK to Deployment Folders & Clean Obsolete APKs
Write-Host "`n[4/6] Synchronizing APK across deployment targets..." -ForegroundColor Yellow
$targetApkName = "ConanServerManager-v$version.apk"
$destinations = @(
    (Join-Path $root $targetApkName),
    (Join-Path $root "ServerManager\$targetApkName"),
    (Join-Path $root "ConanServerManager_DeployPackage\$targetApkName")
)

# Remove older version APKs from destinations
Get-ChildItem -Path $root, (Join-Path $root "ServerManager"), (Join-Path $root "ConanServerManager_DeployPackage") -Filter "ConanServerManager-v*.apk" -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -ne $targetApkName } |
    Remove-Item -Force -ErrorAction SilentlyContinue

# Copy new APK
foreach ($dest in $destinations) {
    $parent = Split-Path $dest -Parent
    if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    Copy-Item $apkSource $dest -Force
    Write-Host "      -> Deployed to: $dest" -ForegroundColor Gray
}

# Ensure app.png and app.ico are copied
Copy-Item (Join-Path $root "src\app.png") (Join-Path $root "ServerManager\app.png") -Force
Copy-Item (Join-Path $root "src\app.png") (Join-Path $root "ConanServerManager_DeployPackage\app.png") -Force

# 5. Sync ServerManager to ConanServerManager_DeployPackage
Write-Host "`n[5/6] Syncing published binaries to DeployPackage..." -ForegroundColor Yellow
Copy-Item -Path (Join-Path $root "ServerManager\*") -Destination (Join-Path $root "ConanServerManager_DeployPackage\") -Recurse -Force

# 6. Compress Release Zip Packages
Write-Host "`n[6/6] Generating Release Zip Packages..." -ForegroundColor Yellow
$zipVersionPath = Join-Path $root "ConanServerManager_v$version.zip"
$zipDeployPath = Join-Path $root "ConanServerManager_DeployPackage.zip"

Compress-Archive -Path (Join-Path $root "ServerManager\*") -DestinationPath $zipVersionPath -Force
Compress-Archive -Path (Join-Path $root "ConanServerManager_DeployPackage\*") -DestinationPath $zipDeployPath -Force

Write-Host "      -> Created: $zipVersionPath" -ForegroundColor Gray
Write-Host "      -> Created: $zipDeployPath" -ForegroundColor Gray

# Verification Summary
Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "  Unified Synchronization Succeeded: v$version" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
$dllVer = (Get-Item (Join-Path $root "ServerManager\ConanServerManager.dll")).VersionInfo.FileVersion
$apkInfo = Get-Item (Join-Path $root $targetApkName)
Write-Host "  Central Version File : version.txt ($version)"
Write-Host "  C# Binary Version    : ConanServerManager.dll ($dllVer)"
Write-Host "  Android Mobile APK   : $targetApkName ($([math]::Round($apkInfo.Length/1MB, 2)) MB)"
Write-Host "  Full Release Zip     : ConanServerManager_v$version.zip"
Write-Host "  Deploy Package Zip   : ConanServerManager_DeployPackage.zip"
Write-Host "==========================================================`n" -ForegroundColor Green
