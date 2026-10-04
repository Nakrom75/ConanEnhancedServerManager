using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ConanServerManager
{
    public class NetworkAdapterInfo
    {
        public string Name { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public string MacAddress { get; set; } = "";
        public override string ToString() => $"{IpAddress} on {Name}";
    }

    public class ManagerConfig
    {
        public string ServerName { get; set; } = "Antigravity Conan Server";
        public string ServerPassword { get; set; } = "";
        public string AdminPassword { get; set; } = "SuperSecretAdminPassword123!";
        public string RconPassword { get; set; } = "SuperSecretRconPassword123!";
        public int GamePort { get; set; } = 7777;
        public int RawUdpPort { get; set; } = 7778;
        public int QueryPort { get; set; } = 27015;
        public int RconPort { get; set; } = 25575;
        public int WebPagePort { get; set; } = 8088;
        public int MaxPlayers { get; set; } = 40;
        public int MaxTickRate { get; set; } = 30;
        public string Region { get; set; } = "0 - Europe";

        public string SelectedNetworkInterface { get; set; } = "";
        public string ExternalIp { get; set; } = "";
        public string MacAddress { get; set; } = "";

        // Custom Folder Override
        public string CustomServerDir { get; set; } = "";

        // Branch & Deployment
        public string AutoUpdateRestartMode { get; set; } = "Auto-Update On Restart";
        public bool ValidateFiles { get; set; } = false;
        public bool StartServerIfNotRunning { get; set; } = true;
        public bool AutoStartOnAppLaunch { get; set; } = false;

        // Multihome
        public bool UseMultihome { get; set; } = false;
        public string MultihomeIp { get; set; } = "";

        // RCon & Anti-Cheat
        public bool RconEnabled { get; set; } = true;
        public int Karma { get; set; } = 60;
        public bool EnableBattlEye { get; set; } = false;
        public bool EnableVAC { get; set; } = false;

        // Startup Map & Extra Params
        public string StartupMap { get; set; } = "Exiled Lands|/Game/Maps/ConanSandbox/ConanSandbox";
        public string GameServerExtraParams { get; set; } = "";
        public string SteamCmdExtraParams { get; set; } = "";

        // Automatic Restart & Warnings
        public bool EnableDailyRestart { get; set; } = false;
        public string DailyRestartTime { get; set; } = "06:00:00";
        public string MinimumUptime { get; set; } = "02:00:00";
        public int RestartsPerDay { get; set; } = 1;
        public bool ZombieCheckEnabled { get; set; } = true;
        public string ZombieCheckInterval { get; set; } = "00:05:00";

        public string FirstWarningTime { get; set; } = "00:10";
        public string FirstWarningMsg { get; set; } = "First Rcon message before shutdown";
        public string SecondWarningTime { get; set; } = "00:05";
        public string SecondWarningMsg { get; set; } = "Second Rcon message before shutdown";
        public string ThirdWarningTime { get; set; } = "00:02";
        public string ThirdWarningMsg { get; set; } = "Third Rcon message before shutdown";
        public bool FastRestartZeroPlayers { get; set; } = true;

        public string ReadyDiscordMsg { get; set; } = "Server Ready message (Discord only)";
        public string RestartedDiscordMsg { get; set; } = "Manual Restart message (Discord only)";
        public string ShutdownDiscordMsg { get; set; } = "Manual Shutdown message (Discord only)";

        // Discord Webhooks
        public bool DiscordEnabled { get; set; } = false;
        public string DiscordWebhookUrl { get; set; } = "";
        public bool DiscordIncludeTime { get; set; } = true;
        public string DiscordBotName { get; set; } = "ConanServerBot";

        // Web Page
        public bool WebPageEnabled { get; set; } = true;
        public string WebPagePassword { get; set; } = "";

        // Performance / Affinity
        public string PriorityClass { get; set; } = "Unmanaged";
        public bool UseAllAvailableCores { get; set; } = true;
        public ulong CpuAffinityMask { get; set; } = 0xFFFFFFFFFFFFFFFF;

        // Backup
        public bool OnShutdownBackup { get; set; } = true;
        public string BackupScriptMode { get; set; } = "Don't Run Scripts";
        public int BackupLimitDays { get; set; } = 7;

        // Application Auto-Update & GitHub Releases
        public string GitHubRepo { get; set; } = "Nakrom75/ConanEnhancedServerManager";
        public bool AutoCheckAppUpdates { get; set; } = true;
        public bool AutoInstallAppUpdates { get; set; } = false;
        public string LastCheckedAppVersion { get; set; } = "";

        public List<string> Mods { get; set; } = new List<string>
        {
            "3722388367", "3803465771", "3723073788", "3722270581", "3723975720",
            "3755091710", "3722359128", "3721422676", "3721926604", "3720663670",
            "3719513784", "3720108366", "3720921242", "3721912252", "3721568940",
            "2864811796", "3789088705", "3786621691"
        };
    }

    public class AppUpdateInfo
    {
        public string TagName { get; set; } = "";
        public string VersionString { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public long AssetSize { get; set; }
        public bool IsNewer { get; set; }
        public bool UpdateAvailable => IsNewer;
        public string CurrentVersion => ServerEngine.CurrentAppVersion;
        public string LatestVersion => !string.IsNullOrEmpty(TagName) ? TagName : $"v{VersionString}";
    }

    public class DownloadProgressInfo
    {
        public string ItemName { get; set; } = "";
        public int CurrentIndex { get; set; }
        public int TotalCount { get; set; }
        public double Percent { get; set; }
        public long DownloadedBytes { get; set; }
        public long TotalBytes { get; set; }
        public double SpeedMBs { get; set; }
        public TimeSpan Eta { get; set; }
        public string StatusText { get; set; } = "";
        public bool IsActive { get; set; }
    }

    public class PortStatusReport
    {
        public bool GamePortBound { get; set; }
        public bool RawPortBound { get; set; }
        public bool QueryPortBound { get; set; }
        public bool RconPortBound { get; set; }
        public bool WebPortBound { get; set; }
    }

    public class ServerEngine
    {
        public static string BaseDir => AppDomain.CurrentDomain.BaseDirectory;

        public static string AppWorkingDir
        {
            get
            {
                string baseDir = BaseDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (Path.GetFileName(baseDir).Equals("dist", StringComparison.OrdinalIgnoreCase))
                {
                    return Directory.GetParent(baseDir)?.FullName ?? baseDir;
                }
                return baseDir;
            }
        }

        public ManagerConfig Config { get; private set; } = new ManagerConfig();
        public Process? ServerProcess { get; private set; }
        public Process? CurrentSteamCmdProcess { get; private set; }
        public string ServerStatus { get; private set; } = "STOPPED";
        public WebServer WebApi { get; private set; }
        public DownloadProgressInfo? CurrentDownloadProgress { get; private set; }

        public event Action<string>? OnLog;
        public event Action<string>? OnSteamCmdLog;
        public event Action<string>? OnErrorLog;
        public event Action<string>? OnStatusChanged;
        public event Action<DownloadProgressInfo>? OnDownloadProgress;

        private static readonly System.Text.RegularExpressions.Regex SteamCmdProgressRegex =
            new System.Text.RegularExpressions.Regex(@"progress:\s*([\d\.]+)\s*\(\s*(\d+)\s*/\s*(\d+)\s*\)",
                System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        private DateTime _lastProgressTime = DateTime.MinValue;
        private long _lastProgressBytes = 0;
        private double _lastSpeedMBs = 0;
        private TimeSpan _lastEta = TimeSpan.Zero;
        private string _currentDownloadName = "";
        private int _currentDownloadIndex = 0;
        private int _totalDownloadCount = 0;
        private int _rconConsecutiveFailures = 0;
        private System.Threading.Timer? _watchdogTimer;
        private System.Threading.Timer? _appUpdateTimer;

        public const string WorkshopAppId = "440900";
        public const string ServerAppId = "443030";
        public const string CurrentAppVersion = "1.0.2";

        public AppUpdateInfo? LatestAppUpdate { get; private set; }
        public event Action<AppUpdateInfo>? OnAppUpdateDiscovered;

        public ServerEngine()
        {
            LoadConfig();
            AutoDetectAndImportIniSettings();
            RefreshNetworkAdapters();

            DetectAndAdoptRunningServerProcess();

            WebApi = new WebServer(this);
            SafeFireAndForget(async () => await WebApi.StartAsync(Config.WebPagePort), "Start WebApi");
            SafeFireAndForget(async () => await DetectExternalIpAsync(), "Detect External IP");
            StartWatchdogTimer();
            StartAppUpdateTimer();
        }

        public bool DetectAndAdoptRunningServerProcess()
        {
            try
            {
                var procs = Process.GetProcessesByName("ConanSandboxServer-Win64-Shipping")
                    .Concat(Process.GetProcessesByName("ConanSandboxServer"))
                    .Where(p => !p.HasExited)
                    .ToArray();

                if (procs.Length > 0)
                {
                    if (procs.Length > 1)
                    {
                        Log($"[Process Monitor] WARNING: {procs.Length} Conan Dedicated Server instances detected running simultaneously!");
                    }

                    var primaryProc = procs[0];
                    ServerProcess = primaryProc;
                    SetStatus("RUNNING");
                    Log($"[Process Monitor] Attached to running Conan Dedicated Server process (PID {primaryProc.Id}). Status: RUNNING.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log($"[Process Monitor] Note checking server process: {ex.Message}");
            }
            return false;
        }

        private void StartWatchdogTimer()
        {
            _watchdogTimer = new System.Threading.Timer(async _ =>
            {
                try
                {
                    await CheckWatchdogAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Watchdog Exception]: {ex.Message}");
                }
            }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        private void StartAppUpdateTimer()
        {
            // Initial check 10 seconds after launch, then recurring every 4 hours
            _appUpdateTimer = new System.Threading.Timer(async _ =>
            {
                if (Config.AutoCheckAppUpdates)
                {
                    try
                    {
                        await CheckForAppUpdateAsync();
                    }
                    catch (Exception ex)
                    {
                        Log($"[Auto-Update Timer Error]: {ex.Message}");
                    }
                }
            }, null, TimeSpan.FromSeconds(10), TimeSpan.FromHours(4));
        }

        private async Task CheckWatchdogAsync()
        {
            if (!Config.ZombieCheckEnabled) return;

            if (ServerStatus == "RUNNING")
            {
                // 1. Process Crash Check
                if (ServerProcess != null)
                {
                    bool hasExited = false;
                    try
                    {
                        hasExited = ServerProcess.HasExited;
                    }
                    catch
                    {
                        hasExited = true;
                    }

                    if (hasExited)
                    {
                        Log("[Watchdog] WARNING: Server process terminated unexpectedly! Executing auto-restart...");
                        SetStatus("STOPPED");
                        ServerProcess = null;
                        _ = Task.Run(async () => await RunFullUpdateAndStartAsync());
                        return;
                    }
                }
                else
                {
                    if (DetectAndAdoptRunningServerProcess())
                    {
                        return;
                    }

                    Log("[Watchdog] WARNING: Server process reference missing while status was RUNNING. Auto-restarting...");
                    SetStatus("STOPPED");
                    _ = Task.Run(async () => await RunFullUpdateAndStartAsync());
                    return;
                }

                // 2. Zombie / Deadlock Check via RCON ping
                if (Config.RconEnabled)
                {
                    bool rconOk = false;
                    try
                    {
                        using var rcon = new ValveRconClient("127.0.0.1", Config.RconPort, Config.RconPassword);
                        await rcon.ConnectAsync(3000);
                        string resp = await rcon.ExecuteAsync("help");
                        if (!string.IsNullOrWhiteSpace(resp))
                        {
                            rconOk = true;
                        }
                    }
                    catch
                    {
                        rconOk = false;
                    }

                    if (rconOk)
                    {
                        _rconConsecutiveFailures = 0;
                    }
                    else
                    {
                        _rconConsecutiveFailures++;
                        Log($"[Watchdog] Warning: RCON heartbeat probe timed out / failed ({_rconConsecutiveFailures}/3).");

                        if (_rconConsecutiveFailures >= 3)
                        {
                            Log("[Watchdog] CRITICAL: Server process failed 3 consecutive RCON heartbeats (Zombie/Deadlock detected). Executing emergency restart...");
                            _rconConsecutiveFailures = 0;
                            await StopServerAsync();
                            _ = Task.Run(async () => await RunFullUpdateAndStartAsync());
                        }
                    }
                }
            }
        }

        private static void SafeFireAndForget(Func<Task> action, string taskName)
        {
            Task.Run(async () =>
            {
                try
                {
                    await action();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Background Task Exception - {taskName}]: {ex.Message}");
                }
            });
        }

        public List<NetworkAdapterInfo> GetNetworkAdapters()
        {
            var list = new List<NetworkAdapterInfo>();
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    var props = nic.GetIPProperties();
                    foreach (var ip in props.UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            list.Add(new NetworkAdapterInfo
                            {
                                Name = nic.Description,
                                IpAddress = ip.Address.ToString(),
                                MacAddress = nic.GetPhysicalAddress().ToString()
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Network adapter enumeration note: {ex.Message}");
            }
            return list;
        }

        public void RefreshNetworkAdapters()
        {
            var adapters = GetNetworkAdapters();
            if (adapters.Count > 0 && string.IsNullOrEmpty(Config.SelectedNetworkInterface))
            {
                Config.SelectedNetworkInterface = adapters[0].ToString();
                Config.MacAddress = adapters[0].MacAddress;
            }
        }

        public async Task DetectExternalIpAsync()
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                string ip = await http.GetStringAsync("https://api.ipify.org");
                Config.ExternalIp = ip.Trim();
                Log($"External IP Address detected: {Config.ExternalIp}");
            }
            catch
            {
                Config.ExternalIp = "127.0.0.1";
            }
        }

        public async Task<PortStatusReport> CheckAllPortsAsync()
        {
            var report = new PortStatusReport();
            await Task.Run(() =>
            {
                report.GamePortBound = IsUdpPortListening(Config.GamePort) || TestTcpPortDirect(Config.GamePort);
                report.RawPortBound = IsUdpPortListening(Config.RawUdpPort) || TestTcpPortDirect(Config.RawUdpPort);
                report.QueryPortBound = IsUdpPortListening(Config.QueryPort) || TestTcpPortDirect(Config.QueryPort);
                report.RconPortBound = IsTcpPortListening(Config.RconPort) || TestTcpPortDirect(Config.RconPort);
                report.WebPortBound = IsTcpPortListening(Config.WebPagePort) || TestTcpPortDirect(Config.WebPagePort);
            });
            return report;
        }

        public async Task<string> TestPortAccessibilityAsync()
        {
            Log($"Testing port accessibility for Game ({Config.GamePort}), Raw ({Config.RawUdpPort}), Query ({Config.QueryPort}), RCON ({Config.RconPort}), and Web ({Config.WebPagePort})...");
            var report = await CheckAllPortsAsync();
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"Web API Port {Config.WebPagePort} (TCP): {(report.WebPortBound ? "✓ ACCESSIBLE" : "✗ CLOSED / TIMED OUT")}");
            sb.AppendLine($"RCON Port {Config.RconPort} (TCP): {(report.RconPortBound ? "✓ ACCESSIBLE" : "✗ CLOSED / NOT LISTENING")}");
            sb.AppendLine($"Game Port {Config.GamePort} (UDP/TCP): {(report.GamePortBound ? "✓ BOUND & ACCESSIBLE" : "ℹ NOT BOUND (Server Stopped)")}");
            sb.AppendLine($"Raw UDP Port {Config.RawUdpPort}: {(report.RawPortBound ? "✓ BOUND & ACCESSIBLE" : "ℹ NOT BOUND (Server Stopped)")}");
            sb.AppendLine($"Query Port {Config.QueryPort} (UDP): {(report.QueryPortBound ? "✓ BOUND & ACCESSIBLE" : "ℹ NOT BOUND (Server Stopped)")}");

            string result = sb.ToString();
            Log($"Port Accessibility Diagnostic Result:\n{result}");
            return result;
        }

        private bool TestTcpPortDirect(int port)
        {
            try
            {
                using var client = new TcpClient();
                var result = client.BeginConnect("127.0.0.1", port, null, null);
                bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));
                if (success && client.Connected)
                {
                    client.EndConnect(result);
                    return true;
                }
            }
            catch { }
            return false;
        }

        public bool IsUdpPortListening(int port)
        {
            try
            {
                var properties = IPGlobalProperties.GetIPGlobalProperties();
                var udpListeners = properties.GetActiveUdpListeners();
                return udpListeners.Any(endpoint => endpoint.Port == port);
            }
            catch
            {
                return false;
            }
        }

        public bool IsTcpPortListening(int port)
        {
            try
            {
                var properties = IPGlobalProperties.GetIPGlobalProperties();
                var tcpListeners = properties.GetActiveTcpListeners();
                return tcpListeners.Any(endpoint => endpoint.Port == port);
            }
            catch
            {
                return false;
            }
        }

        public async Task SendDiscordNotificationAsync(string message)
        {
            if (!Config.DiscordEnabled || string.IsNullOrWhiteSpace(Config.DiscordWebhookUrl)) return;

            try
            {
                using var http = new HttpClient();
                string timeStr = Config.DiscordIncludeTime ? $"[{DateTime.Now:HH:mm:ss}] " : "";
                var payload = new
                {
                    username = Config.DiscordBotName,
                    content = $"{timeStr}{message}"
                };

                string json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await http.PostAsync(Config.DiscordWebhookUrl, content);
                Log($"Discord Notification sent: {message}");
            }
            catch (Exception ex)
            {
                Log($"Discord Webhook warning: {ex.Message}");
            }
        }

        public string ServerRootDir
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Config.CustomServerDir) && Directory.Exists(Config.CustomServerDir))
                {
                    return Config.CustomServerDir;
                }
                return Path.Combine(AppWorkingDir, "ConanExilesDedicatedServer");
            }
        }

        public string SteamCmdExe => FindSteamCmdExe();

        private string FindSteamCmdExe()
        {
            string[] candidates = new[]
            {
                Path.Combine(AppWorkingDir, "SteamCMD.exe"),
                Path.Combine(AppWorkingDir, "DedicatedServerLauncher", "SteamCMD.exe"),
                Path.Combine(ServerRootDir, "SteamCMD.exe"),
                Path.Combine(BaseDir, "SteamCMD.exe")
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate)) return candidate;
            }

            return Path.Combine(AppWorkingDir, "SteamCMD.exe");
        }

        public string ConfigDir
        {
            get
            {
                string path1 = Path.Combine(ServerRootDir, "ConanSandbox", "Saved", "Config", "WindowsServer");
                if (Directory.Exists(path1)) return path1;

                string path2 = Path.Combine(ServerRootDir, "Saved", "Config", "WindowsServer");
                if (Directory.Exists(path2)) return path2;

                return path1;
            }
        }

        public string ServerSettingsIni => Path.Combine(ConfigDir, "ServerSettings.ini");
        public string EngineIni => Path.Combine(ConfigDir, "Engine.ini");
        public string GameIni => Path.Combine(ConfigDir, "Game.ini");
        public string ModsDir => Path.Combine(ServerRootDir, "ConanSandbox", "Mods");
        public string ModlistTxt => Path.Combine(ModsDir, "modlist.txt");
        public string GameDbPath => Path.Combine(ServerRootDir, "ConanSandbox", "Saved", "game.db");
        public string BackupDir => Path.Combine(AppWorkingDir, "Backups");
        public string ConfigJsonPath => Path.Combine(AppWorkingDir, "manager_config.json");

        public string ExecutablePath
        {
            get
            {
                string path1 = Path.Combine(ServerRootDir, "ConanSandbox", "Binaries", "Win64", "ConanSandboxServer-Win64-Shipping.exe");
                if (File.Exists(path1)) return path1;

                string path2 = Path.Combine(ServerRootDir, "ConanSandboxServer.exe");
                if (File.Exists(path2)) return path2;

                return path1;
            }
        }

        public void LoadConfig()
        {
            if (File.Exists(ConfigJsonPath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigJsonPath);
                    var cfg = JsonSerializer.Deserialize<ManagerConfig>(json);
                    if (cfg != null) Config = cfg;
                }
                catch (Exception ex)
                {
                    Log($"Error loading manager_config.json: {ex.Message}");
                }
            }
        }

        public void AutoDetectAndImportIniSettings()
        {
            if (!File.Exists(ServerSettingsIni)) return;

            try
            {
                var lines = File.ReadAllLines(ServerSettingsIni);
                string currentSection = "";

                foreach (var rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        currentSection = line.Substring(1, line.Length - 2).Trim();
                        continue;
                    }

                    if (currentSection.Equals("ServerSettings", StringComparison.OrdinalIgnoreCase) && line.Contains("="))
                    {
                        var parts = line.Split(new[] { '=' }, 2);
                        string key = parts[0].Trim();
                        string val = parts[1].Trim();

                        if (key.Equals("ServerName", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(val)) Config.ServerName = val;
                        else if (key.Equals("ServerPassword", StringComparison.OrdinalIgnoreCase)) Config.ServerPassword = val;
                        else if (key.Equals("AdminPassword", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(val)) Config.AdminPassword = val;
                        else if (key.Equals("RconPassword", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(val)) Config.RconPassword = val;
                        else if (key.Equals("RconPort", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out int rPort)) Config.RconPort = rPort;
                        else if (key.Equals("DedicatedServerLauncherModList", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(val))
                        {
                            var modIds = val.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                            .Select(m => m.Trim())
                                            .Where(m => !string.IsNullOrEmpty(m))
                                            .ToList();
                            if (modIds.Count > 0) Config.Mods = modIds;
                        }
                    }
                }

                if (File.Exists(EngineIni))
                {
                    var eLines = File.ReadAllLines(EngineIni);
                    string eSection = "";
                    foreach (var rawLine in eLines)
                    {
                        string line = rawLine.Trim();
                        if (line.StartsWith("[") && line.EndsWith("]"))
                        {
                            eSection = line.Substring(1, line.Length - 2).Trim();
                            continue;
                        }

                        if (eSection.Equals("URL", StringComparison.OrdinalIgnoreCase) && line.Contains("="))
                        {
                            var parts = line.Split(new[] { '=' }, 2);
                            if (parts[0].Trim().Equals("Port", StringComparison.OrdinalIgnoreCase) && int.TryParse(parts[1].Trim(), out int gPort))
                            {
                                Config.GamePort = gPort;
                            }
                        }
                    }
                }

                Log("Loaded existing server configuration from ServerSettings.ini and Engine.ini.");
            }
            catch (Exception ex)
            {
                Log($"INI import warning: {ex.Message}");
            }
        }

        public void SaveConfig()
        {
            string json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigJsonPath, json);
            SyncIniSettings();
        }

        public void Log(string message)
        {
            string formatted = $"[{DateTime.Now:HH:mm:ss}] {message}";
            OnLog?.Invoke(formatted);
            if (message.Contains("ERROR") || message.Contains("FAILED") || message.Contains("Warning"))
            {
                OnErrorLog?.Invoke(formatted);
            }
        }

        public void LogSteamCmd(string message)
        {
            string formatted = $"[{DateTime.Now:HH:mm:ss}] [SteamCMD] {message}";
            OnSteamCmdLog?.Invoke(formatted);
            OnLog?.Invoke(formatted);
        }

        public void SetStatus(string newStatus)
        {
            ServerStatus = newStatus;
            OnStatusChanged?.Invoke(newStatus);
        }

        public void SyncIniSettings()
        {
            Directory.CreateDirectory(ConfigDir);

            UpdateIniKey(ServerSettingsIni, "ServerSettings", "ServerName", Config.ServerName);
            UpdateIniKey(ServerSettingsIni, "ServerSettings", "ServerPassword", Config.ServerPassword);
            UpdateIniKey(ServerSettingsIni, "ServerSettings", "AdminPassword", Config.AdminPassword);
            UpdateIniKey(ServerSettingsIni, "ServerSettings", "RconEnabled", Config.RconEnabled ? "True" : "False");
            UpdateIniKey(ServerSettingsIni, "ServerSettings", "RconPassword", Config.RconPassword);
            UpdateIniKey(ServerSettingsIni, "ServerSettings", "RconPort", Config.RconPort.ToString());
            UpdateIniKey(ServerSettingsIni, "ServerSettings", "DedicatedServerLauncherModList", string.Join(",", Config.Mods));

            UpdateIniKey(EngineIni, "URL", "Port", Config.GamePort.ToString());
        }

        private void UpdateIniKey(string filePath, string section, string key, string value)
        {
            var lines = File.Exists(filePath) ? File.ReadAllLines(filePath).ToList() : new List<string>();
            int sectionIdx = lines.FindIndex(l => l.Trim().Equals($"[{section}]", StringComparison.OrdinalIgnoreCase));

            if (sectionIdx == -1)
            {
                lines.Add($"[{section}]");
                lines.Add($"{key}={value}");
            }
            else
            {
                int keyIdx = -1;
                for (int i = sectionIdx + 1; i < lines.Count; i++)
                {
                    if (lines[i].Trim().StartsWith("[")) break;
                    if (lines[i].Trim().StartsWith($"{key}=", StringComparison.OrdinalIgnoreCase))
                    {
                        keyIdx = i;
                        break;
                    }
                }

                if (keyIdx != -1)
                {
                    lines[keyIdx] = $"{key}={value}";
                }
                else
                {
                    lines.Insert(sectionIdx + 1, $"{key}={value}");
                }
            }

            File.WriteAllLines(filePath, lines);
        }

        public string GetIniText(string filePath)
        {
            return File.Exists(filePath) ? File.ReadAllText(filePath) : "";
        }

        public void SaveIniText(string filePath, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, content);
            Log($"Updated INI file: {Path.GetFileName(filePath)}");
        }

        private async Task<bool> EnsureSteamCmdDownloadedAsync(string targetExePath)
        {
            try
            {
                string targetDir = Path.GetDirectoryName(targetExePath) ?? AppWorkingDir;
                Directory.CreateDirectory(targetDir);
                string zipPath = Path.Combine(targetDir, "steamcmd.zip");

                Log("Downloading SteamCMD package from Valve CDN...");
                using (var client = new HttpClient())
                {
                    byte[] data = await client.GetByteArrayAsync("https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip");
                    await File.WriteAllBytesAsync(zipPath, data);
                }

                Log("Extracting steamcmd.zip...");
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, targetDir, overwriteFiles: true);
                if (File.Exists(zipPath)) File.Delete(zipPath);

                Log($"SteamCMD downloaded and extracted successfully to {targetExePath}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Failed to download/extract SteamCMD automatically: {ex.Message}");
                return false;
            }
        }

        public async Task RunFullUpdateAndStartAsync()
        {
            if (DetectAndAdoptRunningServerProcess() || ServerStatus == "RUNNING" || ServerStatus == "UPDATING")
            {
                Log("[Server Control] Dedicated Server is already running or updating. Duplicate launch blocked.");
                return;
            }

            SetStatus("UPDATING");
            Log("=== STARTING CONAN DEDICATED SERVER UPDATE & DEPLOYMENT ===");

            SyncIniSettings();

            await Task.Run(() =>
            {
                if (Config.AutoUpdateRestartMode != "Don't Auto-Update on Restart")
                {
                    DownloadServerBase();

                    for (int i = 0; i < Config.Mods.Count; i++)
                    {
                        DownloadModResilient(Config.Mods[i], i);
                    }
                }

                GenerateModlistFile();
            });

            CurrentDownloadProgress = new DownloadProgressInfo { IsActive = false };
            OnDownloadProgress?.Invoke(CurrentDownloadProgress);

            if (!File.Exists(ExecutablePath))
            {
                Log($"ERROR: Executable not found at {ExecutablePath}");
                SetStatus("STOPPED");
                return;
            }

            Log("Launching Conan Sandbox Dedicated Server executable...");

            string mapPath = Config.StartupMap.Contains("|") ? Config.StartupMap.Split('|')[1] : Config.StartupMap;
            string extraArgs = string.IsNullOrWhiteSpace(Config.GameServerExtraParams) ? "" : $" {Config.GameServerExtraParams.Trim()}";
            string multihomeArg = (Config.UseMultihome && !string.IsNullOrWhiteSpace(Config.MultihomeIp)) ? $" -MULTIHOME={Config.MultihomeIp.Trim()}" : "";
            string adminPassArg = !string.IsNullOrWhiteSpace(Config.AdminPassword) ? $" -AdminPassword=\"{Config.AdminPassword.Trim()}\"" : "";

            string arguments = $"{mapPath} -log -Port={Config.GamePort} -QueryPort={Config.QueryPort} -RconPort={Config.RconPort} -RconPassword=\"{Config.RconPassword}\"{adminPassArg} -MaxPlayers={Config.MaxPlayers} -useallavailablecores -nosteamclient -game -server{multihomeArg}{extraArgs}";

            var psi = new ProcessStartInfo
            {
                FileName = ExecutablePath,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(ExecutablePath),
                UseShellExecute = false
            };

            if (DetectAndAdoptRunningServerProcess())
            {
                Log($"[Server Control] Existing server instance detected (PID {ServerProcess?.Id}). Aborting duplicate launch.");
                return;
            }

            try
            {
                ServerProcess = Process.Start(psi);
                if (ServerProcess != null)
                {
                    try
                    {
                        if (Config.PriorityClass == "Normal Priority") ServerProcess.PriorityClass = ProcessPriorityClass.Normal;
                        else if (Config.PriorityClass == "Above Normal Priority") ServerProcess.PriorityClass = ProcessPriorityClass.AboveNormal;
                        else if (Config.PriorityClass == "High Priority") ServerProcess.PriorityClass = ProcessPriorityClass.High;

                        if (Config.CpuAffinityMask != 0 && Config.CpuAffinityMask != 0xFFFFFFFFFFFFFFFF)
                        {
                            ServerProcess.ProcessorAffinity = (IntPtr)Config.CpuAffinityMask;
                        }
                    }
                    catch (Exception exPriority)
                    {
                        Log($"Priority/Affinity assignment note: {exPriority.Message}");
                    }

                    SetStatus("RUNNING");
                    Log($"Server launched successfully! Process PID: {ServerProcess.Id}");
                    Log($"Launch Command: {ExecutablePath} {arguments}");

                    _ = Task.Run(async () => await SendDiscordNotificationAsync(Config.ReadyDiscordMsg));
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to launch server process: {ex.Message}");
                SetStatus("STOPPED");
            }
        }

        private void DownloadServerBase()
        {
            _currentDownloadName = "Conan Exiles Server Base (AppID 443030)";
            _currentDownloadIndex = 1;
            _totalDownloadCount = Config.Mods.Count + 1;
            _lastProgressTime = DateTime.MinValue;
            _lastProgressBytes = 0;

            Log($"[1/{_totalDownloadCount}] Checking/Updating Conan Exiles Server Base (AppID 443030)...");
            RunSteamCmd($"+force_install_dir \"{ServerRootDir}\" +login anonymous +app_update {ServerAppId} validate +logoff +quit");
        }

        private void DownloadModResilient(string modId, int modIndex, int maxRetries = 5)
        {
            _currentDownloadName = $"Workshop Mod {modId}";
            _currentDownloadIndex = modIndex + 2;
            _totalDownloadCount = Config.Mods.Count + 1;
            _lastProgressTime = DateTime.MinValue;
            _lastProgressBytes = 0;

            Log($"[{_currentDownloadIndex}/{_totalDownloadCount}] Downloading/Validating Workshop Mod {modId}...");
            string args = $"+force_install_dir \"{ServerRootDir}\" +login anonymous +workshop_download_item {WorkshopAppId} {modId} validate +logoff +quit";

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                Log($"Mod {modId} - Attempt {attempt}/{maxRetries}...");
                bool success = RunSteamCmd(args);
                if (success)
                {
                    Log($"Mod {modId} download/verification completed successfully.");
                    return;
                }

                Log($"Mod {modId} attempt {attempt} encountered issue/timeout. Retrying in 5s (SteamCMD resumes partial chunks)...");
                Task.Delay(5000).Wait();
            }

            Log($"WARNING: Mod {modId} failed to complete after {maxRetries} attempts.");
        }

        private bool RunSteamCmd(string arguments)
        {
            string steamCmdPath = SteamCmdExe;
            if (!File.Exists(steamCmdPath))
            {
                Log($"SteamCMD.exe missing at {steamCmdPath}. Attempting automatic download from Valve servers...");
                bool downloaded = Task.Run(async () => await EnsureSteamCmdDownloadedAsync(steamCmdPath)).GetAwaiter().GetResult();
                if (!downloaded || !File.Exists(steamCmdPath))
                {
                    Log($"SteamCMD.exe not found at {steamCmdPath}. Please ensure SteamCMD is present.");
                    return false;
                }
            }

            var psi = new ProcessStartInfo
            {
                FileName = steamCmdPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true
            };

            bool isSuccess = false;
            using var proc = new Process { StartInfo = psi };
            CurrentSteamCmdProcess = proc;

            try
            {
                proc.Start();

                var readerTask = Task.Run(() =>
                {
                    var sb = new StringBuilder();
                    using var reader = proc.StandardOutput;
                    int ch;
                    while ((ch = reader.Read()) != -1)
                    {
                        char c = (char)ch;
                        if (c == '\r' || c == '\n')
                        {
                            if (sb.Length > 0)
                            {
                                string line = sb.ToString().Trim();
                                sb.Clear();
                                ProcessSteamCmdLine(line, ref isSuccess);
                            }
                        }
                        else
                        {
                            sb.Append(c);
                        }
                    }
                    if (sb.Length > 0)
                    {
                        string line = sb.ToString().Trim();
                        ProcessSteamCmdLine(line, ref isSuccess);
                    }
                });

                var errorTask = Task.Run(() =>
                {
                    using var errReader = proc.StandardError;
                    string? errLine;
                    while ((errLine = errReader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(errLine))
                        {
                            LogSteamCmd($"[STDERR] {errLine.Trim()}");
                        }
                    }
                });

                proc.WaitForExit();
                Task.WaitAll(readerTask, errorTask);
                return proc.ExitCode == 0 || isSuccess;
            }
            catch (Exception ex)
            {
                Log($"SteamCMD execution note: {ex.Message}");
                return false;
            }
            finally
            {
                CurrentSteamCmdProcess = null;
            }
        }

        private void ProcessSteamCmdLine(string txt, ref bool isSuccess)
        {
            if (string.IsNullOrWhiteSpace(txt)) return;
            if (txt.Contains("Success.")) isSuccess = true;
            LogSteamCmd(txt);

            var match = SteamCmdProgressRegex.Match(txt);
            if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double percent))
            {
                long.TryParse(match.Groups[2].Value, out long currentBytes);
                long.TryParse(match.Groups[3].Value, out long totalBytes);

                DateTime now = DateTime.Now;
                double elapsedSec = (now - _lastProgressTime).TotalSeconds;
                double speedMBs = _lastSpeedMBs;
                TimeSpan eta = _lastEta;

                if (elapsedSec >= 0.5 && currentBytes > _lastProgressBytes)
                {
                    double bytesPerSec = (currentBytes - _lastProgressBytes) / elapsedSec;
                    speedMBs = bytesPerSec / (1024.0 * 1024.0);
                    long remainingBytes = Math.Max(0, totalBytes - currentBytes);
                    if (bytesPerSec > 0)
                    {
                        eta = TimeSpan.FromSeconds(remainingBytes / bytesPerSec);
                    }
                    _lastProgressBytes = currentBytes;
                    _lastProgressTime = now;
                    _lastSpeedMBs = speedMBs;
                    _lastEta = eta;
                }

                CurrentDownloadProgress = new DownloadProgressInfo
                {
                    ItemName = _currentDownloadName,
                    CurrentIndex = _currentDownloadIndex,
                    TotalCount = _totalDownloadCount,
                    Percent = percent,
                    DownloadedBytes = currentBytes,
                    TotalBytes = totalBytes,
                    SpeedMBs = speedMBs,
                    Eta = eta,
                    StatusText = $"[{_currentDownloadIndex}/{_totalDownloadCount}] {_currentDownloadName} - {percent:F1}%",
                    IsActive = true
                };

                OnDownloadProgress?.Invoke(CurrentDownloadProgress);
            }
        }

        private void GenerateModlistFile()
        {
            Directory.CreateDirectory(ModsDir);
            string workshopContentDir = Path.Combine(ServerRootDir, "steamapps", "workshop", "content", WorkshopAppId);
            var pakPaths = new List<string>();

            foreach (var modId in Config.Mods)
            {
                string modFolder = Path.Combine(workshopContentDir, modId);
                if (Directory.Exists(modFolder))
                {
                    var paks = Directory.GetFiles(modFolder, "*.pak");
                    if (paks.Length > 0)
                    {
                        pakPaths.Add(Path.GetFullPath(paks[0]));
                    }
                    else
                    {
                        Log($"Warning: No .pak file found in {modFolder}");
                    }
                }
                else
                {
                    Log($"Warning: Mod folder missing for {modId}");
                }
            }

            File.WriteAllLines(ModlistTxt, pakPaths);
            Log($"Generated modlist.txt with {pakPaths.Count} active mods.");
        }

        public async Task StopServerAsync()
        {
            if (ServerStatus == "UPDATING")
            {
                Log("Aborting active update & download sequence...");
                KillAllSteamCmdProcesses();
                CurrentDownloadProgress = new DownloadProgressInfo { IsActive = false };
                OnDownloadProgress?.Invoke(CurrentDownloadProgress);
                SetStatus("STOPPED");
                return;
            }

            SetStatus("STOPPING");
            Log("Initiating graceful server shutdown sequence...");

            // Step 1: Attempt RCON shutdown
            try
            {
                using var rcon = new ValveRconClient("127.0.0.1", Config.RconPort, Config.RconPassword);
                await rcon.ConnectAsync(3000);
                await rcon.ExecuteAsync("broadcast Server shutting down immediately!");
                await rcon.ExecuteAsync("save");
                await rcon.ExecuteAsync("exit");
                Log("RCON exit command sent successfully. Waiting up to 10s for process exit...");
            }
            catch (Exception ex)
            {
                Log($"RCON shutdown note ({ex.Message}). Proceeding with process termination...");
            }

            // Step 2: Wait for tracked process exit with 10s timeout
            if (ServerProcess != null)
            {
                try
                {
                    if (!ServerProcess.HasExited)
                    {
                        bool exited = ServerProcess.WaitForExit(10000);
                        if (!exited)
                        {
                            Log($"Server process PID {ServerProcess.Id} did not exit within 10s. Force killing process...");
                            ServerProcess.Kill(true);
                        }
                    }
                }
                catch (Exception exKill)
                {
                    Log($"Server process termination note: {exKill.Message}");
                }
            }

            // Step 3: Failsafe search and kill for any lingering server processes in Windows Task Manager
            KillAllConanServerProcesses();

            SetStatus("STOPPED");
            ServerProcess = null;
            Log("Server is completely stopped and verified offline.");
            _ = Task.Run(async () => await SendDiscordNotificationAsync(Config.ShutdownDiscordMsg));
        }

        private void KillAllSteamCmdProcesses()
        {
            try
            {
                if (CurrentSteamCmdProcess != null && !CurrentSteamCmdProcess.HasExited)
                {
                    CurrentSteamCmdProcess.Kill(true);
                }
            }
            catch { }

            try
            {
                foreach (var p in Process.GetProcessesByName("steamcmd"))
                {
                    try { p.Kill(); } catch { }
                }
            }
            catch { }
        }

        private void KillAllConanServerProcesses()
        {
            string targetExeName = "ConanSandboxServer-Win64-Shipping";
            try
            {
                var processes = Process.GetProcessesByName(targetExeName);
                foreach (var proc in processes)
                {
                    try
                    {
                        Log($"Failsafe: Terminating remaining Conan server process PID {proc.Id}...");
                        proc.Kill(true);
                    }
                    catch (Exception ex)
                    {
                        Log($"Failsafe process kill note (PID {proc.Id}): {ex.Message}");
                    }
                }
            }
            catch { }

            try
            {
                var processes = Process.GetProcessesByName("ConanSandboxServer");
                foreach (var proc in processes)
                {
                    try
                    {
                        proc.Kill(true);
                    }
                    catch { }
                }
            }
            catch { }
        }

        public async Task<string> CreateHotBackupAsync()
        {
            if (!File.Exists(GameDbPath))
            {
                Log("Backup skipped: game.db does not exist yet.");
                return "game.db not found.";
            }

            Directory.CreateDirectory(BackupDir);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupPath = Path.Combine(BackupDir, $"game_{timestamp}.db");

            Log($"Starting SQLite Online Hot Backup: {Path.GetFileName(GameDbPath)} -> {Path.GetFileName(backupPath)}...");

            try
            {
                await Task.Run(() =>
                {
                    string srcConnStr = $"Data Source={GameDbPath};Mode=ReadOnly;";
                    string destConnStr = $"Data Source={backupPath};";

                    using var srcConn = new Microsoft.Data.Sqlite.SqliteConnection(srcConnStr);
                    using var destConn = new Microsoft.Data.Sqlite.SqliteConnection(destConnStr);

                    srcConn.Open();
                    destConn.Open();

                    srcConn.BackupDatabase(destConn);
                });

                var fi = new FileInfo(backupPath);
                double mb = Math.Round((double)fi.Length / (1024 * 1024), 2);
                Log($"Backup created successfully: {fi.Name} ({mb} MB)");
                return $"Backup created: {fi.Name} ({mb} MB)";
            }
            catch (Exception ex)
            {
                Log($"Hot backup error: {ex.Message}");
                return $"Backup error: {ex.Message}";
            }
        }

        public async Task<AppUpdateInfo?> CheckForAppUpdateAsync()
        {
            if (string.IsNullOrWhiteSpace(Config.GitHubRepo)) return null;

            try
            {
                Log($"Checking GitHub Releases for application updates ({Config.GitHubRepo})...");
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.Add("User-Agent", "ConanEnhancedServerManager-Updater");

                string url = $"https://api.github.com/repos/{Config.GitHubRepo.Trim()}/releases/latest";
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    Log($"Update check: GitHub API returned {(int)response.StatusCode} ({response.ReasonPhrase})");
                    return null;
                }

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
                string body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";
                string cleanVersion = tagName.TrimStart('v', 'V').Trim();

                if (string.IsNullOrWhiteSpace(cleanVersion)) return null;

                bool isNewer = false;
                if (Version.TryParse(cleanVersion, out Version? remoteVer) && Version.TryParse(CurrentAppVersion, out Version? currentVer))
                {
                    isNewer = remoteVer > currentVer;
                }
                else
                {
                    isNewer = !cleanVersion.Equals(CurrentAppVersion, StringComparison.OrdinalIgnoreCase);
                }

                string downloadUrl = "";
                long assetSize = 0;

                if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assetsProp.EnumerateArray())
                    {
                        string name = asset.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.TryGetProperty("browser_download_url", out var dlProp) ? dlProp.GetString() ?? "" : "";
                            assetSize = asset.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt64() : 0;
                            break;
                        }
                    }
                }

                var updateInfo = new AppUpdateInfo
                {
                    TagName = tagName,
                    VersionString = cleanVersion,
                    ReleaseNotes = body,
                    DownloadUrl = downloadUrl,
                    AssetSize = assetSize,
                    IsNewer = isNewer
                };

                LatestAppUpdate = updateInfo;
                Config.LastCheckedAppVersion = cleanVersion;

                if (isNewer)
                {
                    Log($"🎉 New application version available: {tagName} (Current: v{CurrentAppVersion})");
                    OnAppUpdateDiscovered?.Invoke(updateInfo);

                    if (Config.AutoInstallAppUpdates && !string.IsNullOrEmpty(downloadUrl))
                    {
                        if (ServerStatus == "UPDATING")
                        {
                            Log("[Auto-Updater] Game server is currently downloading updates. Deferring application auto-update...");
                        }
                        else
                        {
                            Log("[Auto-Updater] AutoInstallAppUpdates is enabled. Downloading and applying update automatically in background...");
                            _ = Task.Run(async () => await DownloadAndApplyAppUpdateAsync(updateInfo));
                        }
                    }
                }
                else
                {
                    Log($"Application is up to date (Version v{CurrentAppVersion}).");
                }

                return updateInfo;
            }
            catch (Exception ex)
            {
                Log($"Update check note: {ex.Message}");
                return null;
            }
        }

        public async Task<string> DownloadAndApplyAppUpdateAsync(Action<double>? progressCallback = null)
        {
            var info = LatestAppUpdate ?? await CheckForAppUpdateAsync();
            if (info == null || !info.UpdateAvailable)
            {
                return "No update available.";
            }
            return await DownloadAndApplyAppUpdateAsync(info, progressCallback);
        }

        public async Task<string> DownloadAndApplyAppUpdateAsync(AppUpdateInfo updateInfo, Action<double>? progressCallback = null)
        {
            if (string.IsNullOrEmpty(updateInfo.DownloadUrl))
            {
                return "Update download URL is missing.";
            }

            try
            {
                string updatesDir = Path.Combine(AppWorkingDir, "Updates");
                Directory.CreateDirectory(updatesDir);
                string zipPath = Path.Combine(updatesDir, $"update_{updateInfo.VersionString}.zip");
                string stagedDir = Path.Combine(updatesDir, "staged");

                Log($"Downloading application update {updateInfo.TagName} from GitHub...");
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "ConanEnhancedServerManager-Updater");
                    using var response = await client.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    long totalBytes = response.Content.Headers.ContentLength ?? updateInfo.AssetSize;
                    using var contentStream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                    byte[] buffer = new byte[8192];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;
                        if (totalBytes > 0)
                        {
                            double pct = (double)totalRead / totalBytes * 100.0;
                            progressCallback?.Invoke(pct);
                        }
                    }
                }

                Log("Extracting update package...");
                if (Directory.Exists(stagedDir)) Directory.Delete(stagedDir, true);
                Directory.CreateDirectory(stagedDir);
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, stagedDir, overwriteFiles: true);

                var subDirs = Directory.GetDirectories(stagedDir);
                var rootFiles = Directory.GetFiles(stagedDir);
                if (subDirs.Length == 1 && rootFiles.Length == 0)
                {
                    string innerDir = subDirs[0];
                    foreach (var f in Directory.GetFiles(innerDir))
                    {
                        string dest = Path.Combine(stagedDir, Path.GetFileName(f));
                        if (File.Exists(dest)) File.Delete(dest);
                        File.Move(f, dest);
                    }
                    foreach (var d in Directory.GetDirectories(innerDir))
                    {
                        string dest = Path.Combine(stagedDir, Path.GetFileName(d));
                        if (Directory.Exists(dest)) Directory.Delete(dest, true);
                        Directory.Move(d, dest);
                    }
                }

                string stagedConfig = Path.Combine(stagedDir, "manager_config.json");
                if (File.Exists(stagedConfig))
                {
                    try { File.Delete(stagedConfig); } catch { }
                }

                Log("Preparing update helper trampoline script...");
                string helperBat = Path.Combine(AppWorkingDir, "update_helper.bat");
                int currentPid = Process.GetCurrentProcess().Id;

                string scriptContent = $@"@echo off
