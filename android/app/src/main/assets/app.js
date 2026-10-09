// Conan Server Manager - Android Remote Client Engine
let currentServerUrl = "http://192.168.1.100:8088";
let currentTab = "dashboard";
let pollTimer = null;
let installedMods = [];
let savedServers = [];
let latestApkUrl = "";
let lastKnownConfig = null;
let APP_VERSION = "1.3.2";
if (window.Android && typeof Android.getAppVersion === "function") {
    APP_VERSION = Android.getAppVersion();
}

let currentModalMod = null;

function openSteamWorkshopBrowser(url) {
    const targetUrl = url || "https://steamcommunity.com/app/440900/workshop/";
    try {
        if (window.Android && typeof Android.openWorkshopBrowser === "function") {
            Android.openWorkshopBrowser(targetUrl);
        } else {
            openExternalBrowser(targetUrl);
        }
    } catch (e) {
        openExternalBrowser(targetUrl);
    }
}

function openExternalBrowser(url) {
    if (!url) return;
    try {
        if (window.Android && typeof Android.openExternalUrl === "function") {
            Android.openExternalUrl(url);
        } else {
            window.open(url, "_blank");
        }
    } catch (e) {
        window.open(url, "_blank");
    }
}

function showModDetailsModal(modId, modTitle, previewUrl) {
    if (!modId) return;
    const cleanId = String(modId).trim();
    currentModalMod = {
        id: cleanId,
        title: modTitle || `Mod #${cleanId}`,
        previewUrl: previewUrl || "app.png",
        url: `https://steamcommunity.com/sharedfiles/filedetails/?id=${cleanId}`
    };

    const modal = document.getElementById("modDetailsModal");
    const titleEl = document.getElementById("modalModTitle");
    const idEl = document.getElementById("modalModId");
    const thumbEl = document.getElementById("modalModThumb");
    const urlTextEl = document.getElementById("modalModUrlText");

    if (titleEl) titleEl.innerText = currentModalMod.title;
    if (idEl) idEl.innerText = `ID: ${currentModalMod.id}`;
    if (thumbEl) thumbEl.src = currentModalMod.previewUrl;
    if (urlTextEl) urlTextEl.innerText = currentModalMod.url;

    if (modal) {
        modal.style.display = "flex";
    }
}

function closeModDetailsModal(e) {
    if (e && e.target && e.target.classList && !e.target.classList.contains("modal-overlay") && !e.target.classList.contains("btn-header-close")) {
        return;
    }
    const modal = document.getElementById("modDetailsModal");
    if (modal) {
        modal.style.display = "none";
    }
    currentModalMod = null;
}

function onModalPreDownload() {
    vibrate(30);
    if (currentModalMod && currentModalMod.id) {
        preDownloadMod(currentModalMod.id);
        closeModDetailsModal();
    }
}

async function preDownloadMod(modId) {
    vibrate(40);
    try {
        showToast(`Requesting server pre-download for Mod #${modId}...`);
        const res = await fetch(`${currentServerUrl}/api/mods/predownload`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ modId })
        });
        const data = await res.json();
        if (data.success) {
            showToast(`Pre-download started on server for Mod #${modId}!`);
        } else {
            showToast("Pre-download request failed: " + (data.error || "Unknown"));
        }
    } catch (e) {
        showToast("Error: " + e.message);
    }
}

function onModalOpenWorkshop() {
    vibrate(30);
    if (currentModalMod && currentModalMod.url) {
        openSteamWorkshopBrowser(currentModalMod.url);
        closeModDetailsModal();
    }
}

function onModalCopyLink() {
    vibrate(20);
    if (currentModalMod && currentModalMod.url) {
        try {
            navigator.clipboard.writeText(currentModalMod.url);
            showToast("Copied Workshop link to clipboard!");
        } catch (e) {
            showToast("Link: " + currentModalMod.url);
        }
    }
}

function attachLongPressMod(el, modId, modTitle, previewUrl) {
    if (!el) return;
    let timer = null;
    let startX = 0, startY = 0;
    let moved = false;

    el.addEventListener("touchstart", (e) => {
        if (e.target.closest(".btn-icon") || e.target.closest("button")) return;
        moved = false;
        startX = e.touches[0].clientX;
        startY = e.touches[0].clientY;
        timer = setTimeout(() => {
            if (!moved) {
                vibrate(40);
                showModDetailsModal(modId, modTitle, previewUrl);
            }
        }, 450);
    }, { passive: true });

    el.addEventListener("touchmove", (e) => {
        if (Math.abs(e.touches[0].clientX - startX) > 10 || Math.abs(e.touches[0].clientY - startY) > 10) {
            moved = true;
            clearTimeout(timer);
        }
    }, { passive: true });

    el.addEventListener("touchend", () => {
        clearTimeout(timer);
    });

    el.addEventListener("touchcancel", () => {
        clearTimeout(timer);
    });

    el.addEventListener("contextmenu", (e) => {
        if (e.target.closest(".btn-icon") || e.target.closest("button")) return;
        e.preventDefault();
        vibrate(40);
        showModDetailsModal(modId, modTitle, previewUrl);
    });
}

// Global Error Handler for WebView debugging
window.onerror = function(msg, url, lineNo, columnNo, error) {
    console.error("Window Error:", msg, "at line", lineNo, error);
    return false;
};

