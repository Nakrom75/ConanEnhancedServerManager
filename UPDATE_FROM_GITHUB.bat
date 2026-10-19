@echo off
title Conan Enhanced Server Manager - Emergency GitHub Updater

echo ============================================================================
echo   CONAN ENHANCED SERVER MANAGER - EMERGENCY GITHUB UPDATER ^& RECOVERY
echo ============================================================================
echo.
echo   Repository: https://github.com/Nakrom75/ConanEnhancedServerManager
echo   Target Dir: %~dp0
echo.
echo   NOTE: Any running Conan Exiles Dedicated Server (ConanSandboxServer)
echo         will NOT be stopped. Only ConanServerManager.exe will be updated.
echo ============================================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -Command "$raw = Get-Content -LiteralPath '%~f0' -Raw; $marker = '# === PS_START ==='; $idx = $raw.LastIndexOf($marker); if ($idx -lt 0) { throw 'Script marker not found' }; $scriptBlock = [ScriptBlock]::Create($raw.Substring($idx)); & $scriptBlock '%~dp0'"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Update failed. See error message above.
    pause
    exit /b 1
)

echo.
timeout /t 5
exit /b 0

# === PS_START ===
param([string]$BaseBatchDir)
$ErrorActionPreference = 'Stop'

$rootDir = $BaseBatchDir.TrimEnd('\', '/')
$targetDir = $rootDir

if ((Test-Path (Join-Path $rootDir 'ServerManager\ConanServerManager.exe')) -and -not (Test-Path (Join-Path $rootDir 'ConanServerManager.exe'))) {
    $targetDir = Join-Path $rootDir 'ServerManager'
}

Write-Host '[1/5] Checking running ConanServerManager.exe processes...' -ForegroundColor Cyan
Get-Process -Name 'ConanServerManager' -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host ("      Stopping ConanServerManager.exe (PID " + $_.Id + ")...") -ForegroundColor Yellow
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
}
Start-Sleep -Seconds 1

Write-Host '[2/5] Querying GitHub Releases & Tags (Nakrom75/ConanEnhancedServerManager)...' -ForegroundColor Cyan
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$headers = @{ 'User-Agent' = 'ConanEnhancedServerManager-Updater' }

$releases = @()
try {
    $releases = Invoke-RestMethod -Uri 'https://api.github.com/repos/Nakrom75/ConanEnhancedServerManager/releases?per_page=10' -Headers $headers
} catch {
    Write-Host ("      GitHub Releases query note: " + $_.Exception.Message) -ForegroundColor Yellow
}

$selectedRelease = $null
$selectedAsset = $null

foreach ($rel in $releases) {
    if ($rel.assets) {
        foreach ($asset in $rel.assets) {
            if (($asset.name -like 'ConanServerManager_v*.zip') -and ($asset.name -notlike '*linux*')) {
                $selectedRelease = $rel
                $selectedAsset = $asset
                break
            }
        }
        if (-not $selectedAsset) {
            foreach ($asset in $rel.assets) {
                if (($asset.name -like '*.zip') -and ($asset.name -notlike '*linux*')) {
                    $selectedRelease = $rel
                    $selectedAsset = $asset
                    break
                }
            }
        }
    }
    if ($selectedAsset) { break }
}

# Check if we are in a Git + .NET development clone where main/tags are newer than the latest uploaded Release ZIP
$isGitDevRepo = (Test-Path (Join-Path $rootDir '.git')) -and ($null -ne (Get-Command git -ErrorAction SilentlyContinue)) -and ($null -ne (Get-Command dotnet -ErrorAction SilentlyContinue))
$useGitBuild = $false

if ($isGitDevRepo) {
    try {
        $tags = Invoke-RestMethod -Uri 'https://api.github.com/repos/Nakrom75/ConanEnhancedServerManager/tags?per_page=1' -Headers $headers
        if ($tags -and $tags.Count -gt 0) {
            $latestTagVerStr = $tags[0].name.TrimStart('v', 'V')
            $relVerStr = if ($selectedRelease) { $selectedRelease.tag_name.TrimStart('v', 'V') } else { '0.0.0' }
            $latestTagVer = $null
            $relVer = $null
            if ([Version]::TryParse($latestTagVerStr, [ref]$latestTagVer) -and [Version]::TryParse($relVerStr, [ref]$relVer)) {
                if ($latestTagVer -gt $relVer) {
                    Write-Host ("      Latest Git tag v" + $latestTagVerStr + " is newer than latest Release ZIP (v" + $relVerStr + "). Using Git + .NET build...") -ForegroundColor Cyan
                    $useGitBuild = $true
                }
            }
        }
    } catch { }
}

