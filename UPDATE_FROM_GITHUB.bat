@echo off
setlocal EnableDelayedExpansion
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

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$ErrorActionPreference = 'Stop'; " ^
    "$targetDir = '%~dp0'.TrimEnd('\'); " ^
    "if (Test-Path (Join-Path $targetDir 'ServerManager\ConanServerManager.exe')) { " ^
    "    if (-not (Test-Path (Join-Path $targetDir 'ConanServerManager.exe'))) { " ^
    "        $targetDir = Join-Path $targetDir 'ServerManager'; " ^
    "    } " ^
    "} " ^
    "Write-Host ('[1/5] Checking running ConanServerManager.exe processes...') -ForegroundColor Cyan; " ^
    "Get-Process -Name 'ConanServerManager' -ErrorAction SilentlyContinue | ForEach-Object { " ^
    "    Write-Host ('      Stopping ConanServerManager.exe (PID ' + $_.Id + ')...') -ForegroundColor Yellow; " ^
    "    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue; " ^
    "}; " ^
    "Start-Sleep -Seconds 1; " ^
    "Write-Host '[2/5] Querying GitHub Releases API (Nakrom75/ConanEnhancedServerManager)...' -ForegroundColor Cyan; " ^
    "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; " ^
    "$headers = @{ 'User-Agent' = 'ConanEnhancedServerManager-Updater' }; " ^
    "$releases = Invoke-RestMethod -Uri 'https://api.github.com/repos/Nakrom75/ConanEnhancedServerManager/releases?per_page=10' -Headers $headers; " ^
    "$selectedRelease = $null; " ^
    "$selectedAsset = $null; " ^
    "foreach ($rel in $releases) { " ^
    "    if ($rel.assets) { " ^
    "        foreach ($asset in $rel.assets) { " ^
    "            if ($asset.name -like '*.zip') { " ^
    "                $selectedRelease = $rel; " ^
    "                $selectedAsset = $asset; " ^
    "                break; " ^
    "            } " ^
    "        } " ^
    "    } " ^
    "    if ($selectedAsset) { break; } " ^
    "} " ^
    "if (-not $selectedAsset) { " ^
    "    Write-Host '      No .zip asset found on recent GitHub Releases.' -ForegroundColor Yellow; " ^
    "    if ((Test-Path (Join-Path '%~dp0' '.git')) -and (Get-Command git -ErrorAction SilentlyContinue) -and (Get-Command dotnet -ErrorAction SilentlyContinue)) { " ^
    "        Write-Host '      Git + .NET SDK detected! Pulling latest main branch and compiling locally...' -ForegroundColor Green; " ^
    "        Set-Location '%~dp0'; " ^
    "        git pull origin main; " ^
    "        dotnet publish src/ConanServerManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o ServerManager; " ^
    "        Write-Host '      Local build complete!' -ForegroundColor Green; " ^
    "        exit 0; " ^
    "    } else { " ^
    "        throw 'No .zip release asset found on GitHub Releases. Please upload ConanServerManager_vX.Y.Z.zip to the latest GitHub Release first.'; " ^
    "    } " ^
    "} " ^
    "$tag = $selectedRelease.tag_name; " ^
    "$zipName = $selectedAsset.name; " ^
    "$downloadUrl = $selectedAsset.browser_download_url; " ^
    "Write-Host ('      Found Release: ' + $tag + ' (' + $zipName + ')') -ForegroundColor Green; " ^
    "$tempZip = Join-Path $env:TEMP ('ConanServerManager_Update_' + $tag + '.zip'); " ^
    "$tempExtract = Join-Path $env:TEMP ('ConanServerManager_Extract_' + $tag); " ^
    "if (Test-Path $tempZip) { Remove-Item $tempZip -Force; } " ^
    "if (Test-Path $tempExtract) { Remove-Item $tempExtract -Recurse -Force; } " ^
    "Write-Host ('[3/5] Downloading ' + $zipName + ' from GitHub...') -ForegroundColor Cyan; " ^
    "Invoke-WebRequest -Uri $downloadUrl -Headers $headers -OutFile $tempZip; " ^
    "Write-Host '[4/5] Extracting update archive...' -ForegroundColor Cyan; " ^
    "Expand-Archive -Path $tempZip -DestinationPath $tempExtract -Force; " ^
    "$sourceFolder = $tempExtract; " ^
    "if ((Test-Path (Join-Path $tempExtract 'ServerManager\ConanServerManager.exe')) -and -not (Test-Path (Join-Path $tempExtract 'ConanServerManager.exe'))) { " ^
    "    $sourceFolder = Join-Path $tempExtract 'ServerManager'; " ^
    "} " ^
    "Write-Host ('[5/5] Installing updated files into: ' + $targetDir) -ForegroundColor Cyan; " ^
    "$preserveFiles = @('manager_config.json', 'ceadmin_store.json'); " ^
    "$backups = @{}; " ^
    "foreach ($pf in $preserveFiles) { " ^
    "    $pfPath = Join-Path $targetDir $pf; " ^
    "    if (Test-Path $pfPath) { $backups[$pf] = Get-Content $pfPath -Raw; } " ^
    "} " ^
    "Copy-Item -Path (Join-Path $sourceFolder '*') -Destination $targetDir -Recurse -Force; " ^
    "foreach ($pf in $backups.Keys) { " ^
    "    Set-Content -Path (Join-Path $targetDir $pf) -Value $backups[$pf] -Encoding UTF8; " ^
    "} " ^
    "Remove-Item $tempZip -Force -ErrorAction SilentlyContinue; " ^
    "Remove-Item $tempExtract -Recurse -Force -ErrorAction SilentlyContinue; " ^
    "Write-Host '' ; " ^
    "Write-Host ('SUCCESS! Conan Enhanced Server Manager updated to ' + $tag + '!') -ForegroundColor Green; " ^
    "$exePath = Join-Path $targetDir 'ConanServerManager.exe'; " ^
    "if (Test-Path $exePath) { " ^
    "    Write-Host 'Launching ConanServerManager.exe...' -ForegroundColor Cyan; " ^
    "    Start-Process -FilePath $exePath -WorkingDirectory $targetDir; " ^
    "}"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Update failed. See error message above.
    pause
    exit /b 1
)

echo.
timeout /t 5
exit /b 0