// Initialization
document.addEventListener("DOMContentLoaded", () => {
    if (window.Android && typeof Android.getAppVersion === "function") {
        APP_VERSION = Android.getAppVersion();
    }
    const verBadge = document.getElementById("appInstalledVersion");
    if (verBadge) verBadge.innerText = "v" + APP_VERSION;

    loadSavedServers();

    if (window.Android && typeof Android.getSavedServerUrl === "function") {
        currentServerUrl = Android.getSavedServerUrl() || currentServerUrl;
    } else {
        const local = localStorage.getItem("conan_server_url");
        if (local) currentServerUrl = local;
    }

    normalizeServerUrl();
    updateHeaderDisplay();

    // Attach direct listeners to ensure tapping always works regardless of event bubbling
    const srvBtn = document.getElementById("btnOpenServerModal");
    if (srvBtn) {
        srvBtn.addEventListener("click", (e) => {
            e.preventDefault();
            openServerModal();
        });
    }

    const brandEl = document.getElementById("headerBrandContainer");
    if (brandEl) {
        brandEl.addEventListener("click", (e) => {
            e.preventDefault();
            openServerModal();
        });
    }

    pollServer();
    fetchServerConfig();
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
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 3500);
        const res = await fetch(`${currentServerUrl}/api/status?_t=${Date.now()}`, { signal: controller.signal });
        clearTimeout(timeoutId);

        if (!res.ok) throw new Error("HTTP " + res.status);
        const data = await res.json();

        // Header connection status
        document.getElementById("headerConnectionLed").style.background = "#10b981";

        // Dashboard Metrics
        document.getElementById("dashServerName").innerText = data.serverName || "Conan Exiles Server";
        const srvUp = data.serverUptimeString || data.uptimeString || "00:00:00";
        const appUp = data.appUptimeString || "00:00:00";
        const sysUp = data.systemUptimeString || "00:00:00";
        if (document.getElementById("dashValUptime")) document.getElementById("dashValUptime").innerText = srvUp;
        if (document.getElementById("dashValServerUptime")) document.getElementById("dashValServerUptime").innerText = srvUp;
        if (document.getElementById("dashValAppUptime")) document.getElementById("dashValAppUptime").innerText = appUp;
        if (document.getElementById("dashValSystemUptime")) document.getElementById("dashValSystemUptime").innerText = sysUp;

        if (document.getElementById("dashValRam")) {
            const srvRam = data.serverRamMb != null ? `${Math.round(data.serverRamMb)} MB` : "--";
            const appRam = data.appRamMb != null ? `${Math.round(data.appRamMb)} MB` : "--";
            const sysUsed = data.systemRamUsedGb != null ? `${data.systemRamUsedGb.toFixed(1)}` : "--";
            const sysTot = data.systemRamTotalGb != null ? `${data.systemRamTotalGb.toFixed(1)} GB` : "--";
            const sysPct = data.systemRamPercent != null ? `(${data.systemRamPercent}%)` : "";
            document.getElementById("dashValRam").innerText = `Srv: ${srvRam} | App: ${appRam} | Host: ${sysUsed}/${sysTot} ${sysPct}`;
        }

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

        // Cache configuration received in status payload
        lastKnownConfig = {
            serverName: data.serverName,
            serverPassword: data.serverPassword,
            adminPassword: data.adminPassword,
            rconPassword: data.rconPassword,
            maxPlayers: data.maxPlayers,
            maxTickRate: data.maxTickRate,
            region: data.region || data.serverRegion,
            battlEyeEnabled: data.battlEyeEnabled ?? data.enableBattlEye,
            vacEnabled: data.vacEnabled ?? data.enableVAC,
            ...(data.config || {})
        };

        // If on settings tab and inputs are still empty or unpopulated, apply immediately
        if (currentTab === "settings") {
            const elName = document.getElementById("cfgServerName");
            if (elName && !elName.value && data.serverName) {
                applyConfigToForm(lastKnownConfig);
            }
        }

        if (currentTab === "console") {
            fetchLogs();
        }
    } catch (e) {
        document.getElementById("headerConnectionLed").style.background = "#ef4444";
        document.getElementById("dashStatusBadge").innerText = "OFFLINE";
        document.getElementById("dashStatusBadge").className = "badge badge-stopped";

        const statusEl = document.getElementById("cfgStatusMsg");
        if (statusEl && !lastKnownConfig) {
            statusEl.innerHTML = `<span style="color:#f87171;">⚠️ Server offline at ${escapeHtml(currentServerUrl)}. Is ConanServerManager.exe running?</span>`;
        }
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
        showToast(data.result || data.message || "Command executed.");
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
            <div class="mod-item" data-mod-id="${mod.Id}" data-mod-title="${escapeHtml(mod.Title || 'Unknown Mod')}" data-mod-thumb="${mod.PreviewUrl || 'app.png'}" title="Long press to open Steam Workshop">
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

        container.querySelectorAll(".mod-item").forEach(item => {
            const id = item.getAttribute("data-mod-id");
            const title = item.getAttribute("data-mod-title");
            const thumb = item.getAttribute("data-mod-thumb");
            attachLongPressMod(item, id, title, thumb);
        });
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
            showToast(`Added ${modTitle || ('Mod #' + modId)}! Pre-downloading in background on server...`);
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

    if (!installedMods || installedMods.length === 0) {
        container.innerHTML = `<div style="text-align:center;padding:16px;color:#38bdf8;font-size:0.85rem;">⏳ Loading server mods &amp; titles...</div>`;
    }

    try {
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 6000);
        const res = await fetch(`${currentServerUrl}/api/mods?_t=${Date.now()}`, { signal: controller.signal });
        clearTimeout(timeoutId);

        if (!res.ok) throw new Error("HTTP " + res.status);
        const data = await res.json();
        installedMods = data.details || [];

        const countBadge = document.getElementById("installedModsCount");
        if (countBadge) countBadge.innerText = installedMods.length;

        if (installedMods.length === 0) {
            container.innerHTML = `<div style="text-align:center;padding:16px;color:#64748b;font-size:0.85rem;">No mods currently installed on server</div>`;
            return;
        }

        container.innerHTML = installedMods.map((mod, idx) => `
            <div class="mod-item" data-mod-id="${mod.Id}" data-mod-title="${escapeHtml(mod.Title || ('Mod #' + mod.Id))}" data-mod-thumb="${mod.PreviewUrl || 'app.png'}" title="Long press to open Steam Workshop">
                <span style="font-weight:bold;color:#64748b;font-size:0.8rem;width:18px;">${idx + 1}</span>
                <img src="${mod.PreviewUrl || 'app.png'}" class="mod-thumb" onerror="this.src='app.png'">
                <div class="mod-info">
                    <div class="mod-title" style="font-weight:600;color:#f8fafc;">${escapeHtml(mod.Title || ('Mod #' + mod.Id))}</div>
                    <div class="mod-meta" style="color:#94a3b8;font-size:0.75rem;margin-top:2px;">
                        ID: <strong style="color:#38bdf8;">${mod.Id}</strong>
                        ${mod.IsDownloaded ? 
                            '<span style="margin-left:8px;color:#34d399;font-weight:600;">✅ On Disk</span>' : 
                            '<span style="margin-left:8px;color:#fbbf24;font-weight:600;">⏳ Pending Download</span>'
                        }
                    </div>
                </div>
                <div class="mod-actions">
                    ${idx > 0 ? `<button class="btn-icon" onclick="reorderMod(${idx}, -1)" title="Move Up">▲</button>` : ''}
                    ${idx < installedMods.length - 1 ? `<button class="btn-icon" onclick="reorderMod(${idx}, 1)" title="Move Down">▼</button>` : ''}
                    <button class="btn-icon" style="background:#ef4444;color:white;" onclick="removeModFromServer('${mod.Id}')" title="Remove Mod">🗑️</button>
                </div>
            </div>
        `).join("");

        container.querySelectorAll(".mod-item").forEach(item => {
            const id = item.getAttribute("data-mod-id");
            const title = item.getAttribute("data-mod-title");
            const thumb = item.getAttribute("data-mod-thumb");
            attachLongPressMod(item, id, title, thumb);
        });
    } catch (e) {
        container.innerHTML = `
            <div style="text-align:center;padding:16px;color:#f87171;font-size:0.85rem;">
                <div>⚠️ Failed to load server mods: ${escapeHtml(e.message)}</div>
                <button class="btn-secondary" style="margin-top:8px;padding:4px 12px;font-size:0.75rem;" onclick="fetchInstalledMods()">🔄 Retry</button>
            </div>
        `;
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
function applyConfigToForm(cfg) {
    if (!cfg || typeof cfg !== "object") return;

    const srvName = cfg.serverName ?? cfg.ServerName;
    const srvPass = cfg.serverPassword ?? cfg.ServerPassword;
    const admPass = cfg.adminPassword ?? cfg.AdminPassword;
    const maxPl = cfg.maxPlayers ?? cfg.MaxPlayers;
    const tick = cfg.maxTickRate ?? cfg.MaxTickRate;
    const be = cfg.battlEyeEnabled ?? cfg.enableBattlEye ?? cfg.EnableBattlEye;
    const vac = cfg.vacEnabled ?? cfg.enableVAC ?? cfg.EnableVAC;
    const rawReg = cfg.region ?? cfg.serverRegion ?? cfg.Region;

    const elName = document.getElementById("cfgServerName");
    const elPass = document.getElementById("cfgServerPassword");
    const elAdmin = document.getElementById("cfgAdminPassword");
    const elMaxPl = document.getElementById("cfgMaxPlayers");
    const elTick = document.getElementById("cfgMaxTickRate");
    const elReg = document.getElementById("cfgRegion");
    const elBe = document.getElementById("cfgBattlEye");
    const elVac = document.getElementById("cfgVac");

    if (srvName !== undefined && elName) elName.value = srvName;
    if (srvPass !== undefined && elPass) elPass.value = srvPass;
    if (admPass !== undefined && elAdmin) elAdmin.value = admPass;
    if (maxPl !== undefined && elMaxPl) elMaxPl.value = maxPl;
    if (tick !== undefined && elTick) elTick.value = tick;
    if (be !== undefined && elBe) elBe.checked = !!be;
    if (vac !== undefined && elVac) elVac.checked = !!vac;

    if (rawReg !== undefined && elReg) {
        let matchVal = "";
        const regStr = String(rawReg).trim();
        for (let i = 0; i < elReg.options.length; i++) {
            const opt = elReg.options[i];
            if (opt.value === regStr || opt.value === regStr.split(" - ")[0].trim()) {
                matchVal = opt.value;
                break;
            }
            if (opt.text.toLowerCase().includes(regStr.toLowerCase())) {
                matchVal = opt.value;
                break;
            }
        }
        if (matchVal !== "") {
            elReg.value = matchVal;
        }
    }

    if (srvName) {
        const dashName = document.getElementById("dashServerName");
        if (dashName) dashName.innerText = srvName;
        const activeModalName = document.getElementById("activeModalServerName");
        if (activeModalName) activeModalName.innerText = srvName;
    }
}

async function fetchServerConfig(showFeedback = false) {
    const statusEl = document.getElementById("cfgStatusMsg");
    if (statusEl) {
        statusEl.innerHTML = `<span style="color:#38bdf8;">⏳ Connecting to server config...</span>`;
    }

    // 1. If we already have cached status data, populate immediately
    if (lastKnownConfig) {
        applyConfigToForm(lastKnownConfig);
    }

    try {
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 4000);
        const res = await fetch(`${currentServerUrl}/api/config?_t=${Date.now()}`, {
            signal: controller.signal
        });
        clearTimeout(timeoutId);

        if (!res.ok) throw new Error("HTTP " + res.status);
        const cfg = await res.json();
        lastKnownConfig = cfg;
        applyConfigToForm(cfg);

        if (statusEl) {
            statusEl.innerHTML = `<span style="color:#10b981;">🟢 Live config loaded from ${escapeHtml(currentServerUrl)}</span>`;
        }
        if (showFeedback) {
            showToast("Server configuration updated from host");
        }
    } catch (e) {
        console.warn("fetchServerConfig warning:", e);
        if (lastKnownConfig && (lastKnownConfig.serverName || lastKnownConfig.maxPlayers)) {
            applyConfigToForm(lastKnownConfig);
            if (statusEl) {
                statusEl.innerHTML = `<span style="color:#fbbf24;">⚡ Config loaded from status cache (${escapeHtml(e.message)})</span>`;
            }
        } else {
            if (statusEl) {
                statusEl.innerHTML = `<span style="color:#f87171;">⚠️ Could not reach server at ${escapeHtml(currentServerUrl)} (${escapeHtml(e.message)}). Is ConanServerManager.exe running?</span>`;
            }
        }
        if (showFeedback) {
            showToast("Failed to fetch config: " + e.message);
        }
    }
}