title Conan Server Manager Updater
echo ========================================================
echo   Updating Conan Enhanced Server Manager to {updateInfo.TagName}
echo ========================================================
set ""TARGET=%~1""
if ""%TARGET:~-1%""==""\"" set ""TARGET=%TARGET:~0,-1%""
echo Target Directory: ""%TARGET%""
echo Waiting for running application process PID %~2 to exit...
timeout /t 2 /nobreak > nul

:WAIT_PID
tasklist /fi ""PID eq %~2"" 2>nul | find ""%~2"" > nul
if not errorlevel 1 (
    timeout /t 1 /nobreak > nul
    goto WAIT_PID
)

echo Applying updated binaries...
xcopy ""%~dp0Updates\staged\*"" ""%TARGET%\"" /E /Y /I /Q > nul

echo Restarting ConanServerManager.exe...
start """" ""%TARGET%\ConanServerManager.exe""

echo Cleaning temporary update files...
timeout /t 2 /nobreak > nul
rd /s /q ""%~dp0Updates"" 2>nul
(goto) 2>nul & del ""%~f0""
";
                await File.WriteAllTextAsync(helperBat, scriptContent);

                Log("Update staged successfully. Spawning updater helper and closing application...");
                var psi = new ProcessStartInfo
                {
                    FileName = helperBat,
                    Arguments = $"\"{AppWorkingDir}\" {currentPid}",
                    WorkingDirectory = AppWorkingDir,
                    UseShellExecute = true
                };
                Process.Start(psi);

                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    System.Windows.Application.Current.Shutdown();
                });

                return "Update initiated successfully. Restarting application...";
            }
            catch (Exception ex)
            {
                Log($"Update error: {ex.Message}");
                return $"Update failed: {ex.Message}";
            }
        }
    }
}
