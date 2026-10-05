// Conan Server Manager - Android Remote Client Engine
let currentServerUrl = "http://192.168.1.100:8088";
let currentTab = "dashboard";
let pollTimer = null;
let installedMods = [];
let savedServers = [];
let latestApkUrl = "";
let latestApkVersion = "";
const APP_VERSION = "1.1.0";

// Initialization
document.addEventListener("DOMContentLoaded", () => {
    loadSavedServers();

    if (window.Android && typeof Android.getSavedServerUrl === "function") {
        currentServerUrl = Android.getSavedServerUrl() || currentServerUrl;
    } else {
        const local = localStorage.getItem("conan_server_url");
        if (local) currentServerUrl = local;
    }

    normalizeServerUrl();
    updateHeaderDisplay();

    pollServer();
    pollTimer = setInterval(pollServer, 3000);

    // Initial check for app update
    setTimeout(checkAppUpdates, 2000);
});

function normalizeServerUrl() {
    currentServerUrl = currentServerUrl.trim().replace(/\/+$/, "");
    if (!currentServerUrl.startsWith("http://") && !currentServerUrl.startsWith("https://")) {
        currentServerUrl = "http://" + currentServerUrl;
    }
}

function updateHeaderDisplay() {
    try {
        const u = new URL(currentServerUrl);
        document.getElementById("headerServerHost").innerText = u.host;
    } catch {
        document.getElementById("headerServerHost").innerText = currentServerUrl;
    }
}

function showToast(msg) {
    if (window.Android && typeof Android.showToast === "function") {
        Android.showToast(msg);
    } else {
        alert(msg);
    }
}

function vibrate(ms = 30) {
    if (window.Android && typeof Android.vibrate === "function") {
        Android.vibrate(ms);
    }
}

// Tab Switching
function switchTab(tab) {
    vibrate(20);
    currentTab = tab;
    document.querySelectorAll(".tab-content").forEach(el => el.classList.remove("active"));
    document.querySelectorAll(".nav-item").forEach(el => el.classList.remove("active"));

    const tabEl = document.getElementById("tab-" + tab);
    if (tabEl) tabEl.classList.add("active");

    const navItems = document.querySelectorAll(".nav-item");
    const tabs = ["dashboard", "mods", "settings", "players", "console", "updates"];
    const idx = tabs.indexOf(tab);
    if (idx >= 0 && navItems[idx]) {
        navItems[idx].classList.add("active");
    }

    if (tab === "settings") {
        fetchServerConfig();
    } else if (tab === "mods") {
        fetchInstalledMods();
    } else if (tab === "console") {
        fetchLogs();
    }
}

// Server Polling & Status
async function pollServer() {
    try {
        const res = await fetch(`${currentServerUrl}/api/status`, { cache: "no-store", signal: AbortSignal.timeout(3500) });
        if (!res.ok) throw new Error("HTTP " + res.status);
        const data = await res.json();

        // Header connection status
        document.getElementById("headerConnectionLed").style.background = "#10b981";

        // Dashboard Metrics
        document.getElementById("dashServerName").innerText = data.serverName || "Conan Exiles Server";
        document.getElementById("dashValUptime").innerText = data.uptimeString || "00:00:00";
        document.getElementById("dashValGamePort").innerText = data.gamePort || "7777";
        document.getElementById("dashValRconPort").innerText = data.rconPort || "25575";
        document.getElementById("dashValMods").innerText = data.activeModsCount || 0;

        const maxP = data.steamMaxPlayers || data.maxPlayers || 40;
        const curP = (data.players && data.players.length > 0) ? data.players.length : (data.steamPlayers || 0);
        document.getElementById("dashValPlayers").innerText = `${curP} / ${maxP}`;
        document.getElementById("playersListCount").innerText = curP;

        // Server Version
        if (data.appVersion) {
            document.getElementById("serverInstalledVersion").innerText = "v" + data.appVersion;
        }

        // Status Badge
        const badge = document.getElementById("dashStatusBadge");
        badge.innerText = data.status || "STOPPED";
        if (data.status === "RUNNING") badge.className = "badge badge-running";
        else if (data.status === "UPDATING") badge.className = "badge badge-updating";
        else badge.className = "badge badge-stopped";

        // Steam Master Status
        const steamVal = document.getElementById("dashValSteam");
        if (data.steamOnline) {
            steamVal.innerText = `ONLINE (${data.steamPing || 0}ms)`;
            steamVal.style.color = "#34d399";
        } else if (data.status === "RUNNING") {
            steamVal.innerText = "STARTING UP...";
            steamVal.style.color = "#fbbf24";
        } else {
            steamVal.innerText = "OFFLINE";
            steamVal.style.color = "#94a3b8";
        }

        // Render connected players
        renderPlayersList(data.players || []);

        if (currentTab === "console") {
            fetchLogs();
        }
    } catch (e) {
        document.getElementById("headerConnectionLed").style.background = "#ef4444";
        document.getElementById("dashStatusBadge").innerText = "OFFLINE";
        document.getElementById("dashStatusBadge").className = "badge badge-stopped";
    }
}