async function saveServerConfig() {
    vibrate(40);
    const btn = document.getElementById("btnSaveConfig");
    const originalText = btn ? btn.innerText : "💾 Save Configuration to Server";
    if (btn) {
        btn.disabled = true;
        btn.innerText = "⏳ Saving to Server...";
    }

    try {
        showToast("Saving settings to server...");
        const regSelect = document.getElementById("cfgRegion");
        const regText = regSelect ? (regSelect.options[regSelect.selectedIndex]?.text || regSelect.value) : "0 - Europe";

        const payload = {
            serverName: document.getElementById("cfgServerName").value.trim(),
            serverPassword: document.getElementById("cfgServerPassword").value.trim(),
            adminPassword: document.getElementById("cfgAdminPassword").value.trim(),
            maxPlayers: parseInt(document.getElementById("cfgMaxPlayers").value) || 40,
            maxTickRate: parseInt(document.getElementById("cfgMaxTickRate").value) || 30,
            region: regText,
            serverRegion: regSelect ? regSelect.value : "0",
            enableBattlEye: document.getElementById("cfgBattlEye").checked,
            battlEyeEnabled: document.getElementById("cfgBattlEye").checked,
            enableVAC: document.getElementById("cfgVac").checked,
            vacEnabled: document.getElementById("cfgVac").checked
        };

        const res = await fetch(`${currentServerUrl}/api/config?_t=${Date.now()}`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "Accept": "application/json"
            },
            body: JSON.stringify(payload)
        });

        if (!res.ok) throw new Error("HTTP " + res.status);
        const data = await res.json();
        showToast(data.message || "Server settings updated!");

        lastKnownConfig = { ...lastKnownConfig, ...payload };
        applyConfigToForm(lastKnownConfig);

        const statusEl = document.getElementById("cfgStatusMsg");
        if (statusEl) {
            statusEl.innerHTML = `<span style="color:#10b981;">✅ Settings saved successfully at ${new Date().toLocaleTimeString()}</span>`;
        }

        setTimeout(() => fetchServerConfig(), 1000);
        pollServer();
    } catch (e) {
        showToast("Save error: " + e.message);
        const statusEl = document.getElementById("cfgStatusMsg");
        if (statusEl) {
            statusEl.innerHTML = `<span style="color:#f87171;">❌ Save failed: ${escapeHtml(e.message)}</span>`;
        }
    } finally {
        if (btn) {
            btn.disabled = false;
            btn.innerText = originalText;
        }
    }
}

