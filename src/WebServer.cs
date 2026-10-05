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

                    DiscoveryHelper.StartDiscoveryListener(
                        () => _engine.Config.ServerName,
                        () => _engine.Config.WebPagePort,
                        () => _engine.Config.GamePort
                    );

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
            DiscoveryHelper.StopDiscoveryListener();
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
                    updateNotes = _engine.LatestAppUpdate?.ReleaseNotes ?? "",
                    steamOnline = _engine.SteamStatus.IsOnline,
                    steamServerName = _engine.SteamStatus.ServerName,
                    steamMap = _engine.SteamStatus.Map,
                    steamPlayers = _engine.SteamStatus.Players,
                    steamMaxPlayers = _engine.SteamStatus.MaxPlayers,
                    steamPing = _engine.SteamStatus.PingMs,
                    steamError = _engine.SteamStatus.ErrorMessage,
                    players = _engine.ConnectedPlayers.Select(p => new
                    {
                        index = p.Index,
                        name = p.Name,
                        score = p.Score,
                        durationSeconds = p.DurationSeconds,
                        durationFormatted = p.DurationFormatted,
                        ping = p.Ping
                    }).ToList()
                };
                await SendHttpResponseAsync(stream, 200, "application/json", JsonSerializer.Serialize(statusObj));
            }
            else if (path == "/api/players" && method == "GET")
            {
                await SendHttpResponseAsync(stream, 200, "application/json", JsonSerializer.Serialize(_engine.ConnectedPlayers));
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
                var cfg = _engine.Config;
                var res = new
                {
                    serverName = cfg.ServerName,
                    serverPassword = cfg.ServerPassword,
                    adminPassword = cfg.AdminPassword,
                    rconPassword = cfg.RconPassword,
                    rconPort = cfg.RconPort,
                    gamePort = cfg.GamePort,
                    rawUdpPort = cfg.RawUdpPort,
                    queryPort = cfg.QueryPort,
                    maxPlayers = cfg.MaxPlayers,
                    maxTickRate = cfg.MaxTickRate,
                    region = cfg.Region,
                    serverRegion = cfg.Region,
                    enableBattlEye = cfg.EnableBattlEye,
                    battlEyeEnabled = cfg.EnableBattlEye,
                    enableVAC = cfg.EnableVAC,
                    vacEnabled = cfg.EnableVAC,
                    // PascalCase aliases for backward compatibility
                    ServerName = cfg.ServerName,
                    ServerPassword = cfg.ServerPassword,
                    AdminPassword = cfg.AdminPassword,
                    RconPassword = cfg.RconPassword,
                    RconPort = cfg.RconPort,
                    GamePort = cfg.GamePort,
                    MaxPlayers = cfg.MaxPlayers,
                    MaxTickRate = cfg.MaxTickRate,
                    Region = cfg.Region,
                    EnableBattlEye = cfg.EnableBattlEye,
                    EnableVAC = cfg.EnableVAC
                };
                await SendHttpResponseAsync(stream, 200, "application/json", JsonSerializer.Serialize(res));
            }
            else if (path == "/api/config" && method == "POST")
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    _engine.UpdateSettingsFromRemote(doc.RootElement);
                    await SendHttpResponseAsync(stream, 200, "application/json", "{\"success\": true, \"message\": \"Server configuration updated successfully.\"}");
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
            else if (path == "/api/workshop/search" && method == "GET")
            {
                string query = "";
                if (rawUrl.Contains("?"))
                {
                    var qParts = rawUrl.Split('?')[1].Split('&');
                    foreach (var q in qParts)
                    {
                        var kv = q.Split('=');
                        if (kv.Length == 2 && kv[0].Equals("query", StringComparison.OrdinalIgnoreCase))
                        {
                            query = WebUtility.UrlDecode(kv[1]);
                        }
                    }
                }

                var results = await SteamWorkshopHelper.SearchModsAsync(query);
                var installedSet = new HashSet<string>(_engine.Config.Mods, StringComparer.OrdinalIgnoreCase);
                foreach (var r in results)
                {
                    r.IsInstalled = installedSet.Contains(r.Id);
                }

                await SendHttpResponseAsync(stream, 200, "application/json", JsonSerializer.Serialize(results));
            }
            else if (path == "/api/workshop/details" && method == "GET")
            {
                string modId = "";
                if (rawUrl.Contains("?"))
                {
                    var qParts = rawUrl.Split('?')[1].Split('&');
                    foreach (var q in qParts)
                    {
                        var kv = q.Split('=');
                        if (kv.Length == 2 && kv[0].Equals("id", StringComparison.OrdinalIgnoreCase))
                        {
                            modId = WebUtility.UrlDecode(kv[1]);
                        }
                    }
                }

                var item = await SteamWorkshopHelper.GetModDetailsAsync(modId);
                if (item != null)
                {
                    item.IsInstalled = _engine.Config.Mods.Contains(item.Id, StringComparer.OrdinalIgnoreCase);
                    await SendHttpResponseAsync(stream, 200, "application/json", JsonSerializer.Serialize(item));
                }
                else
                {
                    await SendHttpResponseAsync(stream, 404, "application/json", "{\"error\": \"Mod not found or invalid ID.\"}");
                }
            }
            else if (path == "/api/mods" && method == "GET")
            {
                var modList = new List<WorkshopModItem>();
                foreach (var modId in _engine.Config.Mods)
                {
                    var details = await SteamWorkshopHelper.GetModDetailsAsync(modId);
                    if (details != null)
                    {
                        details.IsInstalled = true;
                        modList.Add(details);
                    }
                    else
                    {
                        modList.Add(new WorkshopModItem
                        {
                            Id = modId,
                            Title = $"Mod #{modId}",
                            IsInstalled = true
                        });
                    }
                }
                await SendHttpResponseAsync(stream, 200, "application/json", JsonSerializer.Serialize(new { mods = _engine.Config.Mods, details = modList }));
            }
            else if (path == "/api/mods/add" && method == "POST")
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    string modId = doc.RootElement.GetProperty("modId").GetString()?.Trim() ?? "";

                    if (string.IsNullOrWhiteSpace(modId))
                    {
                        await SendHttpResponseAsync(stream, 400, "application/json", "{\"success\": false, \"error\": \"modId is required.\"}");
                        return;
                    }

                    if (!_engine.Config.Mods.Contains(modId, StringComparer.OrdinalIgnoreCase))
                    {
                        _engine.Config.Mods.Add(modId);
                        _engine.SaveConfig();
                        _engine.SyncIniSettings();
                        _engine.GenerateModlistFile();
                        _engine.Log($"[Remote Admin] Added Steam Workshop Mod #{modId} to server mod list.");
                    }

                    await SendHttpResponseAsync(stream, 200, "application/json", JsonSerializer.Serialize(new { success = true, mods = _engine.Config.Mods }));
                }
                catch (Exception ex)
                {
                    await SendHttpResponseAsync(stream, 400, "application/json", JsonSerializer.Serialize(new { success = false, error = ex.Message }));
                }
            }
            else if (path == "/api/mods/remove" && method == "POST")
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    string modId = doc.RootElement.GetProperty("modId").GetString()?.Trim() ?? "";

                    _engine.Config.Mods.RemoveAll(x => x.Equals(modId, StringComparison.OrdinalIgnoreCase));
                    _engine.SaveConfig();
                    _engine.SyncIniSettings();
                    _engine.GenerateModlistFile();
                    _engine.Log($"[Remote Admin] Removed Steam Workshop Mod #{modId} from server mod list.");

                    await SendHttpResponseAsync(stream, 200, "application/json", JsonSerializer.Serialize(new { success = true, mods = _engine.Config.Mods }));
                }
                catch (Exception ex)
                {
                    await SendHttpResponseAsync(stream, 400, "application/json", JsonSerializer.Serialize(new { success = false, error = ex.Message }));
                }
            }
            else if (path == "/api/mods/reorder" && method == "POST")
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("mods", out var modsArr) && modsArr.ValueKind == JsonValueKind.Array)
                    {
                        var newOrder = new List<string>();
                        foreach (var el in modsArr.EnumerateArray())
                        {
                            string id = el.GetString()?.Trim() ?? "";
                            if (!string.IsNullOrWhiteSpace(id) && !newOrder.Contains(id))
                            {
                                newOrder.Add(id);
                            }
                        }
                        _engine.Config.Mods = newOrder;
                        _engine.SaveConfig();
                        _engine.SyncIniSettings();
                        _engine.GenerateModlistFile();
                        _engine.Log($"[Remote Admin] Reordered server mod list ({newOrder.Count} mods).");
                    }

                    await SendHttpResponseAsync(stream, 200, "application/json", JsonSerializer.Serialize(new { success = true, mods = _engine.Config.Mods }));
                }
                catch (Exception ex)
                {
                    await SendHttpResponseAsync(stream, 400, "application/json", JsonSerializer.Serialize(new { success = false, error = ex.Message }));
                }
            }
            else if ((path == "/api/download/apk" || path == "/conan.apk" || path == "/app.apk") && method == "GET")
            {
                string apkName = $"ConanServerManager-v{ServerEngine.CurrentAppVersion}.apk";
                string apkPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, apkName);
                if (!File.Exists(apkPath))
                {
                    apkPath = Path.Combine(Directory.GetCurrentDirectory(), apkName);
                }
                if (!File.Exists(apkPath))
                {
                    // Fallback to find any ConanServerManager-*.apk in current or base dir
                    var found = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "ConanServerManager-*.apk");
                    if (found.Length > 0) apkPath = found[0];
                    else
                    {
                        found = Directory.GetFiles(Directory.GetCurrentDirectory(), "ConanServerManager-*.apk");
                        if (found.Length > 0) apkPath = found[0];
                    }
                }

                if (File.Exists(apkPath))
                {
                    byte[] apkBytes = await File.ReadAllBytesAsync(apkPath);
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("HTTP/1.1 200 OK");
                    sb.AppendLine("Content-Type: application/vnd.android.package-archive");
                    sb.AppendLine($"Content-Length: {apkBytes.Length}");
                    sb.AppendLine($"Content-Disposition: attachment; filename=\"{Path.GetFileName(apkPath)}\"");
                    sb.AppendLine("Access-Control-Allow-Origin: *");
                    sb.AppendLine("Connection: close");
                    sb.AppendLine();

                    byte[] headerBytes = Encoding.UTF8.GetBytes(sb.ToString());
                    await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
                    await stream.WriteAsync(apkBytes, 0, apkBytes.Length);
                    await stream.FlushAsync();
                    return;
                }
                else
                {
                    await SendHttpResponseAsync(stream, 404, "application/json", "{\"error\": \"APK not found on server host.\"}");
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
    <title>Conan Exiles Server Remote Console (v__APP_VER__)</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif; }
        body { background: #0f172a; color: #f8fafc; padding: 16px; max-width: 900px; margin: 0 auto; }
        .card { background: #1e293b; border: 1px solid #334155; border-radius: 12px; padding: 16px; margin-bottom: 16px; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1); }
        .header { display: flex; justify-content: space-between; align-items: center; }
        .title { font-size: 1.25rem; font-weight: bold; color: #818cf8; }
        .subtitle { font-size: 0.85rem; color: #94a3b8; display: flex; align-items: center; gap: 8px; margin-top: 2px; flex-wrap: wrap; }
        .badge { padding: 6px 14px; border-radius: 20px; font-weight: bold; font-size: 0.85rem; letter-spacing: 0.5px; }
        .badge-running { background: rgba(16, 185, 129, 0.2); border: 1px solid rgba(16, 185, 129, 0.4); color: #34d399; }
        .badge-stopped { background: rgba(239, 68, 68, 0.2); border: 1px solid rgba(239, 68, 68, 0.4); color: #f87171; }
        .badge-updating { background: rgba(245, 158, 11, 0.2); border: 1px solid rgba(245, 158, 11, 0.4); color: #fbbf24; }
        .badge-ver { background: rgba(56, 189, 248, 0.15); color: #38bdf8; border: 1px solid rgba(56, 189, 248, 0.4); padding: 2px 8px; border-radius: 10px; font-weight: bold; font-size: 0.75rem; }
        .btn-apk { display: inline-flex; align-items: center; gap: 4px; background: #6366f1; color: white; text-decoration: none; padding: 4px 10px; border-radius: 6px; font-size: 0.75rem; font-weight: bold; }
        .btn-apk:hover { background: #4f46e5; }
        
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
                <div class=""subtitle"">
                    <span>Remote Mobile &amp; Web Control Console</span>
                    <span id=""appVerBadge"" class=""badge-ver"">v__APP_VER__</span>
                    <a href=""/conan.apk"" class=""btn-apk"">📱 Download Android App (.apk)</a>
                </div>
            </div>
            <div id=""statusBadge"" class=""badge badge-stopped"">STOPPED</div>
        </div>

        <div class=""metrics-grid"">
            <div class=""metric-box"">
                <div class=""metric-lbl"">Players Online</div>
                <div class=""metric-val"" id=""valPlayers"">0 / 40</div>
            </div>
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

    <!-- Connected Players Card -->
    <div class=""card"">
        <div class=""header"" style=""margin-bottom: 10px;"">
            <div class=""subtitle"" style=""text-transform: uppercase; letter-spacing: 0.5px; font-weight: bold; color: #818cf8;"">
                👥 Players Online (<span id=""playersHeaderCount"">0</span>)
            </div>
            <div id=""steamStatusTag"" style=""font-size: 0.75rem; color: #94a3b8; font-weight: bold;"">STEAM: OFFLINE</div>
        </div>
        <div id=""playersListContainer"">
            <div style=""color: #64748b; font-size: 0.85rem; text-align: center; padding: 12px;"" id=""noPlayersText"">No players currently online</div>
        </div>
    </div>

    <!-- Steam Workshop Mod Search & Add Card -->
    <div class=""card"">
        <div class=""subtitle"" style=""margin-bottom: 8px; text-transform: uppercase; letter-spacing: 0.5px; font-weight: bold; color: #818cf8;"">
            🔍 Steam Workshop Mod Lookup
        </div>
        <div style=""display: flex; gap: 8px; margin-bottom: 10px;"">
            <input type=""text"" id=""txtWorkshopQuery"" placeholder=""Enter mod name or ID (e.g. pippi, emberlight)"">
            <button class=""btn-start"" style=""padding: 10px 16px;"" onclick=""searchWorkshopMods()"">Search</button>
        </div>
        <div id=""workshopResults"" style=""max-height: 280px; overflow-y: auto;""></div>
    </div>

    <!-- Active Server Mods Card -->
    <div class=""card"">
        <div class=""header"" style=""margin-bottom: 8px;"">
            <div class=""subtitle"" style=""text-transform: uppercase; letter-spacing: 0.5px; font-weight: bold; color: #818cf8;"">
                📦 Active Server Mods (<span id=""activeModsCountBadge"">0</span>)
            </div>
            <button class=""btn-backup"" style=""padding: 4px 10px; font-size: 0.75rem;"" onclick=""loadServerMods()"">🔄 Refresh</button>
        </div>
        <div id=""activeModsContainer""></div>
    </div>

    <!-- Server Settings Card -->
    <div class=""card"">
        <div class=""subtitle"" style=""margin-bottom: 8px; text-transform: uppercase; letter-spacing: 0.5px; font-weight: bold; color: #818cf8;"">
            ⚙️ Server Identity &amp; Rules
        </div>
        <div style=""display: flex; flex-direction: column; gap: 10px;"">
            <div>
                <label style=""display: block; font-size: 0.75rem; color: #94a3b8; margin-bottom: 4px;"">Server Name</label>
                <input type=""text"" id=""cfgSrvName"" placeholder=""Server Name"">
            </div>
            <div style=""display: grid; grid-template-columns: 1fr 1fr; gap: 8px;"">
                <div>
                    <label style=""display: block; font-size: 0.75rem; color: #94a3b8; margin-bottom: 4px;"">Server Password</label>
                    <input type=""text"" id=""cfgSrvPass"" placeholder=""Password"">
                </div>
                <div>
                    <label style=""display: block; font-size: 0.75rem; color: #94a3b8; margin-bottom: 4px;"">Admin Password</label>
                    <input type=""text"" id=""cfgAdmPass"" placeholder=""Admin Password"">
                </div>
            </div>
            <div style=""display: grid; grid-template-columns: 1fr 1fr; gap: 8px;"">
                <div>
                    <label style=""display: block; font-size: 0.75rem; color: #94a3b8; margin-bottom: 4px;"">Max Players</label>
                    <input type=""number"" id=""cfgMaxPl"" value=""40"">
                </div>
                <div>
                    <label style=""display: block; font-size: 0.75rem; color: #94a3b8; margin-bottom: 4px;"">Max Tick Rate</label>
                    <input type=""number"" id=""cfgMaxTick"" value=""30"">
                </div>
            </div>
            <button class=""btn-start"" style=""background: #10b981; margin-top: 4px;"" onclick=""saveSettings()"">💾 Save Settings to Server</button>
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
        function escapeHtml(str) {
            if (!str) return '';
            return String(str).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/""/g, '&quot;');
        }

        async function fetchStatus() {
            try {
                const res = await fetch('/api/status');
                const data = await res.json();

                document.getElementById('srvName').innerText = data.serverName || 'Conan Exiles Server';
                document.getElementById('valGamePort').innerText = data.gamePort;
                document.getElementById('valRconPort').innerText = data.rconPort;
                document.getElementById('valMods').innerText = data.activeModsCount;
                document.getElementById('valUptime').innerText = data.uptimeString || '00:00:00';

                if (data.appVersion) {
                    document.getElementById('appVerBadge').innerText = 'v' + data.appVersion;
                }

                const maxP = data.steamMaxPlayers || data.maxPlayers || 40;
                const curP = (data.players && data.players.length > 0) ? data.players.length : (data.steamPlayers || 0);
                document.getElementById('valPlayers').innerText = curP + ' / ' + maxP;
                document.getElementById('playersHeaderCount').innerText = curP;

                const steamTag = document.getElementById('steamStatusTag');
                if (data.steamOnline) {
                    steamTag.innerText = 'STEAM: ONLINE (' + (data.steamPing || 0) + 'ms)';
                    steamTag.style.color = '#34d399';
                } else if (data.status === 'RUNNING') {
                    steamTag.innerText = 'STEAM: STARTING UP...';
                    steamTag.style.color = '#fbbf24';
                } else {
                    steamTag.innerText = 'STEAM: OFFLINE';
                    steamTag.style.color = '#94a3b8';
                }

                const badge = document.getElementById('statusBadge');
                badge.innerText = data.status;
                if (data.status === 'RUNNING') badge.className = 'badge badge-running';
                else if (data.status === 'UPDATING') badge.className = 'badge badge-updating';
                else badge.className = 'badge badge-stopped';

                // Render live connected players
                const container = document.getElementById('playersListContainer');
                if (data.players && data.players.length > 0) {
                    container.innerHTML = data.players.map(p => `
                        <div style=""display: flex; justify-content: space-between; align-items: center; background: #0f172a; border: 1px solid #334155; border-radius: 8px; padding: 8px 12px; margin-bottom: 6px;"">
                            <div style=""display: flex; align-items: center; gap: 8px;"">
                                <span style=""display: inline-block; width: 8px; height: 8px; border-radius: 50%; background: #10b981;""></span>
                                <div>
                                    <div style=""font-weight: bold; color: #f8fafc; font-size: 0.9rem;"">${escapeHtml(p.name)}</div>
                                    <div style=""font-size: 0.75rem; color: #94a3b8;"">Connected: ${escapeHtml(p.durationFormatted || '--')} | Score: ${p.score || 0}</div>
                                </div>
                            </div>
                            <div style=""background: #1e293b; color: #34d399; font-size: 0.75rem; font-weight: bold; padding: 3px 8px; border-radius: 4px;"">
                                ${(p.ping && p.ping > 0) ? p.ping + 'ms' : (data.steamPing ? data.steamPing + 'ms' : '--')}
                            </div>
                        </div>
                    `).join('');
                } else if (data.steamPlayers > 0) {
                    container.innerHTML = `
                        <div style=""background: #0f172a; border: 1px solid #334155; border-radius: 8px; padding: 10px; color: #38bdf8; font-size: 0.85rem; text-align: center;"">
                            ${data.steamPlayers} player(s) actively connected to server
                        </div>
                    `;
                } else {
                    container.innerHTML = '<div style=""color: #64748b; font-size: 0.85rem; text-align: center; padding: 12px;"">No players currently online</div>';
                }
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

        async function searchWorkshopMods() {
            const query = document.getElementById('txtWorkshopQuery').value.trim();
            if (!query) return alert('Please enter a mod name or ID');
            const container = document.getElementById('workshopResults');
            container.innerHTML = '<div style=""color:#38bdf8;padding:8px;text-align:center;"">🔍 Searching Steam Workshop for ""' + escapeHtml(query) + '""...</div>';

            try {
                const res = await fetch('/api/workshop/search?query=' + encodeURIComponent(query));
                const results = await res.json();
                if (!Array.isArray(results) || results.length === 0) {
                    container.innerHTML = '<div style=""color:#94a3b8;padding:8px;text-align:center;"">No matching mods found</div>';
                    return;
                }
                container.innerHTML = results.map(m => `
                    <div style=""display:flex;align-items:center;gap:10px;background:#0f172a;border:1px solid #334155;border-radius:8px;padding:8px;margin-bottom:6px;"">
                        <img src=""${m.PreviewUrl || 'app.png'}"" style=""width:48px;height:48px;border-radius:4px;object-fit:cover;background:#1e293b;"" onerror=""this.src='app.png'"">
                        <div style=""flex:1;min-width:0;"">
                            <div style=""font-weight:bold;font-size:0.85rem;color:#f8fafc;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;"">${escapeHtml(m.Title)}</div>
                            <div style=""font-size:0.72rem;color:#94a3b8;"">ID: <strong>${m.Id}</strong></div>
                        </div>
                        <div>
                            ${m.IsInstalled ? '<span style=""color:#34d399;font-size:0.75rem;font-weight:bold;"">✓ Added</span>' :
                            `<button class=""btn-start"" style=""padding:6px 10px;font-size:0.75rem;"" onclick=""addModToServer('${m.Id}','${escapeHtml(m.Title)}')\"">➕ Add</button>`}
                        </div>
                    </div>
                `).join('');
            } catch (e) {
                container.innerHTML = '<div style=""color:#f87171;padding:8px;text-align:center;"">Search failed: ' + e.message + '</div>';
            }
        }

        async function addModToServer(id, title) {
            try {
                const res = await fetch('/api/mods/add', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ modId: id })
                });
                const data = await res.json();
                if (data.success) {
                    alert('Added ' + title + ' to server!');
                    loadServerMods();
                    searchWorkshopMods();
                } else {
                    alert('Add failed: ' + (data.error || 'Unknown'));
                }
            } catch (e) {
                alert('Error: ' + e.message);
            }
        }

        async function removeModFromServer(id) {
            if (!confirm('Remove mod #' + id + ' from server?')) return;
            try {
                const res = await fetch('/api/mods/remove', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ modId: id })
                });
                const data = await res.json();
                if (data.success) {
                    alert('Removed mod #' + id);
                    loadServerMods();
                } else {
                    alert('Remove failed: ' + (data.error || 'Unknown'));
                }
            } catch (e) {
                alert('Error: ' + e.message);
            }
        }

        async function loadServerMods() {
            try {
                const res = await fetch('/api/mods');
                const data = await res.json();
                const container = document.getElementById('activeModsContainer');
                const list = data.details || [];
                document.getElementById('activeModsCountBadge').innerText = list.length;
                if (list.length === 0) {
                    container.innerHTML = '<div style=""color:#64748b;font-size:0.8rem;text-align:center;padding:10px;"">No mods installed on server</div>';
                    return;
                }
                container.innerHTML = list.map((m, idx) => `
                    <div style=""display:flex;align-items:center;gap:8px;background:#0f172a;border:1px solid #334155;border-radius:6px;padding:6px 10px;margin-bottom:6px;"">
                        <span style=""font-weight:bold;color:#64748b;font-size:0.75rem;width:18px;"">${idx + 1}</span>
                        <div style=""flex:1;min-width:0;"">
                            <div style=""font-weight:bold;font-size:0.85rem;color:#f8fafc;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;"">${escapeHtml(m.Title)}</div>
                            <div style=""font-size:0.7rem;color:#94a3b8;"">ID: <strong>${m.Id}</strong></div>
                        </div>
                        <button class=""btn-stop"" style=""padding:4px 8px;font-size:0.75rem;"" onclick=""removeModFromServer('${m.Id}')"">🗑️</button>
                    </div>
                `).join('');
            } catch (e) {}
        }

        async function loadSettings() {
            try {
                const res = await fetch('/api/config');
                const cfg = await res.json();
                document.getElementById('cfgSrvName').value = cfg.serverName || '';
                document.getElementById('cfgSrvPass').value = cfg.serverPassword || '';
                document.getElementById('cfgAdmPass').value = cfg.adminPassword || '';
                document.getElementById('cfgMaxPl').value = cfg.maxPlayers || 40;
                document.getElementById('cfgMaxTick').value = cfg.maxTickRate || 30;
            } catch (e) {}
        }

        async function saveSettings() {
            try {
                const payload = {
                    serverName: document.getElementById('cfgSrvName').value.trim(),
                    serverPassword: document.getElementById('cfgSrvPass').value.trim(),
                    adminPassword: document.getElementById('cfgAdmPass').value.trim(),
                    maxPlayers: parseInt(document.getElementById('cfgMaxPl').value) || 40,
                    maxTickRate: parseInt(document.getElementById('cfgMaxTick').value) || 30
                };
                const res = await fetch('/api/config', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });
                const data = await res.json();
                alert(data.message || 'Settings saved successfully!');
                fetchStatus();
            } catch (e) {
                alert('Save failed: ' + e.message);
            }
        }

        setInterval(fetchStatus, 3000);
        setInterval(fetchLogs, 2000);
        fetchStatus();
        fetchLogs();
        loadServerMods();
        loadSettings();
    </script>
</body>
</html>".Replace("__APP_VER__", ServerEngine.CurrentAppVersion);
        }
    }
}