// Render Players List
function renderPlayersList(players) {
    const container = document.getElementById("playersListContainer");
    if (!container) return;

    if (!players || players.length === 0) {
        container.innerHTML = `<div style="text-align:center;padding:16px;color:#64748b;font-size:0.85rem;">No players currently online</div>`;
        return;
    }

    container.innerHTML = players.map(p => `
        <div style="background:#0f172a;border:1px solid #334155;border-radius:8px;padding:10px;margin-bottom:8px;display:flex;justify-content:space-between;align-items:center;">
            <div>
                <div style="font-weight:bold;color:#f8fafc;font-size:0.92rem;">${escapeHtml(p.name)}</div>
                <div style="font-size:0.72rem;color:#94a3b8;margin-top:2px;">
                    Ping: <strong style="color:#38bdf8;">${p.ping || 0}ms</strong> | Time: <strong>${p.durationFormatted || "00:00"}</strong> | Score: <strong>${p.score || 0}</strong>
                </div>
            </div>
            <button class="btn-danger" style="padding:6px 12px;font-size:0.75rem;" onclick="kickPlayer('${escapeHtml(p.name)}')">Kick</button>
        </div>
    `).join("");
}

async function kickPlayer(name) {
    if (!confirm(`Are you sure you want to kick player "${name}"?`)) return;
    vibrate(40);
    try {
        const res = await fetch(`${currentServerUrl}/api/control/rcon`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ command: `kick ${name}` })
        });
        const data = await res.json();
        showToast(`Kick command sent: ${data.message || data.reply || "Done"}`);
        pollServer();
    } catch (e) {
        showToast("Kick error: " + e.message);
    }
}

// Remote Controls
async function controlAction(action) {
    vibrate(40);
    try {
        showToast(`Sending ${action} command...`);
        const res = await fetch(`${currentServerUrl}/api/control/${action}`, {
            method: "POST",
            headers: { "Content-Type": "application/json" }
        });
        const data = await res.json();
        showToast(data.message || "Command executed.");
        pollServer();
    } catch (e) {
        showToast("Error: " + e.message);
    }
}

// Live Logs & RCON
async function fetchLogs() {
    try {
        const res = await fetch(`${currentServerUrl}/api/logs`);
        const logs = await res.json();
        const box = document.getElementById("consoleLogBox");
        if (box && Array.isArray(logs)) {
            box.innerText = logs.join("\n");
            box.scrollTop = box.scrollHeight;
        }
    } catch {}
}

async function sendRcon() {
    const input = document.getElementById("txtRconCmd");
    const cmd = input.value.trim();
    if (!cmd) return;
    vibrate(30);

    try {
        const res = await fetch(`${currentServerUrl}/api/control/rcon`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ command: cmd })
        });
        const data = await res.json();
        input.value = "";
        showToast(data.reply ? `RCON: ${data.reply}` : "RCON command sent");
        fetchLogs();
    } catch (e) {
        showToast("RCON failed: " + e.message);
    }
}