function togglePasswordVisibility(fieldId) {
    vibrate(20);
    const input = document.getElementById(fieldId);
    if (!input) return;
    input.type = input.type === "password" ? "text" : "password";
}

// IN-APP SMART DUAL-SOURCE UPDATE CHECKER
async function checkAppUpdates() {
    let updateFound = false;
    let foundVersion = "";
    let downloadUrl = "";
    let releaseNotes = "";
    let sourceLabel = "";

    const currentClean = APP_VERSION.replace(/^v/i, "").trim();
    showToast("Checking for updates...");

    // 1. Try GitHub Releases & Raw Repo Fallback
    try {
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 4500);
        const res = await fetch("https://api.github.com/repos/Nakrom75/ConanEnhancedServerManager/releases/latest?_t=" + Date.now(), {
            signal: controller.signal
        });
        clearTimeout(timeoutId);

        if (res.ok) {
            const release = await res.json();
            const latestTag = (release.tag_name || "").replace(/^v/i, "").trim();

            if (latestTag && isNewerVersion(latestTag, currentClean)) {
                foundVersion = latestTag;
                releaseNotes = release.name || release.body?.substring(0, 160) || "Latest release improvements & fixes";
                sourceLabel = "GitHub";

                // Check for attached APK in release assets
                const apkAsset = (release.assets || []).find(a => a.name && a.name.endsWith(".apk"));
                if (apkAsset && apkAsset.browser_download_url) {
                    downloadUrl = apkAsset.browser_download_url;
                } else {
                    // Fallback to direct raw repo APK
                    downloadUrl = `https://raw.githubusercontent.com/Nakrom75/ConanEnhancedServerManager/main/ConanServerManager-v${latestTag}.apk`;
                }
                updateFound = true;
            }
        }
    } catch (e) {
        console.warn("GitHub release check failed, checking server host fallback:", e);
    }

    // 2. Check connected Conan Server Host (/api/status & /conan.apk)
    try {
        const srvController = new AbortController();
        const srvTimeoutId = setTimeout(() => srvController.abort(), 3500);
        const srvRes = await fetch(`${currentServerUrl}/api/status?_t=${Date.now()}`, {
            signal: srvController.signal
        });
        clearTimeout(srvTimeoutId);

        if (srvRes.ok) {
            const srvData = await srvRes.json();
            const srvVer = (srvData.appVersion || "").replace(/^v/i, "").trim();

            if (srvVer) {
                const srvBadge = document.getElementById("serverInstalledVersion");
                if (srvBadge) srvBadge.innerText = "v" + srvVer;
            }

            // If server is newer than current app version and either GitHub wasn't newer or failed
            if (srvVer && isNewerVersion(srvVer, currentClean)) {
                if (!updateFound || isNewerVersion(srvVer, foundVersion)) {
                    foundVersion = srvVer;
                    downloadUrl = `${currentServerUrl}/conan.apk`;
                    releaseNotes = `Direct update from your Conan Server host (${srvData.serverName || currentServerUrl})`;
                    sourceLabel = "Server Host";
                    updateFound = true;
                }
            }
        }
    } catch (e) {
        console.warn("Server host update check warning:", e);
    }

    // 3. Update UI Banner
    const banner = document.getElementById("updateBannerCard");
    const bannerTitle = document.getElementById("updateBannerTitle");
    const bannerNotes = document.getElementById("updateBannerNotes");
    const installBtn = document.getElementById("btnInstallUpdate");

    if (updateFound && downloadUrl) {
        latestApkUrl = downloadUrl;
        latestApkVersion = foundVersion;

        if (banner) banner.style.display = "block";
        if (bannerTitle) bannerTitle.innerText = `New Update: v${foundVersion} Available!`;
        if (bannerNotes) bannerNotes.innerText = `${releaseNotes}\n(Download Source: ${sourceLabel})`;
        if (installBtn) installBtn.innerText = `📥 Download & Install v${foundVersion} (${sourceLabel})`;

        showToast(`Update v${foundVersion} found from ${sourceLabel}!`);
    } else {
        if (banner) banner.style.display = "none";
        showToast(`Application is up to date (v${APP_VERSION})`);
    }
}

