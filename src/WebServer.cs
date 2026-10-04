using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ConanServerManager
{
    public class WebServer
    {
        private readonly ServerEngine _engine;
        private TcpListener? _tcpListener;
        private bool _isListening;
        private readonly ConcurrentQueue<string> _logBuffer = new ConcurrentQueue<string>();
        private DateTime _startTime = DateTime.Now;

        public bool IsRunning => _isListening;

        public WebServer(ServerEngine engine)
        {
            _engine = engine;
            _engine.OnLog += msg =>
            {
                _logBuffer.Enqueue(msg);
                while (_logBuffer.Count > 500) _logBuffer.TryDequeue(out _);
            };
        }

        public async Task StartAsync(int port = 8088)
        {
            if (_isListening) return;

            await Task.Run(() =>
            {
                try
                {
                    _tcpListener = new TcpListener(IPAddress.Any, port);
                    _tcpListener.Start();
                    _isListening = true;
                    _engine.Log($"Embedded Web Server & Remote API active on http://0.0.0.0:{port} (All hostnames & IPs allowed)");

                    _ = Task.Run(ListenLoop);
                }
                catch (Exception ex)
                {
                    _engine.Log($"Embedded Web Server start error on port {port}: {ex.Message}");
                }
            });
        }

        public void Stop()
        {
            _isListening = false;
            try { _tcpListener?.Stop(); } catch { }
            _tcpListener = null;
        }

        private async Task ListenLoop()
        {
            while (_isListening && _tcpListener != null)
            {
                try
                {
                    var client = await _tcpListener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleTcpClientAsync(client));
                }
                catch
                {
                    if (!_isListening) break;
                }
            }
        }

        private async Task HandleTcpClientAsync(TcpClient client)
        {
            using (client)
            {
                try
                {
                    client.ReceiveTimeout = 5000;
                    client.SendTimeout = 5000;
                    using var stream = client.GetStream();

                    byte[] buffer = new byte[8192];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead <= 0) return;

                    string requestHeaderStr = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    int headerEnd = requestHeaderStr.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                    if (headerEnd == -1) headerEnd = requestHeaderStr.IndexOf("\n\n", StringComparison.Ordinal);

                    string headersPart = headerEnd != -1 ? requestHeaderStr.Substring(0, headerEnd) : requestHeaderStr;
                    string body = "";

                    var lines = headersPart.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    if (lines.Length == 0) return;

                    var reqLineParts = lines[0].Split(' ');
                    if (reqLineParts.Length < 2) return;

                    string method = reqLineParts[0].ToUpperInvariant();
                    string rawUrl = reqLineParts[1];
                    string path = rawUrl.Split('?')[0].ToLowerInvariant();

                    int contentLength = 0;
                    foreach (var headerLine in lines.Skip(1))
                    {
                        if (headerLine.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        {
                            int.TryParse(headerLine.Substring(15).Trim(), out contentLength);
                        }
                    }

                    if (contentLength > 0)
                    {
                        int bodyStartIdx = headerEnd != -1 ? (requestHeaderStr.Contains("\r\n\r\n") ? headerEnd + 4 : headerEnd + 2) : bytesRead;
                        int existingBodyBytes = bytesRead - bodyStartIdx;

                        using var ms = new MemoryStream();
                        if (existingBodyBytes > 0)
                        {
                            ms.Write(buffer, bodyStartIdx, existingBodyBytes);
                        }

                        while (ms.Length < contentLength)
                        {
                            int needed = contentLength - (int)ms.Length;
                            int r = await stream.ReadAsync(buffer, 0, Math.Min(buffer.Length, needed));
                            if (r <= 0) break;
                            ms.Write(buffer, 0, r);
                        }

                        body = Encoding.UTF8.GetString(ms.ToArray());
                    }

                    await ProcessRequestAsync(stream, method, path, body, rawUrl);
                }
                catch
                {
                    // Ignore socket disconnects
                }
            }
        }

        private async Task ProcessRequestAsync(Stream stream, string method, string path, string body, string rawUrl)
        {
            if (method == "OPTIONS")
            {
                await SendHttpResponseAsync(stream, 200, "text/plain", "OK");
                return;
            }

            if (path == "/" || path == "/index.html")
            {
                await SendHttpResponseAsync(stream, 200, "text/html; charset=utf-8", GetEmbeddedHtmlDashboard());
            }
            else if (path == "/api/status" && method == "GET")
            {
                TimeSpan uptime = _engine.ServerStatus == "RUNNING" ? DateTime.Now - _startTime : TimeSpan.Zero;
                var prog = _engine.CurrentDownloadProgress;
                var statusObj = new
                {
                    status = _engine.ServerStatus,
                    serverName = _engine.Config.ServerName,
                    gamePort = _engine.Config.GamePort,
                    rconPort = _engine.Config.RconPort,
                    maxPlayers = _engine.Config.MaxPlayers,
                    activeModsCount = _engine.Config.Mods.Count,
                    uptimeSeconds = (int)uptime.TotalSeconds,
                    uptimeString = $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}",
                    executableExists = File.Exists(_engine.ExecutablePath),
                    steamCmdExists = File.Exists(_engine.SteamCmdExe),
                    downloadActive = prog != null && prog.IsActive,
                    downloadItem = prog?.ItemName ?? "",
                    downloadPercent = prog?.Percent ?? 0,
                    downloadSpeedMBs = prog?.SpeedMBs ?? 0,
                    downloadEtaString = prog?.Eta != null && prog.Eta > TimeSpan.Zero ? prog.Eta.ToString(@"hh\:mm\:ss") : "--:--:--",
                    appVersion = ServerEngine.CurrentAppVersion,
                    latestAppVersion = _engine.LatestAppUpdate?.TagName ?? "",
                    updateAvailable = _engine.LatestAppUpdate?.IsNewer ?? false,
                    updateNotes = _engine.LatestAppUpdate?.ReleaseNotes ?? ""
                };
                await SendHttpResponseAsync(stream, 200, "application/json", JsonSerializer.Serialize(statusObj));
            }
            else if (path == "/api/control/check-update" && method == "POST")
            {
                var update = await _engine.CheckForAppUpdateAsync();
                await SendHttpResponseAsync(stream, 200, "application/json", JsonSerializer.Serialize(new { success = true, update = update }));
            }
            else if (path == "/api/control/apply-update" && method == "POST")
            {
                if (_engine.LatestAppUpdate != null && _engine.LatestAppUpdate.IsNewer)
                {
                    _ = Task.Run(async () => await _engine.DownloadAndApplyAppUpdateAsync(_engine.LatestAppUpdate));
                    await SendHttpResponseAsync(stream, 200, "application/json", "{\"success\": true, \"message\": \"Update application sequence initiated.\"}");
                }
                else
                {
                    await SendHttpResponseAsync(stream, 200, "application/json", "{\"success\": false, \"message\": \"No newer update available to install.\"}");
                }
            }
            else if (path == "/api/logs" && method == "GET")
            {
                var logs = _logBuffer.ToArray();
                await SendHttpResponseAsync(stream, 200, "application/json", JsonSerializer.Serialize(logs));
            }
            else if (path == "/api/control/start" && method == "POST")
            {
                _startTime = DateTime.Now;
                _ = Task.Run(async () => await _engine.RunFullUpdateAndStartAsync());
                await SendHttpResponseAsync(stream, 200, "application/json", "{\"success\": true, \"message\": \"Start sequence initiated.\"}");
            }
            else if (path == "/api/control/stop" && method == "POST")
            {
                _ = Task.Run(async () => await _engine.StopServerAsync());
                await SendHttpResponseAsync(stream, 200, "application/json", "{\"success\": true, \"message\": \"Stop sequence initiated.\"}");
            }
            else if (path == "/api/control/restart" && method == "POST")
            {
                _ = Task.Run(async () =>
                {
                    await _engine.StopServerAsync();
                    await Task.Delay(3000);
                    _startTime = DateTime.Now;
                    await _engine.RunFullUpdateAndStartAsync();
                });
                await SendHttpResponseAsync(stream, 200, "application/json", "{\"success\": true, \"message\": \"Restart sequence initiated.\"}");
            }
            else if (path == "/api/control/backup" && method == "POST")
            {
                string res = await _engine.CreateHotBackupAsync();
                await SendHttpResponseAsync(stream, 200, "application/json", JsonSerializer.Serialize(new { success = true, result = res }));
            }
            else if (path == "/api/control/rcon" && method == "POST")
            {
                string cmd = "";
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    cmd = doc.RootElement.GetProperty("command").GetString() ?? "";
                }
                catch { }

                if (string.IsNullOrWhiteSpace(cmd))
                {
                    await SendHttpResponseAsync(stream, 400, "application/json", "{\"error\": \"Missing command property.\"}");
                    return;
                }

                if (_engine.ServerStatus != "RUNNING")
                {
                    await SendHttpResponseAsync(stream, 200, "application/json", "{\"success\": false, \"message\": \"Server is not running.\"}");
                    return;
                }

                try
                {
                    using var rcon = new ValveRconClient("127.0.0.1", _engine.Config.RconPort, _engine.Config.RconPassword);
                    await rcon.ConnectAsync();
                    string reply = await rcon.ExecuteAsync(cmd);
                    await SendHttpResponseAsync(stream, 200, "application/json", JsonSerializer.Serialize(new { success = true, command = cmd, reply = reply }));
                }
                catch (Exception ex)
                {
                    await SendHttpResponseAsync(stream, 200, "application/json", JsonSerializer.Serialize(new { success = false, error = ex.Message }));
                }
            }
            else if (path == "/api/config" && method == "GET")
            {
                await SendHttpResponseAsync(stream, 200, "application/json", JsonSerializer.Serialize(_engine.Config));
            }
            else if (path == "/api/config" && method == "POST")
            {
                try
                {
                    var cfg = JsonSerializer.Deserialize<ManagerConfig>(body);
                    if (cfg != null)
                    {
                        _engine.Config.ServerName = cfg.ServerName;
                        _engine.Config.ServerPassword = cfg.ServerPassword;
                        _engine.Config.AdminPassword = cfg.AdminPassword;
                        _engine.Config.RconPassword = cfg.RconPassword;
                        _engine.Config.GamePort = cfg.GamePort;
                        _engine.Config.RawUdpPort = cfg.RawUdpPort;
                        _engine.Config.QueryPort = cfg.QueryPort;
                        _engine.Config.RconPort = cfg.RconPort;
                        _engine.Config.MaxPlayers = cfg.MaxPlayers;
                        _engine.Config.Mods = cfg.Mods;
                        _engine.SaveConfig();
                    }
                    await SendHttpResponseAsync(stream, 200, "application/json", "{\"success\": true, \"message\": \"Manager config updated remotely.\"}");
                }
                catch (Exception ex)
                {
                    await SendHttpResponseAsync(stream, 400, "application/json", JsonSerializer.Serialize(new { success = false, error = ex.Message }));
                }
            }
            else if (path == "/api/ini" && method == "GET")
            {
                string queryFile = "";
                if (rawUrl.Contains("?"))
                {
                    var qParts = rawUrl.Split('?')[1].Split('&');
                    foreach (var q in qParts)
                    {
                        var kv = q.Split('=');
                        if (kv.Length == 2 && kv[0].Equals("file", StringComparison.OrdinalIgnoreCase))
                        {
                            queryFile = WebUtility.UrlDecode(kv[1]);
                        }
                    }
                }

                string filePath = queryFile.Equals("Engine.ini", StringComparison.OrdinalIgnoreCase) ? _engine.EngineIni :
                                 queryFile.Equals("Game.ini", StringComparison.OrdinalIgnoreCase) ? _engine.GameIni : _engine.ServerSettingsIni;

                string content = _engine.GetIniText(filePath);
                await SendHttpResponseAsync(stream, 200, "application/json", JsonSerializer.Serialize(new { file = Path.GetFileName(filePath), content = content }));
            }
            else if (path == "/api/ini" && method == "POST")
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    string file = doc.RootElement.GetProperty("file").GetString() ?? "ServerSettings.ini";
                    string content = doc.RootElement.GetProperty("content").GetString() ?? "";

                    string filePath = file.Equals("Engine.ini", StringComparison.OrdinalIgnoreCase) ? _engine.EngineIni :
                                     file.Equals("Game.ini", StringComparison.OrdinalIgnoreCase) ? _engine.GameIni : _engine.ServerSettingsIni;

                    _engine.SaveIniText(filePath, content);
                    _engine.AutoDetectAndImportIniSettings();
                    await SendHttpResponseAsync(stream, 200, "application/json", "{\"success\": true, \"message\": \"INI file updated remotely.\"}");
                }
                catch (Exception ex)
                {
                    await SendHttpResponseAsync(stream, 400, "application/json", JsonSerializer.Serialize(new { success = false, error = ex.Message }));
                }
            }
            else
            {
                await SendHttpResponseAsync(stream, 404, "application/json", "{\"error\": \"Endpoint not found.\"}");
            }
        }

        private async Task SendHttpResponseAsync(Stream stream, int statusCode, string contentType, string content)
        {
            byte[] contentBytes = Encoding.UTF8.GetBytes(content);
            string statusText = statusCode == 200 ? "OK" : (statusCode == 400 ? "Bad Request" : (statusCode == 404 ? "Not Found" : "Internal Server Error"));

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"HTTP/1.1 {statusCode} {statusText}");
            sb.AppendLine($"Content-Type: {contentType}");
            sb.AppendLine($"Content-Length: {contentBytes.Length}");
            sb.AppendLine("Access-Control-Allow-Origin: *");
            sb.AppendLine("Access-Control-Allow-Methods: GET, POST, OPTIONS");
            sb.AppendLine("Access-Control-Allow-Headers: Content-Type");
            sb.AppendLine("Connection: close");
            sb.AppendLine();

            byte[] headerBytes = Encoding.UTF8.GetBytes(sb.ToString());
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
            await stream.WriteAsync(contentBytes, 0, contentBytes.Length);
            await stream.FlushAsync();
        }

        private string GetEmbeddedHtmlDashboard()
        {
            return @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Conan Exiles Server Remote Console</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif; }
        body { background: #0f172a; color: #f8fafc; padding: 16px; max-width: 900px; margin: 0 auto; }
        .card { background: #1e293b; border: 1px solid #334155; border-radius: 12px; padding: 16px; margin-bottom: 16px; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1); }
        .header { display: flex; justify-content: space-between; align-items: center; }
        .title { font-size: 1.25rem; font-weight: bold; color: #818cf8; }
        .subtitle { font-size: 0.85rem; color: #94a3b8; }
        .badge { padding: 6px 14px; border-radius: 20px; font-weight: bold; font-size: 0.85rem; letter-spacing: 0.5px; }
        .badge-running { background: rgba(16, 185, 129, 0.2); border: 1px solid rgba(16, 185, 129, 0.4); color: #34d399; }
        .badge-stopped { background: rgba(239, 68, 68, 0.2); border: 1px solid rgba(239, 68, 68, 0.4); color: #f87171; }
        .badge-updating { background: rgba(245, 158, 11, 0.2); border: 1px solid rgba(245, 158, 11, 0.4); color: #fbbf24; }
        
        .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(130px, 1fr)); gap: 12px; margin-top: 14px; }
        .metric-box { background: #0f172a; border-radius: 8px; padding: 12px; text-align: center; border: 1px solid #334155; }
        .metric-val { font-size: 1.2rem; font-weight: bold; color: #38bdf8; margin-top: 4px; }
        .metric-lbl { font-size: 0.75rem; color: #94a3b8; text-transform: uppercase; letter-spacing: 0.5px; }

        .btn-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 10px; margin-top: 10px; }
        button { border: none; border-radius: 8px; padding: 12px; font-weight: bold; font-size: 0.9rem; cursor: pointer; transition: all 0.2s; }
        button:active { transform: scale(0.98); }
        .btn-start { background: #6366f1; color: white; }
        .btn-stop { background: #ef4444; color: white; }
        .btn-restart { background: #f59e0b; color: white; }
        .btn-backup { background: #334155; color: #e2e8f0; }
        .btn-send { background: #10b981; color: white; padding: 10px 18px; }

        .log-box { background: #020617; border: 1px solid #1e293b; border-radius: 8px; padding: 12px; height: 320px; overflow-y: auto; font-family: 'Consolas', monospace; font-size: 0.8rem; color: #38bdf8; white-space: pre-wrap; line-height: 1.4; }
        .rcon-bar { display: flex; gap: 8px; margin-top: 10px; }
        input[type=""text""] { flex: 1; background: #0f172a; border: 1px solid #334155; border-radius: 8px; padding: 10px; color: white; font-size: 0.9rem; }
    </style>
</head>
<body>
    <div class=""card"">
        <div class=""header"">
            <div>
                <div class=""title"" id=""srvName"">Conan Exiles Server</div>
                <div class=""subtitle"">Remote Mobile & Web Control Console</div>
            </div>
            <div id=""statusBadge"" class=""badge badge-stopped"">STOPPED</div>
        </div>

        <div class=""metrics-grid"">
            <div class=""metric-box"">
                <div class=""metric-lbl"">Uptime</div>
                <div class=""metric-val"" id=""valUptime"">00:00:00</div>
            </div>
            <div class=""metric-box"">
                <div class=""metric-lbl"">Game Port</div>
                <div class=""metric-val"" id=""valGamePort"">7777</div>
            </div>
            <div class=""metric-box"">
                <div class=""metric-lbl"">RCON Port</div>
                <div class=""metric-val"" id=""valRconPort"">25575</div>
            </div>
            <div class=""metric-box"">
                <div class=""metric-lbl"">Active Mods</div>
                <div class=""metric-val"" id=""valMods"">0</div>
            </div>
        </div>
    </div>

    <div class=""card"">
        <div class=""subtitle"" style=""margin-bottom: 8px; text-transform: uppercase; letter-spacing: 0.5px; font-weight: bold;"">Remote Server Controls</div>
        <div class=""btn-grid"">
            <button class=""btn-start"" onclick=""triggerAction('start')"">▶ Start Server</button>
            <button class=""btn-stop"" onclick=""triggerAction('stop')"">■ Stop Server</button>
            <button class=""btn-restart"" onclick=""triggerAction('restart')"">🔄 Restart</button>
            <button class=""btn-backup"" onclick=""triggerAction('backup')"">💾 Hot Backup</button>
        </div>
    </div>

    <div class=""card"">
        <div class=""subtitle"" style=""margin-bottom: 8px; text-transform: uppercase; letter-spacing: 0.5px; font-weight: bold;"">Live Log Output</div>
        <div class=""log-box"" id=""logConsole"">Connecting to live log stream...</div>

        <div class=""rcon-bar"">
            <input type=""text"" id=""txtRconCmd"" placeholder=""Enter RCON command (e.g. save or broadcast Hello)"">
            <button class=""btn-send"" onclick=""sendRcon()"">Send RCON</button>
        </div>
    </div>

    <script>
        async function fetchStatus() {
            try {
                const res = await fetch('/api/status');
                const data = await res.json();

                document.getElementById('srvName').innerText = data.serverName || 'Conan Exiles Server';
                document.getElementById('valGamePort').innerText = data.gamePort;
                document.getElementById('valRconPort').innerText = data.rconPort;
                document.getElementById('valMods').innerText = data.activeModsCount;
                document.getElementById('valUptime').innerText = data.uptimeString || '00:00:00';

                const badge = document.getElementById('statusBadge');
                badge.innerText = data.status;
                if (data.status === 'RUNNING') badge.className = 'badge badge-running';
                else if (data.status === 'UPDATING') badge.className = 'badge badge-updating';
                else badge.className = 'badge badge-stopped';
            } catch (e) {}
        }

        async function fetchLogs() {
            try {
                const res = await fetch('/api/logs');
                const logs = await res.json();
                const logConsole = document.getElementById('logConsole');
                logConsole.innerText = logs.join('\n');
                logConsole.scrollTop = logConsole.scrollHeight;
            } catch (e) {}
        }

        async function triggerAction(act) {
            if (!confirm('Confirm ' + act.toUpperCase() + ' command?')) return;
            try {
                const res = await fetch('/api/control/' + act, { method: 'POST' });
                const data = await res.json();
                alert(data.message || data.result || 'Command sent successfully.');
            } catch (e) {
                alert('Action failed: ' + e.message);
            }
        }

        async function sendRcon() {
            const input = document.getElementById('txtRconCmd');
            const cmd = input.value.trim();
            if (!cmd) return;

            try {
                const res = await fetch('/api/control/rcon', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ command: cmd })
                });
                const data = await res.json();
                if (data.success) {
                    alert('RCON Response:\n' + data.reply);
                    input.value = '';
                } else {
                    alert('RCON Failed: ' + (data.message || data.error));
                }
            } catch (e) {
                alert('RCON request failed: ' + e.message);
            }
        }

        setInterval(fetchStatus, 3000);
        setInterval(fetchLogs, 2000);
        fetchStatus();
        fetchLogs();
    </script>
</body>
</html>";
        }
    }
}