// STEAM WORKSHOP MOD MANAGEMENT
async function searchWorkshop() {
    const query = document.getElementById("txtModSearch").value.trim();
    if (!query) {
        showToast("Please enter a mod name or ID to search");
        return;
    }

    vibrate(30);
    const container = document.getElementById("workshopSearchResults");
    container.innerHTML = `<div style="text-align:center;padding:16px;color:#38bdf8;">🔍 Searching Steam Workshop for "${escapeHtml(query)}"...</div>`;

    try {
        const res = await fetch(`${currentServerUrl}/api/workshop/search?query=${encodeURIComponent(query)}`);
        const results = await res.json();

        if (!Array.isArray(results) || results.length === 0) {
            container.innerHTML = `<div style="text-align:center;padding:16px;color:#94a3b8;">No matching mods found for "${escapeHtml(query)}"</div>`;
            return;
        }

        container.innerHTML = results.map(mod => `
            <div class="mod-item">
                <img src="${mod.PreviewUrl || 'app.png'}" class="mod-thumb" onerror="this.src='app.png'">
                <div class="mod-info">
                    <div class="mod-title">${escapeHtml(mod.Title || 'Unknown Mod')}</div>
                    <div class="mod-meta">ID: <strong>${mod.Id}</strong></div>
                </div>
                <div>
                    ${mod.IsInstalled ? 
                        `<button class="btn-secondary" style="padding:8px 10px;font-size:0.75rem;" disabled>✓ Added</button>` : 
                        `<button class="btn-primary" style="padding:8px 12px;font-size:0.75rem;" onclick="addModToServer('${mod.Id}', '${escapeHtml(mod.Title)}')">➕ Add</button>`
                    }
                </div>
            </div>
        `).join("");
    } catch (e) {
        container.innerHTML = `<div style="text-align:center;padding:16px;color:#f87171;">Search error: ${e.message}</div>`;
    }
}

async function addModToServer(modId, modTitle) {
    vibrate(40);
    try {
        showToast(`Adding mod #${modId}...`);
        const res = await fetch(`${currentServerUrl}/api/mods/add`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ modId })
        });
        const data = await res.json();
        if (data.success) {
            showToast(`Added ${modTitle || ('Mod #' + modId)} to server!`);
            fetchInstalledMods();
            // Re-render search results to update button state
            searchWorkshop();
        } else {
            showToast("Failed to add mod: " + (data.error || "Unknown"));
        }
    } catch (e) {
        showToast("Error adding mod: " + e.message);
    }
}

async function removeModFromServer(modId) {
    if (!confirm(`Are you sure you want to remove Mod #${modId} from the server?`)) return;
    vibrate(40);

    try {
        showToast(`Removing mod #${modId}...`);
        const res = await fetch(`${currentServerUrl}/api/mods/remove`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ modId })
        });
        const data = await res.json();
        if (data.success) {
            showToast(`Removed Mod #${modId}`);
            fetchInstalledMods();
        } else {
            showToast("Failed: " + (data.error || "Unknown error"));
        }
    } catch (e) {
        showToast("Error: " + e.message);
    }
}

async function fetchInstalledMods() {
    const container = document.getElementById("installedModsContainer");
    if (!container) return;

    try {
        const res = await fetch(`${currentServerUrl}/api/mods`);
        const data = await res.json();
        installedMods = data.details || [];

        document.getElementById("installedModsCount").innerText = installedMods.length;

        if (installedMods.length === 0) {
            container.innerHTML = `<div style="text-align:center;padding:16px;color:#64748b;font-size:0.85rem;">No mods currently installed on server</div>`;
            return;
        }

        container.innerHTML = installedMods.map((mod, idx) => `
            <div class="mod-item">
                <span style="font-weight:bold;color:#64748b;font-size:0.8rem;width:18px;">${idx + 1}</span>
                <img src="${mod.PreviewUrl || 'app.png'}" class="mod-thumb" onerror="this.src='app.png'">
                <div class="mod-info">
                    <div class="mod-title">${escapeHtml(mod.Title || ('Mod #' + mod.Id))}</div>
                    <div class="mod-meta">ID: <strong>${mod.Id}</strong></div>
                </div>
                <div class="mod-actions">
                    ${idx > 0 ? `<button class="btn-icon" onclick="reorderMod(${idx}, -1)">▲</button>` : ''}
                    ${idx < installedMods.length - 1 ? `<button class="btn-icon" onclick="reorderMod(${idx}, 1)">▼</button>` : ''}
                    <button class="btn-icon" style="background:#ef4444;color:white;" onclick="removeModFromServer('${mod.Id}')">🗑️</button>
                </div>
            </div>
        `).join("");
    } catch (e) {
        container.innerHTML = `<div style="text-align:center;padding:16px;color:#f87171;">Failed to load server mods: ${e.message}</div>`;
    }
}