function isNewerVersion(remote, local) {
    if (!remote || !local) return false;
    const rParts = remote.split(".").map(s => parseInt(s, 10) || 0);
    const lParts = local.split(".").map(s => parseInt(s, 10) || 0);
    for (let i = 0; i < Math.max(rParts.length, lParts.length); i++) {
        const r = rParts[i] || 0;
        const l = lParts[i] || 0;
        if (r > l) return true;
        if (r < l) return false;
    }
    return false;
}

function installUpdate(customUrl = null) {
    vibrate(40);
    const targetUrl = customUrl || latestApkUrl;
    if (!targetUrl) {
        showToast("No APK download link found.");
        return;
    }

    const ver = latestApkVersion || APP_VERSION;
    if (window.Android && typeof Android.downloadAndInstallApk === "function") {
        Android.downloadAndInstallApk(targetUrl, ver);
    } else {
        window.open(targetUrl, "_system");
    }
}

function downloadFromServerDirect() {
    vibrate(30);
    const url = `${currentServerUrl}/conan.apk`;
    showToast("Downloading APK from server...");
    installUpdate(url);
}

function downloadFromGithubDirect() {
    vibrate(30);
    const url = `https://raw.githubusercontent.com/Nakrom75/ConanEnhancedServerManager/main/ConanServerManager-v${APP_VERSION}.apk`;
    showToast("Downloading APK from GitHub...");
    installUpdate(url);
}