if ((-not $selectedAsset) -or $useGitBuild) {
    if ($isGitDevRepo) {
        Write-Host '[3/5] Pulling latest main branch from GitHub and compiling locally...' -ForegroundColor Green
        Set-Location $rootDir
        git pull origin main
        dotnet publish src/ConanServerManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o ServerManager
        if (Test-Path (Join-Path $rootDir 'ServerManager\ConanServerManager.exe')) {
            Copy-Item (Join-Path $rootDir 'ServerManager\ConanServerManager.exe') (Join-Path $rootDir 'ConanServerManager.exe') -Force -ErrorAction SilentlyContinue
            Copy-Item (Join-Path $rootDir 'ServerManager\ConanServerManager.dll') (Join-Path $rootDir 'ConanServerManager.dll') -Force -ErrorAction SilentlyContinue
            Copy-Item (Join-Path $rootDir 'ServerManager\ConanServerManager.pdb') (Join-Path $rootDir 'ConanServerManager.pdb') -Force -ErrorAction SilentlyContinue
        }
        Write-Host ''
        Write-Host 'SUCCESS: Local Git pull and build completed!' -ForegroundColor Green
        $exePath = Join-Path $targetDir 'ConanServerManager.exe'
        if (Test-Path $exePath) {
            Write-Host 'Launching ConanServerManager.exe...' -ForegroundColor Cyan
            Start-Process -FilePath $exePath -WorkingDirectory $targetDir
        }
        exit 0
    } else {
        throw 'No .zip release asset found on GitHub Releases. Please upload ConanServerManager_vX.Y.Z.zip to the latest GitHub Release first.'
    }
}

$tag = $selectedRelease.tag_name
$zipName = $selectedAsset.name
$downloadUrl = $selectedAsset.browser_download_url
Write-Host ("      Found Release: " + $tag + " (" + $zipName + ")") -ForegroundColor Green

$tempZip = Join-Path $env:TEMP ("ConanServerManager_Update_" + $tag + ".zip")
$tempExtract = Join-Path $env:TEMP ("ConanServerManager_Extract_" + $tag)
if (Test-Path $tempZip) { Remove-Item $tempZip -Force }
if (Test-Path $tempExtract) { Remove-Item $tempExtract -Recurse -Force }

Write-Host ("[3/5] Downloading " + $zipName + " from GitHub...") -ForegroundColor Cyan
Invoke-WebRequest -Uri $downloadUrl -Headers $headers -OutFile $tempZip

Write-Host '[4/5] Extracting update archive...' -ForegroundColor Cyan
Expand-Archive -Path $tempZip -DestinationPath $tempExtract -Force

$sourceFolder = $tempExtract
if ((Test-Path (Join-Path $tempExtract 'ServerManager\ConanServerManager.exe')) -and -not (Test-Path (Join-Path $tempExtract 'ConanServerManager.exe'))) {
    $sourceFolder = Join-Path $tempExtract 'ServerManager'
}

Write-Host ("[5/5] Installing updated files into: " + $targetDir) -ForegroundColor Cyan
$preserveFiles = @('manager_config.json', 'ceadmin_store.json')
$backups = @{}
foreach ($pf in $preserveFiles) {
    $pfPath = Join-Path $targetDir $pf
    if (Test-Path $pfPath) {
        $backups[$pf] = Get-Content -LiteralPath $pfPath -Raw
    }
}

Copy-Item -Path (Join-Path $sourceFolder '*') -Destination $targetDir -Recurse -Force

$subDir = Join-Path $rootDir 'ServerManager'
if (($targetDir -eq $rootDir) -and (Test-Path (Join-Path $subDir 'ConanServerManager.exe'))) {
    Copy-Item -Path (Join-Path $sourceFolder '*') -Destination $subDir -Recurse -Force
}

foreach ($pf in $backups.Keys) {
    Set-Content -LiteralPath (Join-Path $targetDir $pf) -Value $backups[$pf] -Encoding UTF8
}

Remove-Item $tempZip -Force -ErrorAction SilentlyContinue
Remove-Item $tempExtract -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ''
Write-Host ("SUCCESS: Conan Enhanced Server Manager updated to " + $tag) -ForegroundColor Green

$exePath = Join-Path $targetDir 'ConanServerManager.exe'
if (Test-Path $exePath) {
    Write-Host 'Launching ConanServerManager.exe...' -ForegroundColor Cyan
    Start-Process -FilePath $exePath -WorkingDirectory $targetDir
}