async function reorderMod(index, delta) {
    const targetIdx = index + delta;
    if (targetIdx < 0 || targetIdx >= installedMods.length) return;

    vibrate(20);
    const item = installedMods.splice(index, 1)[0];
    installedMods.splice(targetIdx, 0, item);

    const newIds = installedMods.map(m => m.Id);
    try {
        await fetch(`${currentServerUrl}/api/mods/reorder`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ mods: newIds })
        });
        fetchInstalledMods();
    } catch (e) {
        showToast("Reorder error: " + e.message);
    }
}

// SERVER SETTINGS
async function fetchServerConfig() {
    try {
        const res = await fetch(`${currentServerUrl}/api/config`);
        const cfg = await res.json();

        document.getElementById("cfgServerName").value = cfg.serverName || "";
        document.getElementById("cfgServerPassword").value = cfg.serverPassword || "";
        document.getElementById("cfgAdminPassword").value = cfg.adminPassword || "";
        document.getElementById("cfgMaxPlayers").value = cfg.maxPlayers || 40;
        document.getElementById("cfgMaxTickRate").value = cfg.maxTickRate || 30;
        document.getElementById("cfgRegion").value = cfg.serverRegion ?? 0;
        document.getElementById("cfgBattlEye").checked = !!cfg.battlEyeEnabled;
        document.getElementById("cfgVac").checked = !!cfg.vacEnabled;
    } catch (e) {
        showToast("Error loading server config: " + e.message);
    }
}

async function saveServerConfig() {
    vibrate(40);
    try {
        showToast("Saving settings to server...");
        const payload = {
            serverName: document.getElementById("cfgServerName").value.trim(),
            serverPassword: document.getElementById("cfgServerPassword").value.trim(),
            adminPassword: document.getElementById("cfgAdminPassword").value.trim(),
            maxPlayers: parseInt(document.getElementById("cfgMaxPlayers").value) || 40,
            maxTickRate: parseInt(document.getElementById("cfgMaxTickRate").value) || 30,
            serverRegion: parseInt(document.getElementById("cfgRegion").value) || 0,
            battlEyeEnabled: document.getElementById("cfgBattlEye").checked,
            vacEnabled: document.getElementById("cfgVac").checked
        };

        const res = await fetch(`${currentServerUrl}/api/config`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });
        const data = await res.json();
        showToast(data.message || "Server settings updated!");
        pollServer();
    } catch (e) {
        showToast("Save error: " + e.message);
    }
}

// IN-APP UPDATE CHECKER
async function checkAppUpdates() {
    try {
        const res = await fetch("https://api.github.com/repos/Nakrom75/ConanEnhancedServerManager/releases/latest");
        if (!res.ok) return;
        const release = await res.json();

        const latestTag = (release.tag_name || "").replace(/^v/i, "");
        const currentClean = APP_VERSION.replace(/^v/i, "");

        if (latestTag && isNewerVersion(latestTag, currentClean)) {
            // Find APK asset
            const apkAsset = (release.assets || []).find(a => a.name && a.name.endsWith(".apk"));
            if (apkAsset) {
                latestApkUrl = apkAsset.browser_download_url;
                latestApkVersion = latestTag;

                document.getElementById("updateBannerCard").style.display = "block";
                document.getElementById("updateBannerTitle").innerText = `New Update: v${latestTag} Available!`;
                document.getElementById("updateBannerNotes").innerText = release.name || release.body?.substring(0, 150) || "Bug fixes & new features";
                showToast(`New update v${latestTag} is available!`);
            }
        } else {
            document.getElementById("updateBannerCard").style.display = "none";
            if (currentTab === "updates") {
                showToast("Application is up to date (v" + APP_VERSION + ")");
            }
        }
    } catch {}
}