// SERVER CONNECTION FULL-SCREEN MODAL
function openServerModal() {
    try {
        vibrate(20);
    } catch (e) {}

    const modal = document.getElementById("serverModal");
    if (!modal) {
        console.error("serverModal element not found in DOM");
        return;
    }

    // 1. ALWAYS OPEN THE MODAL IMMEDIATELY
    modal.classList.add("active");
    modal.style.display = "flex";

    // 2. Safely populate input values
    try {
        const input = document.getElementById("txtTargetUrl");
        if (input) input.value = currentServerUrl || "";

        const nickInput = document.getElementById("txtTargetNickname");
        if (nickInput) nickInput.value = "";

        const dashName = document.getElementById("dashServerName");
        const activeName = document.getElementById("activeModalServerName");
        if (activeName && dashName) activeName.innerText = dashName.innerText || "Conan Server";

        const activeUrl = document.getElementById("activeModalServerUrl");
        if (activeUrl) activeUrl.innerText = currentServerUrl || "";

        const dashBadge = document.getElementById("dashStatusBadge");
        const activeBadge = document.getElementById("activeModalStatusBadge");
        if (activeBadge && dashBadge) {
            activeBadge.innerText = dashBadge.innerText || "UNKNOWN";
            activeBadge.className = dashBadge.className || "badge";
        }
    } catch (err) {
        console.error("Error setting active server display:", err);
    }

    // 3. Safely render saved servers
    try {
        renderSavedServers();
    } catch (err) {
        console.error("Error rendering saved servers:", err);
    }
}

function closeServerModal() {
    try {
        vibrate(15);
    } catch (e) {}
    const modal = document.getElementById("serverModal");
    if (modal) {
        modal.classList.remove("active");
        modal.style.display = "none";
    }
}

function quickFillTarget(val) {
    try {
        vibrate(15);
        const input = document.getElementById("txtTargetUrl");
        if (!input) return;
        input.value = val;
        input.focus();
    } catch (e) {}
}

function quickAppendPort(port) {
    try {
        vibrate(15);
        const input = document.getElementById("txtTargetUrl");
        if (!input) return;
        let val = input.value.trim();
        if (!val) {
            input.value = "http://192.168.1.100" + port;
        } else if (!val.includes(":") || val.lastIndexOf(":") <= val.indexOf("//") + 1) {
            input.value = val.replace(/\/+$/, "") + port;
        }
        input.focus();
    } catch (e) {}
}

function quickPrependHttp() {
    try {
        vibrate(15);
        const input = document.getElementById("txtTargetUrl");
        if (!input) return;
        let val = input.value.trim();
        if (val && !val.startsWith("http://") && !val.startsWith("https://")) {
            input.value = "http://" + val;
        } else if (!val) {
            input.value = "http://";
        }
        input.focus();
    } catch (e) {}
}

function clearTargetUrl() {
    try {
        vibrate(15);
        const urlInput = document.getElementById("txtTargetUrl");
        const nickInput = document.getElementById("txtTargetNickname");
        if (urlInput) urlInput.value = "";
        if (nickInput) nickInput.value = "";
    } catch (e) {}
}

function connectToCustomServer() {
    try {
        vibrate(30);
        const urlInput = document.getElementById("txtTargetUrl");
        const nickInput = document.getElementById("txtTargetNickname");
        let url = urlInput ? urlInput.value.trim() : "";
        let nickname = nickInput ? nickInput.value.trim() : "";

        if (!url) {
            showToast("Please enter a valid server URL or IP");
            return;
        }

        if (!url.startsWith("http://") && !url.startsWith("https://")) {
            url = "http://" + url;
        }

        currentServerUrl = url;
        normalizeServerUrl();

        // Save to Android persistent storage
        if (window.Android && typeof Android.saveServerUrl === "function") {
            Android.saveServerUrl(currentServerUrl);
        } else {
            localStorage.setItem("conan_server_url", currentServerUrl);
        }

        addSavedServer(currentServerUrl, nickname);
        updateHeaderDisplay();
        closeServerModal();
        pollServer();
        fetchServerConfig();
        showToast("Connecting to " + currentServerUrl + "...");
    } catch (err) {
        console.error("connectToCustomServer error:", err);
    }
}

function saveServerWithoutConnecting() {
    try {
        vibrate(25);
        const urlInput = document.getElementById("txtTargetUrl");
        const nickInput = document.getElementById("txtTargetNickname");
        let url = urlInput ? urlInput.value.trim() : "";
        let nickname = nickInput ? nickInput.value.trim() : "";

        if (!url) {
            showToast("Please enter a valid server URL or IP");
            return;
        }

        if (!url.startsWith("http://") && !url.startsWith("https://")) {
            url = "http://" + url;
        }

        addSavedServer(url, nickname);
        renderSavedServers();
        showToast("Server added to saved list!");
    } catch (err) {
        console.error("saveServerWithoutConnecting error:", err);
    }
}

function normalizeServerItem(item) {
    if (!item) return null;
    if (typeof item === "string") {
        return { url: item.trim(), name: item.trim() };
    }
    const u = (item.url || item.host || "").trim();
    if (!u) return null;
    return {
        url: u,
        name: (item.name || item.label || u).trim()
    };
}

function loadSavedServers() {
    try {
        const raw = localStorage.getItem("conan_saved_servers");
        if (raw) {
            const parsed = JSON.parse(raw);
            if (Array.isArray(parsed) && parsed.length > 0) {
                savedServers = parsed.map(normalizeServerItem).filter(Boolean);
                if (savedServers.length > 0) {
                    try {
                        localStorage.setItem("conan_saved_servers", JSON.stringify(savedServers));
                    } catch (e) {}
                    return;
                }
            }
        }
    } catch (e) {
        console.error("loadSavedServers error:", e);
    }

    savedServers = [
        { url: "http://127.0.0.1:8088", name: "Local Host (Loopback)" },
        { url: "http://192.168.1.100:8088", name: "Home Server Default" }
    ];
}

function addSavedServer(url, nickname) {
    if (!url) return;
    try {
        loadSavedServers();

        const cleanUrl = url.trim();
        const existingIdx = savedServers.findIndex(s => s && s.url && s.url.toLowerCase() === cleanUrl.toLowerCase());
        const name = (nickname && nickname.trim()) || (existingIdx >= 0 ? savedServers[existingIdx].name : cleanUrl);

        if (existingIdx >= 0) {
            savedServers.splice(existingIdx, 1);
        }

        savedServers.unshift({ url: cleanUrl, name: name });
        if (savedServers.length > 15) savedServers.pop();

        localStorage.setItem("conan_saved_servers", JSON.stringify(savedServers));
    } catch (e) {
        console.error("addSavedServer error:", e);
    }
}

function selectSavedServer(url) {
    if (!url) return;
    const input = document.getElementById("txtTargetUrl");
    if (input) input.value = url;
    connectToCustomServer();
}

function deleteSavedServer(idx, e) {
    if (e) {
        if (e.stopPropagation) e.stopPropagation();
        if (e.preventDefault) e.preventDefault();
    }
    try {
        vibrate(20);
        loadSavedServers();
        savedServers.splice(idx, 1);
        localStorage.setItem("conan_saved_servers", JSON.stringify(savedServers));
        renderSavedServers();
        showToast("Server removed from list");
    } catch (err) {
        console.error("deleteSavedServer error:", err);
    }
}

function renderSavedServers() {
    const list = document.getElementById("savedServersList");
    const countBadge = document.getElementById("savedServersCount");
    if (!list) return;

    try {
        loadSavedServers();
        if (countBadge) countBadge.innerText = savedServers.length;

        if (!savedServers || savedServers.length === 0) {
            list.innerHTML = `<div style="color:#64748b;font-size:0.85rem;text-align:center;padding:16px;">No saved servers yet. Add one above!</div>`;
            return;
        }

        list.innerHTML = savedServers.map((item, idx) => {
            const srv = (typeof item === "string") ? { url: item, name: item } : (item || { url: "", name: "" });
            const srvUrl = (srv.url || "").trim();
            const srvName = (srv.name && srv.name !== srvUrl) ? srv.name : srvUrl;
            const curUrl = (currentServerUrl || "").trim();
            const isActive = (srvUrl && curUrl && srvUrl.toLowerCase() === curUrl.toLowerCase());

            return `
                <div class="saved-server-card ${isActive ? 'active-server' : ''}" data-url="${escapeHtml(srvUrl)}" onclick="selectSavedServer(this.getAttribute('data-url'))">
                    <div class="saved-server-info">
                        <div class="saved-server-name">
                            <span>🖥️</span>
                            <span>${escapeHtml(srvName || "Server")}</span>
                            ${isActive ? '<span style="background:rgba(16,185,129,0.2);color:#34d399;font-size:0.65rem;font-weight:bold;padding:2px 6px;border-radius:6px;border:1px solid rgba(16,185,129,0.4);">ACTIVE</span>' : ''}
                        </div>
                        <div class="saved-server-url">${escapeHtml(srvUrl)}</div>
                    </div>
                    <div class="saved-server-actions">
                        <button class="btn-primary" style="padding:6px 12px;font-size:0.75rem;" data-url="${escapeHtml(srvUrl)}" onclick="event.stopPropagation(); selectSavedServer(this.getAttribute('data-url'))">▶ Connect</button>
                        <button class="btn-danger" style="padding:6px 10px;font-size:0.75rem;" onclick="deleteSavedServer(${idx}, event)">🗑️</button>
                    </div>
                </div>
            `;
        }).join("");
    } catch (err) {
        console.error("renderSavedServers error:", err);
        list.innerHTML = `<div style="color:#f87171;font-size:0.8rem;text-align:center;padding:12px;">Error displaying servers. Please re-add your server above.</div>`;
    }
}