function isNewerVersion(remote, local) {
    const rParts = remote.split(".").map(Number);
    const lParts = local.split(".").map(Number);
    for (let i = 0; i < Math.max(rParts.length, lParts.length); i++) {
        const r = rParts[i] || 0;
        const l = lParts[i] || 0;
        if (r > l) return true;
        if (r < l) return false;
    }
    return false;
}

function installUpdate() {
    vibrate(40);
    if (!latestApkUrl) {
        showToast("No APK download link found.");
        return;
    }

    if (window.Android && typeof Android.downloadAndInstallApk === "function") {
        Android.downloadAndInstallApk(latestApkUrl, latestApkVersion);
    } else {
        window.open(latestApkUrl, "_system");
    }
}

// SERVER CONNECTION MODAL
function openServerModal() {
    vibrate(20);
    document.getElementById("txtTargetUrl").value = currentServerUrl;
    renderSavedServers();
    document.getElementById("serverModal").style.display = "block";
}

function closeServerModal(e) {
    if (!e || e.target.id === "serverModal") {
        document.getElementById("serverModal").style.display = "none";
    }
}

function connectToCustomServer() {
    vibrate(30);
    const url = document.getElementById("txtTargetUrl").value.trim();
    if (!url) return;

    currentServerUrl = url;
    normalizeServerUrl();

    // Save to persistent storage
    if (window.Android && typeof Android.saveServerUrl === "function") {
        Android.saveServerUrl(currentServerUrl);
    } else {
        localStorage.setItem("conan_server_url", currentServerUrl);
    }

    addSavedServer(currentServerUrl);
    updateHeaderDisplay();
    closeServerModal();
    pollServer();
    showToast("Connecting to " + currentServerUrl + "...");
}

function loadSavedServers() {
    try {
        const raw = localStorage.getItem("conan_saved_servers");
        savedServers = raw ? JSON.parse(raw) : [];
    } catch {
        savedServers = [];
    }
}

function addSavedServer(url) {
    if (!savedServers.includes(url)) {
        savedServers.unshift(url);
        if (savedServers.length > 8) savedServers.pop();
        localStorage.setItem("conan_saved_servers", JSON.stringify(savedServers));
    }
}

function selectSavedServer(url) {
    document.getElementById("txtTargetUrl").value = url;
    connectToCustomServer();
}

function deleteSavedServer(idx, e) {
    e.stopPropagation();
    savedServers.splice(idx, 1);
    localStorage.setItem("conan_saved_servers", JSON.stringify(savedServers));
    renderSavedServers();
}

function renderSavedServers() {
    const list = document.getElementById("savedServersList");
    if (!list) return;

    if (savedServers.length === 0) {
        list.innerHTML = `<div style="color:#64748b;font-size:0.8rem;text-align:center;padding:10px;">No saved servers yet</div>`;
        return;
    }

    list.innerHTML = savedServers.map((srv, idx) => `
        <div style="background:#0f172a;border:1px solid #334155;border-radius:8px;padding:8px 12px;margin-bottom:6px;display:flex;justify-content:space-between;align-items:center;cursor:pointer;" onclick="selectSavedServer('${srv}')">
            <span style="font-size:0.85rem;color:#38bdf8;font-weight:600;">${srv}</span>
            <button class="btn-icon" style="background:#ef4444;color:white;padding:4px 8px;font-size:0.75rem;" onclick="deleteSavedServer(${idx}, event)">✕</button>
        </div>
    `).join("");
}

function handleBackPressed() {
    const modal = document.getElementById("serverModal");
    if (modal && modal.style.display === "block") {
        modal.style.display = "none";
        return "handled";
    }
    if (currentTab !== "dashboard") {
        switchTab("dashboard");
        return "handled";
    }
    return false;
}

function escapeHtml(str) {
    if (!str) return "";
    return String(str).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}