async function scanLocalNetworkServers() {
    vibrate(20);
    const card = document.getElementById("discoveredServersCard");
    const list = document.getElementById("discoveredServersList");
    if (!card || !list) return;

    card.style.display = "block";
    list.innerHTML = `<div style="color:#38bdf8;font-size:0.85rem;text-align:center;padding:12px;">🔍 Scanning local network for Conan Server Managers...</div>`;

    const candidates = [
        "http://127.0.0.1:8088",
        "http://localhost:8088",
        currentServerUrl
    ];

    const prefixes = new Set();

    // 1. Get phone's actual Wi-Fi IP from Android native bridge
    if (window.Android && typeof Android.getLocalIpAddress === "function") {
        const phoneIp = Android.getLocalIpAddress();
        if (phoneIp && phoneIp.includes(".")) {
            const pParts = phoneIp.split(".");
            if (pParts.length === 4) {
                prefixes.add(pParts.slice(0, 3).join("."));
            }
        }
    }

    // 2. Extract base IP subnet from currentServerUrl
    try {
        const u = new URL(currentServerUrl);
        const parts = u.hostname.split('.');
        if (parts.length === 4) {
            prefixes.add(parts.slice(0, 3).join('.'));
        }
    } catch {}

    // 3. Fallback common private subnets
    prefixes.add("192.168.0");
    prefixes.add("192.168.1");

    const probes = [1, 2, 5, 10, 20, 50, 100, 101, 102, 105, 110, 120, 150, 200, 254];
    prefixes.forEach(prefix => {
        probes.forEach(n => {
            const candidate = `http://${prefix}.${n}:8088`;
            if (!candidates.includes(candidate)) candidates.push(candidate);
        });
    });

    const foundServers = [];

    await Promise.all(candidates.map(async (targetUrl) => {
        try {
            const controller = new AbortController();
            const timeoutId = setTimeout(() => controller.abort(), 1200);
            const res = await fetch(targetUrl + "/api/status?_t=" + Date.now(), { signal: controller.signal });
            clearTimeout(timeoutId);

            if (res.ok) {
                const data = await res.json();
                foundServers.push({
                    url: targetUrl,
                    name: data.serverName || "Conan Dedicated Server",
                    status: data.status || "ONLINE",
                    players: (data.players && data.players.length) || data.steamPlayers || 0
                });
            }
        } catch {}
    }));

    if (foundServers.length === 0) {
        list.innerHTML = `<div style="color:#94a3b8;font-size:0.85rem;text-align:center;padding:12px;">No managers detected on standard subnet ports. Try entering your IP manually above.</div>`;
    } else {
        list.innerHTML = foundServers.map(s => `
            <div class="saved-server-card active-server" style="border-color:#6366f1;margin-bottom:8px;" data-url="${escapeHtml(s.url)}" onclick="selectSavedServer(this.getAttribute('data-url'))">
                <div class="saved-server-info">
                    <div class="saved-server-name">
                        <span>🌐</span>
                        <span>${escapeHtml(s.name)}</span>
                        <span style="background:rgba(99,102,241,0.2);color:#818cf8;font-size:0.65rem;font-weight:bold;padding:2px 6px;border-radius:6px;border:1px solid rgba(99,102,241,0.4);">${s.status} (${s.players} pl)</span>
                    </div>
                    <div class="saved-server-url">${escapeHtml(s.url)}</div>
                </div>
                <button class="btn-primary" style="padding:6px 12px;font-size:0.75rem;" data-url="${escapeHtml(s.url)}" onclick="event.stopPropagation(); selectSavedServer(this.getAttribute('data-url'))">▶ Connect</button>
            </div>
        `).join("");
    }
}

function handleBackPressed() {
    const modModal = document.getElementById("modDetailsModal");
    if (modModal && modModal.style.display && modModal.style.display !== "none") {
        closeModDetailsModal();
        return "handled";
    }
    const modal = document.getElementById("serverModal");
    if (modal && (modal.classList.contains("active") || (modal.style.display && modal.style.display !== "none"))) {
        closeServerModal();
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
    return String(str)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

// Explicitly register global helper functions on window
window.openServerModal = openServerModal;
window.closeServerModal = closeServerModal;
window.selectSavedServer = selectSavedServer;
window.deleteSavedServer = deleteSavedServer;
window.connectToCustomServer = connectToCustomServer;
window.saveServerWithoutConnecting = saveServerWithoutConnecting;
window.scanLocalNetworkServers = scanLocalNetworkServers;
window.handleBackPressed = handleBackPressed;
window.switchTab = switchTab;
window.controlAction = controlAction;
window.searchWorkshop = searchWorkshop;
window.installMod = installMod;
window.uninstallMod = uninstallMod;
window.fetchInstalledMods = fetchInstalledMods;
window.saveServerConfig = saveServerConfig;
window.sendRcon = sendRcon;
window.checkAppUpdates = checkAppUpdates;
window.installUpdate = installUpdate;
window.downloadFromServerDirect = downloadFromServerDirect;
window.downloadFromGithubDirect = downloadFromGithubDirect;
window.togglePasswordVisibility = togglePasswordVisibility;
window.fetchServerConfig = fetchServerConfig;
window.openModDetailsModal = showModDetailsModal;
window.closeModDetailsModal = closeModDetailsModal;
window.onModalOpenWorkshop = onModalOpenWorkshop;
window.onModalCopyLink = onModalCopyLink;
window.onModalPreDownload = onModalPreDownload;
window.preDownloadMod = preDownloadMod;
window.openSteamWorkshopBrowser = openSteamWorkshopBrowser;
window.addModToServer = addModToServer;

