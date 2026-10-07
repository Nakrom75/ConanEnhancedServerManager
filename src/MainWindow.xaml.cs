using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ConanServerManager
{
    public partial class MainWindow : Window
    {
        private readonly ServerEngine _engine;
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly DispatcherTimer _remoteTimer = new DispatcherTimer();
        private bool _isInitialized = false;
        private bool _hasSyncedRemoteConfig = false;
        private bool _isPopulatingRemoteDropdown = false;
        private int _remoteUptimeSeconds = 0;

        private bool _isModBrowserInitialized = false;
        private string? _currentDetectedModId = null;
        private const string ConanWorkshopHomeUrl = "https://steamcommunity.com/app/440900/workshop/";

        public bool IsRemoteMode => RadRemoteMode != null && RadRemoteMode.IsChecked == true;

        public MainWindow()
        {
            _engine = new ServerEngine();
            InitializeComponent();
            _isInitialized = true;

            _engine.OnLog += LogToLauncherConsole;
            _engine.OnSteamCmdLog += LogToSteamCmdConsole;
            _engine.OnErrorLog += LogToErrorConsole;
            _engine.OnStatusChanged += UpdateStatusUi;
            _engine.OnDownloadProgress += UpdateDownloadProgressUi;
            _engine.OnAppUpdateDiscovered += ShowAppUpdateBanner;
            _engine.OnSteamStatusChanged += UpdateSteamVisibilityUi;
            _engine.OnPlayersChanged += (players) => Dispatcher.Invoke(() => UpdatePlayersListUi(players));
            _engine.OnConfigSaved += () => Dispatcher.Invoke(LoadUiFromConfig);
            _engine.OnModPreDownloadCompleted += (modId, success, msg) =>
            {
                Dispatcher.Invoke(() =>
                {
                    RefreshModListDownloadedStatus();
                    if (_currentDetectedModId != null && _currentDetectedModId.Equals(modId, StringComparison.OrdinalIgnoreCase))
                    {
                        if (success)
                        {
                            TxtBrowserModStatus.Text = "✅ Pre-download complete! Ready on disk for server restart.";
                        }
                        else
                        {
                            TxtBrowserModStatus.Text = $"⚠️ Pre-download incomplete ({msg}). Will download on server update.";
                        }
                    }
                });
            };

            _remoteTimer.Interval = TimeSpan.FromSeconds(3);
            _remoteTimer.Tick += async (s, e) =>
            {
                if (IsRemoteMode)
                {
                    await PollRemoteServerAsync();
                }
            };

            Loaded += async (s, e) =>
            {
                Activate();
                Focus();
                TxtAppHeaderTitle.Text = $"Conan Enhanced Server Manager v{ServerEngine.CurrentAppVersion}";
                TxtAppHeaderVersionBadge.Text = $"✨ v{ServerEngine.CurrentAppVersion}";
                _engine.Log($"[System] Conan Enhanced Server Manager v{ServerEngine.CurrentAppVersion} initialized successfully.");
                LoadUiFromConfig();

                if (!IsRemoteMode)
                {
                    LoadIniFilesToTabs();
                    await UpdatePortStatusLedsAsync();
                    UpdateStatusUi(_engine.ServerStatus);

                    if (_engine.ServerStatus == "RUNNING")
                    {
                        _engine.Log($"[Process Monitor] Dedicated Server is currently RUNNING (PID {_engine.ServerProcess?.Id}). Re-attached.");
                    }
                    else if (_engine.Config.AutoStartOnAppLaunch && _engine.ServerStatus == "STOPPED")
                    {
                        _engine.Log("[Auto-Start] AutoStartOnAppLaunch enabled. Launching Conan Dedicated Server...");
                        _ = Task.Run(async () => await _engine.RunFullUpdateAndStartAsync());
                    }
                }
            };
        }

        private async Task UpdatePortStatusLedsAsync()
        {
            if (_engine == null) return;
            var report = await _engine.CheckAllPortsAsync();
            Dispatcher.Invoke(() =>
            {
                SetLedColor(LedGamePort, report.GamePortBound);
                SetLedColor(LedRawPort, report.RawPortBound);
                SetLedColor(LedQueryPort, report.QueryPortBound);
                SetLedColor(LedRconPort, report.RconPortBound);
                SetLedColor(LedWebPort, report.WebPortBound);
            });
        }

        private void SetLedColor(System.Windows.Shapes.Ellipse? led, bool isBound)
        {
            if (led == null) return;
            string colorHex = isBound ? "#10B981" : "#EF4444";
            led.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
            led.ToolTip = isBound ? "Port status: BOUND / ACCESSIBLE (Green)" : "Port status: NOT BOUND / CLOSED (Red)";
        }

        private void UpdateDownloadProgressUi(DownloadProgressInfo info)
        {
            Dispatcher.Invoke(() =>
            {
                if (PnlDownloadProgress == null) return;

                if (info != null && info.IsActive)
                {
                    PnlDownloadProgress.Visibility = Visibility.Visible;
                    PbDownloadProgress.Value = Math.Max(0, Math.Min(100, info.Percent));

                    TxtDownloadItemInfo.Text = $"[{info.CurrentIndex}/{info.TotalCount}] {info.ItemName}";

                    string speedStr = info.SpeedMBs > 0 ? $"{info.SpeedMBs:F1} MB/s" : "Calculating...";
                    string etaStr = info.Eta > TimeSpan.Zero ? info.Eta.ToString(@"hh\:mm\:ss") : "--:--:--";
                    TxtDownloadSpeedEta.Text = $"Speed: {speedStr} | ETA: {etaStr} ({info.Percent:F1}%)";
                }
                else
                {
                    PnlDownloadProgress.Visibility = Visibility.Collapsed;
                }
            });
        }

        private void RadMode_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || TxtServerPathInfo == null) return; // Guard against XAML initialization ordering

            if (IsRemoteMode)
            {
                _hasSyncedRemoteConfig = false;
                if (PnlRemoteStatusIndicator != null) PnlRemoteStatusIndicator.Visibility = Visibility.Visible;
                if (CmbRemoteServers != null && (CmbRemoteServers.ItemsSource == null || CmbRemoteServers.Items.Count == 0))
                {
                    PopulateRemoteServerDropdown();
                }
                string targetUrl = TxtRemoteUrl?.Text?.Trim() ?? "";
                TxtServerPathInfo.Text = $"Mode: Remote Client | Target: {targetUrl}";
                _engine.Config.IsRemoteClientMode = true;
                if (!string.IsNullOrWhiteSpace(targetUrl))
                {
                    _engine.Config.RemoteServerUrl = targetUrl;
                }
                _engine.SaveConfig();
                _remoteTimer.Start();
                _ = PollRemoteServerAsync();
            }
            else
            {
                _remoteTimer.Stop();
                if (PnlRemoteStatusIndicator != null) PnlRemoteStatusIndicator.Visibility = Visibility.Collapsed;
                _engine.Config.IsRemoteClientMode = false;
                _engine.SaveConfig();
                int webPort = _engine.Config.WebPagePort;
                if (TxtServerPathInfo != null)
                {
                    TxtServerPathInfo.Text = $"Mode: Local Server Host | Embedded Web Console: http://0.0.0.0:{webPort} (http://localhost:{webPort})";
                }
                PopulateUiFromConfig(_engine.Config);
                LoadIniFilesToTabs();
                UpdateStatusUi(_engine.ServerStatus);
            }
        }

        private void PopulateRemoteServerDropdown(List<DiscoveredServer>? extraDiscovered = null)
        {
            if (CmbRemoteServers == null) return;

            _isPopulatingRemoteDropdown = true;
            try
            {
                string currentUrl = TxtRemoteUrl?.Text?.Trim() ?? _engine.Config.RemoteServerUrl ?? "";
                if (!string.IsNullOrWhiteSpace(currentUrl) && !currentUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !currentUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    currentUrl = "http://" + currentUrl;
                }

                var items = new List<DiscoveredServer>();

                // 1. Localhost
                int localWebPort = _engine.Config.WebPagePort > 0 ? _engine.Config.WebPagePort : 8088;
                string localUrl = $"http://127.0.0.1:{localWebPort}";
                items.Add(new DiscoveredServer
                {
                    ServerName = "Local Host",
                    Url = localUrl,
                    IpAddress = "127.0.0.1",
                    WebPort = localWebPort,
                    Source = "Local"
                });

                // 2. Extra Discovered Servers (from network UDP scan or initial probe)
                if (extraDiscovered != null)
                {
                    foreach (var s in extraDiscovered)
                    {
                        if (s.Source == "Local") continue;
                        if (!items.Any(x => x.Url.Equals(s.Url, StringComparison.OrdinalIgnoreCase)))
                        {
                            items.Add(s);
                        }
                    }
                }

                // 3. Saved / Recent servers from config
                if (_engine.Config.RecentRemoteServers != null)
                {
                    foreach (var savedUrl in _engine.Config.RecentRemoteServers)
                    {
                        if (string.IsNullOrWhiteSpace(savedUrl)) continue;
                        if (!items.Any(x => x.Url.Equals(savedUrl, StringComparison.OrdinalIgnoreCase)))
                        {
                            items.Add(new DiscoveredServer
                            {
                                ServerName = "Saved Server",
                                Url = savedUrl,
                                Source = "Recent"
                            });
                        }
                    }
                }

                // 4. Custom entry at bottom
                items.Add(new DiscoveredServer
                {
                    ServerName = "Custom Server",
                    Url = "",
                    Source = "Custom"
                });

                CmbRemoteServers.ItemsSource = items;

                // Match selection
                int selectedIndex = 0;
                if (!string.IsNullOrWhiteSpace(currentUrl))
                {
                    int matched = items.FindIndex(x => x.Url.Equals(currentUrl, StringComparison.OrdinalIgnoreCase));
                    if (matched >= 0)
                    {
                        selectedIndex = matched;
                    }
                    else
                    {
                        selectedIndex = items.Count - 1; // Custom
                    }
                }

                CmbRemoteServers.SelectedIndex = selectedIndex;
            }
            finally
            {
                _isPopulatingRemoteDropdown = false;
            }
        }

        private void CmbRemoteServers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isPopulatingRemoteDropdown || TxtRemoteUrl == null) return;

            if (CmbRemoteServers.SelectedItem is DiscoveredServer selected)
            {
                if (selected.Source == "Custom")
                {
                    TxtRemoteUrl.Focus();
                    TxtRemoteUrl.SelectAll();
                }
                else if (!string.IsNullOrWhiteSpace(selected.Url))
                {
                    TxtRemoteUrl.Text = selected.Url;
                    _engine.Config.RemoteServerUrl = selected.Url;
                    if (IsRemoteMode)
                    {
                        _hasSyncedRemoteConfig = false;
                        _ = PollRemoteServerAsync();
                    }
                }
            }
        }

        private async void BtnDiscoverServers_Click(object sender, RoutedEventArgs e)
        {
            if (BtnDiscoverServers == null) return;

            BtnDiscoverServers.IsEnabled = false;
            BtnDiscoverServers.Content = "⏳ Scanning...";

            try
            {
                _engine.Log("[Discovery] Scanning local network, subnets, and VMs for active Conan Server Managers (UDP 8089)...");
                var discovered = await DiscoveryHelper.DiscoverServersAsync(1500);

                int nonLocalCount = discovered.Count(x => x.Source != "Local");
                _engine.Log($"[Discovery] Scan complete. Found {nonLocalCount} remote server(s) on network/VMs.");

                PopulateRemoteServerDropdown(discovered);

                if (nonLocalCount > 0 && CmbRemoteServers.SelectedItem is DiscoveredServer curr && curr.Source == "Local")
                {
                    var firstRemote = CmbRemoteServers.Items.OfType<DiscoveredServer>().FirstOrDefault(x => x.Source != "Local" && x.Source != "Custom");
                    if (firstRemote != null)
                    {
                        CmbRemoteServers.SelectedItem = firstRemote;
                    }
                }
            }
            catch (Exception ex)
            {
                _engine.Log($"[Discovery Error] Failed during network scan: {ex.Message}");
            }
            finally
            {
                BtnDiscoverServers.IsEnabled = true;
                BtnDiscoverServers.Content = "🔍 Scan";
            }
        }

        private async void BtnConnectRemote_Click(object sender, RoutedEventArgs e)
        {
            string url = TxtRemoteUrl?.Text?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(url))
            {
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    url = "http://" + url;
                    if (TxtRemoteUrl != null) TxtRemoteUrl.Text = url;
                }
                _engine.Config.RemoteServerUrl = url;
            }
            _engine.Config.IsRemoteClientMode = true;
            _engine.SaveConfig();

            if (RadRemoteMode != null && RadRemoteMode.IsChecked != true)
            {
                RadRemoteMode.IsChecked = true;
            }

            _hasSyncedRemoteConfig = false;
            _engine.Log($"[Remote Client] Connecting to remote manager at {url}...");
            await PollRemoteServerAsync();
        }

        private async Task PollRemoteServerAsync()
        {
            if (TxtRemoteUrl == null) return;
            string baseUrl = TxtRemoteUrl.Text.Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(baseUrl)) return;

            if (!baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                baseUrl = "http://" + baseUrl;
            }

            try
            {
                using var cts = new System.Threading.CancellationTokenSource(4000);
                string statusJson = await _httpClient.GetStringAsync($"{baseUrl}/api/status", cts.Token);
                using (var doc = JsonDocument.Parse(statusJson))
                {
                    string status = doc.RootElement.GetProperty("status").GetString() ?? "STOPPED";
                    string uptime = doc.RootElement.GetProperty("uptimeString").GetString() ?? "00:00:00";
                    string srvName = doc.RootElement.GetProperty("serverName").GetString() ?? "";

                    bool steamOnline = doc.RootElement.TryGetProperty("steamOnline", out var soProp) && soProp.GetBoolean();
                    int steamPlayers = doc.RootElement.TryGetProperty("steamPlayers", out var spProp) ? spProp.GetInt32() : 0;
                    int steamMaxPlayers = doc.RootElement.TryGetProperty("steamMaxPlayers", out var smpProp) ? smpProp.GetInt32() : 0;
                    int steamPing = doc.RootElement.TryGetProperty("steamPing", out var pingProp) ? pingProp.GetInt32() : 0;
                    string steamErr = doc.RootElement.TryGetProperty("steamError", out var seProp) ? seProp.GetString() ?? "" : "";
                    int uptimeSec = doc.RootElement.TryGetProperty("uptimeSeconds", out var upProp) ? upProp.GetInt32() : 0;

                    var remoteSteam = new SteamServerInfo
                    {
                        IsOnline = steamOnline,
                        ServerName = srvName,
                        Players = steamPlayers,
                        MaxPlayers = steamMaxPlayers,
                        PingMs = steamPing,
                        ErrorMessage = steamErr
                    };

                    _remoteUptimeSeconds = uptimeSec;
                    UpdateStatusUi(status);
                    UpdateSteamVisibilityUi(remoteSteam);

                    if (doc.RootElement.TryGetProperty("players", out var playersArr) && playersArr.ValueKind == JsonValueKind.Array)
                    {
                        var remotePlayers = new List<SteamPlayerInfo>();
                        foreach (var pEl in playersArr.EnumerateArray())
                        {
                            string pName = pEl.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "" : "";
                            int pScore = pEl.TryGetProperty("score", out var sProp) ? sProp.GetInt32() : 0;
                            float pDur = pEl.TryGetProperty("durationSeconds", out var dProp) ? (float)dProp.GetDouble() : 0f;
                            int pPing = pEl.TryGetProperty("ping", out var piProp) ? piProp.GetInt32() : 0;
                            if (!string.IsNullOrWhiteSpace(pName))
                            {
                                remotePlayers.Add(new SteamPlayerInfo
                                {
                                    Name = pName,
                                    Score = pScore,
                                    DurationSeconds = pDur,
                                    Ping = pPing
                                });
                            }
                        }
                        UpdatePlayersListUi(remotePlayers, steamPlayers, steamMaxPlayers);
                    }
                    else
                    {
                        UpdatePlayersListUi(new List<SteamPlayerInfo>(), steamPlayers, steamMaxPlayers);
                    }

                    if (TxtServerPathInfo != null)
                        TxtServerPathInfo.Text = $"Remote Host: {srvName} ({status}) | Uptime: {uptime} | URL: {baseUrl}";

                    SetRemoteConnectionStatus(true, $"CONNECTED to {srvName} ({status})");

                    if (!_hasSyncedRemoteConfig)
                    {
                        _hasSyncedRemoteConfig = true;
                        _ = FetchAndPopulateRemoteConfigAsync(baseUrl);
                    }

                    if (!string.IsNullOrWhiteSpace(baseUrl) &&
                        !baseUrl.Contains("127.0.0.1") &&
                        !baseUrl.Contains("localhost"))
                    {
                        if (_engine.Config.RecentRemoteServers == null)
                            _engine.Config.RecentRemoteServers = new List<string>();

                        if (!_engine.Config.RecentRemoteServers.Contains(baseUrl, StringComparer.OrdinalIgnoreCase))
                        {
                            _engine.Config.RecentRemoteServers.Insert(0, baseUrl);
                            if (_engine.Config.RecentRemoteServers.Count > 10)
                                _engine.Config.RecentRemoteServers.RemoveAt(_engine.Config.RecentRemoteServers.Count - 1);
                            _engine.SaveConfig();
                        }
                    }
                }

                string logsJson = await _httpClient.GetStringAsync($"{baseUrl}/api/logs", cts.Token);
                var logs = JsonSerializer.Deserialize<List<string>>(logsJson);
                if (logs != null && TxtServerLog != null)
                {
                    TxtServerLog.Text = string.Join("\n", logs);
                    TxtServerLog.ScrollToEnd();
                }
            }
            catch (Exception ex)
            {
                UpdateStatusUi("OFFLINE");
                UpdateSteamVisibilityUi(new SteamServerInfo { IsOnline = false, ErrorMessage = "Remote server unreachable" });
                string errorMsg = ex is TaskCanceledException ? "Connection timed out (4s limit)" : ex.Message;
                if (TxtServerPathInfo != null)
                    TxtServerPathInfo.Text = $"Remote Connection Error: Cannot reach {baseUrl} ({errorMsg})";

                SetRemoteConnectionStatus(false, $"UNREACHABLE ({errorMsg})");
            }
        }

        private void SetRemoteConnectionStatus(bool isConnected, string message)
        {
            Dispatcher.Invoke(() =>
            {
                if (LedRemoteConnection != null)
                {
                    string colorHex = isConnected ? "#10B981" : "#EF4444";
                    LedRemoteConnection.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
                    LedRemoteConnection.ToolTip = isConnected ? "Remote Manager is CONNECTED and responding." : "Remote Manager is UNREACHABLE / DISCONNECTED.";
                }
                if (TxtRemoteStatusText != null)
                {
                    TxtRemoteStatusText.Text = isConnected ? $"Remote: ONLINE ({message})" : $"Remote: {message}";
                    string textHex = isConnected ? "#34D399" : "#F87171";
                    TxtRemoteStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(textHex));
                }
            });
        }

        private void LoadUiFromConfig()
        {
            if (!_isInitialized) return;

            if (TxtRemoteUrl != null && !string.IsNullOrWhiteSpace(_engine.Config.RemoteServerUrl))
            {
                TxtRemoteUrl.Text = _engine.Config.RemoteServerUrl;
            }

            PopulateRemoteServerDropdown();

            if (_engine.Config.IsRemoteClientMode)
            {
                if (RadRemoteMode != null && RadRemoteMode.IsChecked != true)
                    RadRemoteMode.IsChecked = true;
            }
            else
            {
                if (RadLocalMode != null && RadLocalMode.IsChecked != true)
                    RadLocalMode.IsChecked = true;
            }

            if (TxtServerPathInfo != null)
            {
                if (IsRemoteMode)
                    TxtServerPathInfo.Text = $"Mode: Remote Client | Target: {TxtRemoteUrl?.Text}";
                else
                    TxtServerPathInfo.Text = $"Mode: Local Server Host | Web API: http://localhost:{_engine.Config.WebPagePort}";
            }

            PopulateUiFromConfig(_engine.Config);
        }

        private void PopulateUiFromConfig(ManagerConfig cfg)
        {
            if (!_isInitialized || cfg == null || TxtServerName == null) return;

            TxtServerName.Text = cfg.ServerName;
            TxtServerPass.Text = cfg.ServerPassword;
            TxtAdminPass.Text = cfg.AdminPassword;
            TxtRconPass.Text = cfg.RconPassword;

            TxtGamePort.Text = cfg.GamePort.ToString();
            TxtRawPort.Text = cfg.RawUdpPort.ToString();
            TxtQueryPort.Text = cfg.QueryPort.ToString();
            TxtRconPort.Text = cfg.RconPort.ToString();
            TxtWebPort.Text = cfg.WebPagePort.ToString();

            TxtMaxPlayers.Text = cfg.MaxPlayers.ToString();
            TxtMaxTickRate.Text = cfg.MaxTickRate.ToString();

            // Populate Network Adapter dropdown
            CmbNetworkAdapter.Items.Clear();
            var adapters = _engine.GetNetworkAdapters();
            foreach (var adapter in adapters)
            {
                CmbNetworkAdapter.Items.Add(adapter.ToString());
            }
            if (adapters.Count > 0)
            {
                CmbNetworkAdapter.SelectedIndex = 0;
            }

            if (!string.IsNullOrEmpty(cfg.MacAddress))
            {
                TxtMacAddress.Text = cfg.MacAddress;
            }
            else if (adapters.Count > 0)
            {
                TxtMacAddress.Text = adapters[0].MacAddress;
            }

            TxtExternalIp.Text = string.IsNullOrEmpty(cfg.ExternalIp) ? "127.0.0.1" : cfg.ExternalIp;

            ChkUseMultihome.IsChecked = cfg.UseMultihome;
            TxtMultihomeIp.Text = cfg.MultihomeIp;

            // Region
            if (!string.IsNullOrWhiteSpace(cfg.Region))
            {
                for (int i = 0; i < CmbRegion.Items.Count; i++)
                {
                    if (CmbRegion.Items[i] is ComboBoxItem item && (item.Content?.ToString() ?? "").Equals(cfg.Region, StringComparison.OrdinalIgnoreCase))
                    {
                        CmbRegion.SelectedIndex = i;
                        break;
                    }
                }
            }

            // Auto-Update Mode
            if (!string.IsNullOrWhiteSpace(cfg.AutoUpdateRestartMode))
            {
                for (int i = 0; i < CmbAutoUpdate.Items.Count; i++)
                {
                    if (CmbAutoUpdate.Items[i] is ComboBoxItem item && (item.Content?.ToString() ?? "").Equals(cfg.AutoUpdateRestartMode, StringComparison.OrdinalIgnoreCase))
                    {
                        CmbAutoUpdate.SelectedIndex = i;
                        break;
                    }
                }
            }

            ChkValidateFiles.IsChecked = cfg.ValidateFiles;
            ChkStartIfNotRunning.IsChecked = cfg.StartServerIfNotRunning;
            ChkAutoStartOnAppLaunch.IsChecked = cfg.AutoStartOnAppLaunch;
            ChkZombieWatch.IsChecked = cfg.ZombieCheckEnabled;
            ChkAutoCheckAppUpdates.IsChecked = cfg.AutoCheckAppUpdates;
            ChkAutoInstallAppUpdates.IsChecked = cfg.AutoInstallAppUpdates;

            ChkRconEnable.IsChecked = cfg.RconEnabled;
            ChkBattlEye.IsChecked = cfg.EnableBattlEye;
            ChkVAC.IsChecked = cfg.EnableVAC;

            // Priority
            if (!string.IsNullOrWhiteSpace(cfg.PriorityClass))
            {
                for (int i = 0; i < CmbPriorityClass.Items.Count; i++)
                {
                    if (CmbPriorityClass.Items[i] is ComboBoxItem item && (item.Content?.ToString() ?? "").Equals(cfg.PriorityClass, StringComparison.OrdinalIgnoreCase))
                    {
                        CmbPriorityClass.SelectedIndex = i;
                        break;
                    }
                }
            }

            ChkDailyRestart.IsChecked = cfg.EnableDailyRestart;
            TxtDailyRestartTime.Text = cfg.DailyRestartTime;
            TxtMinUptime.Text = cfg.MinimumUptime;
            TxtRestartsPerDay.Text = cfg.RestartsPerDay.ToString();

            TxtWarn1Time.Text = cfg.FirstWarningTime;
            TxtWarn1Msg.Text = cfg.FirstWarningMsg;
            TxtWarn2Time.Text = cfg.SecondWarningTime;
            TxtWarn2Msg.Text = cfg.SecondWarningMsg;
            TxtWarn3Time.Text = cfg.ThirdWarningTime;
            TxtWarn3Msg.Text = cfg.ThirdWarningMsg;
            ChkFastRestartZeroPlayers.IsChecked = cfg.FastRestartZeroPlayers;

            ChkShutdownBackup.IsChecked = cfg.OnShutdownBackup;
            TxtBackupDays.Text = cfg.BackupLimitDays.ToString();
            TxtCustomBackupDir.Text = cfg.CustomBackupDir;

            // Backup Script Mode
            if (!string.IsNullOrWhiteSpace(cfg.BackupScriptMode))
            {
                for (int i = 0; i < CmbBackupScriptMode.Items.Count; i++)
                {
                    if (CmbBackupScriptMode.Items[i] is ComboBoxItem item && (item.Content?.ToString() ?? "").Equals(cfg.BackupScriptMode, StringComparison.OrdinalIgnoreCase))
                    {
                        CmbBackupScriptMode.SelectedIndex = i;
                        break;
                    }
                }
            }

            ChkDiscordEnable.IsChecked = cfg.DiscordEnabled;
            ChkDiscordTime.IsChecked = cfg.DiscordIncludeTime;
            TxtDiscordWebhook.Text = cfg.DiscordWebhookUrl;

            ChkUseAllCores.IsChecked = cfg.UseAllAvailableCores;

            PopulateModListUi(cfg.Mods);
        }

        private ManagerConfig GetConfigFromUi()
        {
            var cfg = new ManagerConfig();

            cfg.ServerName = TxtServerName.Text;
            cfg.ServerPassword = TxtServerPass.Text;
            cfg.AdminPassword = TxtAdminPass.Text;
            cfg.RconPassword = TxtRconPass.Text;

            if (int.TryParse(TxtGamePort.Text, out int gp)) cfg.GamePort = gp;
            if (int.TryParse(TxtRawPort.Text, out int rp)) cfg.RawUdpPort = rp;
            if (int.TryParse(TxtQueryPort.Text, out int qp)) cfg.QueryPort = qp;
            if (int.TryParse(TxtRconPort.Text, out int rcp)) cfg.RconPort = rcp;
            if (int.TryParse(TxtWebPort.Text, out int wp)) cfg.WebPagePort = wp;

            if (int.TryParse(TxtMaxPlayers.Text, out int mp)) cfg.MaxPlayers = mp;
            if (int.TryParse(TxtMaxTickRate.Text, out int mtr)) cfg.MaxTickRate = mtr;

            if (CmbRegion.SelectedItem is ComboBoxItem regItem && regItem.Content != null)
                cfg.Region = regItem.Content.ToString() ?? "0 - Europe";

            cfg.ExternalIp = TxtExternalIp.Text.Trim();
            cfg.MacAddress = TxtMacAddress.Text.Trim();
            cfg.UseMultihome = ChkUseMultihome.IsChecked == true;
            cfg.MultihomeIp = TxtMultihomeIp.Text.Trim();

            if (CmbAutoUpdate.SelectedItem is ComboBoxItem updItem && updItem.Content != null)
                cfg.AutoUpdateRestartMode = updItem.Content.ToString() ?? "Auto-Update On Restart";

            cfg.ValidateFiles = ChkValidateFiles.IsChecked == true;
            cfg.StartServerIfNotRunning = ChkStartIfNotRunning.IsChecked == true;
            cfg.AutoStartOnAppLaunch = ChkAutoStartOnAppLaunch.IsChecked == true;
            cfg.ZombieCheckEnabled = ChkZombieWatch.IsChecked == true;
            cfg.AutoCheckAppUpdates = ChkAutoCheckAppUpdates.IsChecked == true;
            cfg.AutoInstallAppUpdates = ChkAutoInstallAppUpdates.IsChecked == true;

            cfg.RconEnabled = ChkRconEnable.IsChecked == true;
            cfg.EnableBattlEye = ChkBattlEye.IsChecked == true;
            cfg.EnableVAC = ChkVAC.IsChecked == true;

            if (CmbPriorityClass.SelectedItem is ComboBoxItem prioItem && prioItem.Content != null)
                cfg.PriorityClass = prioItem.Content.ToString() ?? "Unmanaged";

            cfg.UseAllAvailableCores = ChkUseAllCores.IsChecked == true;

            cfg.EnableDailyRestart = ChkDailyRestart.IsChecked == true;
            cfg.DailyRestartTime = TxtDailyRestartTime.Text;
            cfg.MinimumUptime = TxtMinUptime.Text;
            if (int.TryParse(TxtRestartsPerDay.Text, out int rpd)) cfg.RestartsPerDay = rpd;

            cfg.FirstWarningTime = TxtWarn1Time.Text;
            cfg.FirstWarningMsg = TxtWarn1Msg.Text;
            cfg.SecondWarningTime = TxtWarn2Time.Text;
            cfg.SecondWarningMsg = TxtWarn2Msg.Text;
            cfg.ThirdWarningTime = TxtWarn3Time.Text;
            cfg.ThirdWarningMsg = TxtWarn3Msg.Text;
            cfg.FastRestartZeroPlayers = ChkFastRestartZeroPlayers.IsChecked == true;

            cfg.OnShutdownBackup = ChkShutdownBackup.IsChecked == true;
            if (int.TryParse(TxtBackupDays.Text, out int bd)) cfg.BackupLimitDays = bd;
            cfg.CustomBackupDir = TxtCustomBackupDir.Text.Trim();

            if (CmbBackupScriptMode.SelectedItem is ComboBoxItem scriptItem && scriptItem.Content != null)
                cfg.BackupScriptMode = scriptItem.Content.ToString() ?? "Don't Run Scripts";

            cfg.DiscordEnabled = ChkDiscordEnable.IsChecked == true;
            cfg.DiscordIncludeTime = ChkDiscordTime.IsChecked == true;
            cfg.DiscordWebhookUrl = TxtDiscordWebhook.Text.Trim();

            cfg.Mods = LstMods.Items.Cast<object>()
                .Select(item => item is ModDisplayItem m ? m.Id : item?.ToString()?.Trim() ?? "")
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();

            cfg.IsRemoteClientMode = RadRemoteMode.IsChecked == true;
            cfg.RemoteServerUrl = TxtRemoteUrl.Text.Trim();

            return cfg;
        }

        private void SaveConfigFromUi()
        {
            var cfg = GetConfigFromUi();
            _engine.UpdateConfig(cfg);
            _engine.Log($"Configuration saved cleanly to manager_config.json. Web API Port: {_engine.Config.WebPagePort}");
            LoadIniFilesToTabs();
        }

        private async Task FetchAndPopulateRemoteConfigAsync(string baseUrl)
        {
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(5000);
                string configJson = await _httpClient.GetStringAsync($"{baseUrl}/api/config", cts.Token);
                var remoteCfg = JsonSerializer.Deserialize<ManagerConfig>(configJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (remoteCfg != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        PopulateUiFromConfig(remoteCfg);
                    });
                    _engine.Log($"[Remote Client] Synchronized configuration from remote server {baseUrl}.");
                }

                await FetchRemoteIniFileAsync(baseUrl, "ServerSettings.ini", TxtServerSettingsIni);
                await FetchRemoteIniFileAsync(baseUrl, "Engine.ini", TxtEngineIni);
                await FetchRemoteIniFileAsync(baseUrl, "Game.ini", TxtGameIni);
            }
            catch (Exception ex)
            {
                _engine.Log($"[Remote Client] Note fetching remote config/INIs: {ex.Message}");
            }
        }

        private async Task FetchRemoteIniFileAsync(string baseUrl, string fileName, TextBox? targetBox)
        {
            if (targetBox == null) return;
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(5000);
                string iniJson = await _httpClient.GetStringAsync($"{baseUrl}/api/ini?file={fileName}", cts.Token);
                using var doc = JsonDocument.Parse(iniJson);
                if (doc.RootElement.TryGetProperty("content", out var cProp))
                {
                    string text = cProp.GetString() ?? "";
                    Dispatcher.Invoke(() =>
                    {
                        targetBox.Text = text;
                    });
                }
            }
            catch (Exception ex)
            {
                _engine.Log($"[Remote Client] Could not fetch remote {fileName}: {ex.Message}");
            }
        }

        private async Task SaveRemoteIniFileAsync(string fileName, string content)
        {
            string baseUrl = TxtRemoteUrl.Text.Trim().TrimEnd('/');
            if (!baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                baseUrl = "http://" + baseUrl;
            }

            try
            {
                var payload = new { file = fileName, content = content };
                string json = JsonSerializer.Serialize(payload);
                var res = await _httpClient.PostAsync($"{baseUrl}/api/ini", new StringContent(json, Encoding.UTF8, "application/json"));
                if (res.IsSuccessStatusCode)
                {
                    MessageBox.Show($"Remote {fileName} saved successfully.", "Remote INI Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                    _engine.Log($"[Remote Client] Saved {fileName} remotely.");
                    await FetchAndPopulateRemoteConfigAsync(baseUrl);
                }
                else
                {
                    string err = await res.Content.ReadAsStringAsync();
                    MessageBox.Show($"Remote INI save error:\n{err}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save {fileName} remotely:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CmbNetworkAdapter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || CmbNetworkAdapter == null || TxtMacAddress == null) return;
            if (CmbNetworkAdapter.SelectedItem != null)
            {
                string sel = CmbNetworkAdapter.SelectedItem.ToString() ?? "";
                _engine.Config.SelectedNetworkInterface = sel;
                var adapters = _engine.GetNetworkAdapters();
                var matched = adapters.FirstOrDefault(a => a.ToString() == sel);
                if (matched != null)
                {
                    TxtMacAddress.Text = matched.MacAddress;
                }
            }
        }

        private void BtnCopyConnectionInfo_Click(object sender, RoutedEventArgs e)
        {
            string ip = string.IsNullOrEmpty(_engine.Config.ExternalIp) ? "127.0.0.1" : _engine.Config.ExternalIp;
            string info = $"Server Name: {TxtServerName.Text}\nDirect Connect: {ip}:{TxtGamePort.Text}\nPassword: {TxtServerPass.Text}";
            try
            {
                Clipboard.SetText(info);
                MessageBox.Show($"Copied connection details to Clipboard:\n\n{info}", "Connection Details Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _engine.Log($"Clipboard copy note: {ex.Message}");
            }
        }

        private async void BtnTestPorts_Click(object sender, RoutedEventArgs e)
        {
            SaveConfigFromUi();
            await UpdatePortStatusLedsAsync();
            string report = await _engine.TestPortAccessibilityAsync();
            MessageBox.Show(report, "Port Accessibility Diagnostic", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RefreshModListUi()
        {
            PopulateModListUi(_engine.Config.Mods);
        }

        private void RefreshModListDownloadedStatus()
        {
            foreach (var item in LstMods.Items.OfType<ModDisplayItem>())
            {
                item.IsDownloaded = _engine.IsModDownloaded(item.Id);
            }
        }

        private void PopulateModListUi(IEnumerable<string>? modIds)
        {
            LstMods.Items.Clear();
            if (modIds == null) return;
            var list = modIds.Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m.Trim()).ToList();
            if (list.Count == 0) return;

            var items = new List<ModDisplayItem>();
            foreach (var id in list)
            {
                var cached = SteamWorkshopHelper.GetCachedMod(id);
                var item = new ModDisplayItem
                {
                    Id = id,
                    Title = cached != null && !string.IsNullOrWhiteSpace(cached.Title) && !cached.Title.StartsWith("Mod #")
                        ? cached.Title
                        : $"Loading Mod #{id}...",
                    PreviewUrl = cached?.PreviewUrl ?? "",
                    IsDownloaded = _engine.IsModDownloaded(id)
                };
                items.Add(item);
                LstMods.Items.Add(item);
            }

            // Asynchronously resolve actual titles from Steam Workshop if any are missing
            _ = Task.Run(async () =>
            {
                try
                {
                    var map = await SteamWorkshopHelper.GetMultipleModDetailsAsync(list);
                    Dispatcher.Invoke(() =>
                    {
                        foreach (var item in items)
                        {
                            if (map.TryGetValue(item.Id, out var detail) && !string.IsNullOrWhiteSpace(detail.Title))
                            {
                                item.Title = detail.Title;
                                item.PreviewUrl = detail.PreviewUrl;
                            }
                            else if (item.Title.StartsWith("Loading"))
                            {
                                item.Title = $"Mod #{item.Id}";
                            }
                        }
                    });
                }
                catch { }
            });
        }

        private void LoadIniFilesToTabs()
        {
            if (IsRemoteMode) return;
            try
            {
                TxtServerSettingsIni.Text = _engine.GetIniText(_engine.ServerSettingsIni);
                TxtEngineIni.Text = _engine.GetIniText(_engine.EngineIni);
                TxtGameIni.Text = _engine.GetIniText(_engine.GameIni);
            }
            catch (Exception ex)
            {
                _engine.Log($"Error reading INI tabs: {ex.Message}");
            }
        }

        private void LogToLauncherConsole(string message)
        {
            Dispatcher.Invoke(() =>
            {
                if (TxtServerLog != null)
                {
                    TxtServerLog.AppendText(message + "\n");
                    TxtServerLog.ScrollToEnd();
                }
                if (TxtLauncherLog != null)
                {
                    TxtLauncherLog.AppendText(message + "\n");
                    TxtLauncherLog.ScrollToEnd();
                }
            });
        }

        private void LogToSteamCmdConsole(string message)
        {
            Dispatcher.Invoke(() =>
            {
                if (TxtSteamCmdLog != null)
                {
                    TxtSteamCmdLog.AppendText(message + "\n");
                    TxtSteamCmdLog.ScrollToEnd();
                }
            });
        }

        private void LogToErrorConsole(string message)
        {
            Dispatcher.Invoke(() =>
            {
                if (TxtErrorLog != null)
                {
                    TxtErrorLog.AppendText(message + "\n");
                    TxtErrorLog.ScrollToEnd();
                }
            });
        }

        private void UpdateStatusUi(string status)
        {
            Dispatcher.Invoke(() =>
            {
                if (StatusText != null) StatusText.Text = status;
                if (StatusBadge != null)
                {
                    if (status == "RUNNING")
                    {
                        StatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2610B981"));
                        StatusBadge.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4D10B981"));
                        if (StatusText != null) StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"));
                    }
                    else if (status == "UPDATING")
                    {
                        StatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#26F59E0B"));
                        StatusBadge.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4DF59E0B"));
                        if (StatusText != null) StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FBBF24"));
                    }
                    else
                    {
                        StatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#26EF4444"));
                        StatusBadge.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4DEF4444"));
                        if (StatusText != null) StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));
                    }
                }
                if (!IsRemoteMode)
                {
                    UpdateSteamVisibilityUi(_engine.SteamStatus);
                }
            });
        }

        private void UpdateSteamVisibilityUi(SteamServerInfo info)
        {
            Dispatcher.Invoke(() =>
            {
                if (BadgeSteamVisibility == null || LedSteamVisibility == null || TxtSteamVisibility == null) return;

                string currentStatus = IsRemoteMode ? (StatusText?.Text ?? "STOPPED") : _engine.ServerStatus;

                if (currentStatus == "STOPPED" || currentStatus == "OFFLINE")
                {
                    LedSteamVisibility.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
                    TxtSteamVisibility.Text = "STEAM: OFFLINE";
                    TxtSteamVisibility.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
                    BadgeSteamVisibility.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"));
                    BadgeSteamVisibility.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155"));
                    BadgeSteamVisibility.ToolTip = "Server is stopped / offline.";
                }
                else if (currentStatus == "UPDATING")
                {
                    LedSteamVisibility.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8"));
                    TxtSteamVisibility.Text = "STEAM: UPDATING...";
                    TxtSteamVisibility.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8"));
                    BadgeSteamVisibility.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2638BDF8"));
                    BadgeSteamVisibility.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8"));
                    BadgeSteamVisibility.ToolTip = "Server manager is currently downloading updates or mods via SteamCMD.";
                }
                else // RUNNING
                {
                    if (info != null && info.IsOnline)
                    {
                        // GREEN LIGHT: Server is visibly responding on Steam!
                        LedSteamVisibility.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                        string playersText = info.MaxPlayers > 0 ? $"{info.Players}/{info.MaxPlayers}" : $"{info.Players}";
                        TxtSteamVisibility.Text = $"STEAM: ONLINE ({playersText})";
                        TxtSteamVisibility.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"));
                        BadgeSteamVisibility.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2610B981"));
                        BadgeSteamVisibility.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                        BadgeSteamVisibility.ToolTip = $"Server is VERIFIED ONLINE and actively responding on Steam Query Port!\n\nName: {info.ServerName}\nMap: {info.Map}\nPlayers: {playersText}\nPing: {info.PingMs}ms";

                        if (TxtPlayersCountBadge != null)
                        {
                            TxtPlayersCountBadge.Text = playersText;
                        }

                        if (!IsRemoteMode && info.PlayerList != null && info.PlayerList.Count > 0)
                        {
                            UpdatePlayersListUi(info.PlayerList, info.Players, info.MaxPlayers);
                        }
                    }
                    else
                    {
                        if (!IsRemoteMode)
                        {
                            UpdatePlayersListUi(new List<SteamPlayerInfo>(), 0, _engine?.Config?.MaxPlayers ?? 40);
                        }
                        bool isLongStartup = false;
                        if (IsRemoteMode)
                        {
                            isLongStartup = _remoteUptimeSeconds >= 240;
                        }
                        else
                        {
                            DateTime startTime = _engine?.ServerStartTime ?? DateTime.MinValue;
                            isLongStartup = startTime > DateTime.MinValue && (DateTime.Now - startTime).TotalMinutes >= 4;
                        }

                        if (isLongStartup)
                        {
                            // RED: Server running for 4+ mins without answering Steam queries
                            LedSteamVisibility.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                            TxtSteamVisibility.Text = "STEAM: UNRESPONSIVE";
                            TxtSteamVisibility.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));
                            BadgeSteamVisibility.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#26EF4444"));
                            BadgeSteamVisibility.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                            string note = info?.ErrorMessage ?? "Connection timed out";
                            BadgeSteamVisibility.ToolTip = $"Warning: Server process is running, but has failed to answer Steam queries for over 4 minutes.\nNote: {note}\nThe server may be frozen in a loop or locked database.";
                        }
                        else
                        {
                            // AMBER / YELLOW: Normal loading pipeline
                            LedSteamVisibility.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
                            TxtSteamVisibility.Text = "STEAM: STARTING UP...";
                            TxtSteamVisibility.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FBBF24"));
                            BadgeSteamVisibility.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#26F59E0B"));
                            BadgeSteamVisibility.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
                            BadgeSteamVisibility.ToolTip = "Conan Dedicated Server process is active. Game engine is initializing mods and database. Waiting for Steam Query Port to come online...";
                        }
                    }
                }
            });
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (IsRemoteMode)
            {
                await SendRemoteActionAsync("start");
            }
            else
            {
                SaveConfigFromUi();
                await _engine.RunFullUpdateAndStartAsync();
            }
        }

        private async void BtnStartNoUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (IsRemoteMode)
            {
                await SendRemoteActionAsync("start-noupdate");
            }
            else
            {
                SaveConfigFromUi();
                await _engine.StartServerWithoutUpdateAsync();
            }
        }

        private async void BtnRestart_Click(object sender, RoutedEventArgs e)
        {
            if (IsRemoteMode)
            {
                await SendRemoteActionAsync("restart");
            }
            else
            {
                SaveConfigFromUi();
                await _engine.RestartServerAsync();
            }
        }

        private async void BtnRestartNoUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (IsRemoteMode)
            {
                await SendRemoteActionAsync("restart-noupdate");
            }
            else
            {
                SaveConfigFromUi();
                await _engine.RestartServerWithoutUpdateAsync();
            }
        }

        private async void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            if (IsRemoteMode)
            {
                await SendRemoteActionAsync("stop");
            }
            else
            {
                await _engine.StopServerAsync();
            }
        }

        private async void BtnBackup_Click(object sender, RoutedEventArgs e)
        {
            if (IsRemoteMode)
            {
                await SendRemoteActionAsync("backup");
            }
            else
            {
                string res = await _engine.CreateHotBackupAsync();
                MessageBox.Show(res, "Hot Backup Result", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnBrowseBackupDir_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Custom Backup Directory"
            };
            if (!string.IsNullOrWhiteSpace(TxtCustomBackupDir.Text) && System.IO.Directory.Exists(TxtCustomBackupDir.Text))
            {
                dlg.InitialDirectory = TxtCustomBackupDir.Text;
            }
            if (dlg.ShowDialog() == true)
            {
                TxtCustomBackupDir.Text = dlg.FolderName;
            }
        }

        private async Task SendRemoteActionAsync(string action)
        {
            string baseUrl = TxtRemoteUrl.Text.Trim().TrimEnd('/');
            try
            {
                var res = await _httpClient.PostAsync($"{baseUrl}/api/control/{action}", new StringContent("", Encoding.UTF8, "application/json"));
                string body = await res.Content.ReadAsStringAsync();
                MessageBox.Show($"Remote Response:\n{body}", "Remote Action Result", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Remote Action Failed:\n{ex.Message}", "Remote Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
        {
            if (IsRemoteMode)
            {
                string baseUrl = TxtRemoteUrl.Text.Trim().TrimEnd('/');
                if (!baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    baseUrl = "http://" + baseUrl;
                }

                try
                {
                    var cfg = GetConfigFromUi();
                    string json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var res = await _httpClient.PostAsync($"{baseUrl}/api/config", content);
                    if (res.IsSuccessStatusCode)
                    {
                        _engine.Config.IsRemoteClientMode = true;
                        _engine.Config.RemoteServerUrl = baseUrl;
                        _engine.SaveConfig();

                        MessageBox.Show($"Configuration saved successfully to remote server at {baseUrl}.", "Remote Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                        _engine.Log($"[Remote Client] Configuration saved to remote server {baseUrl}.");
                    }
                    else
                    {
                        string err = await res.Content.ReadAsStringAsync();
                        MessageBox.Show($"Remote save returned error:\n{err}", "Remote Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save config remotely:\n{ex.Message}", "Remote Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return;
            }

            SaveConfigFromUi();
            MessageBox.Show($"Configuration updated successfully.\nWeb API running on port {_engine.Config.WebPagePort}.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnModMoveUp_Click(object sender, RoutedEventArgs e)
        {
            int idx = LstMods.SelectedIndex;
            if (idx > 0)
            {
                var item = LstMods.Items[idx];
                LstMods.Items.RemoveAt(idx);
                LstMods.Items.Insert(idx - 1, item);
                LstMods.SelectedIndex = idx - 1;
            }
        }

        private void BtnModMoveDown_Click(object sender, RoutedEventArgs e)
        {
            int idx = LstMods.SelectedIndex;
            if (idx >= 0 && idx < LstMods.Items.Count - 1)
            {
                var item = LstMods.Items[idx];
                LstMods.Items.RemoveAt(idx);
                LstMods.Items.Insert(idx + 1, item);
                LstMods.SelectedIndex = idx + 1;
            }
        }

        private void BtnAddMod_Click(object sender, RoutedEventArgs e)
        {
            string newId = TxtNewModId.Text.Trim();
            if (string.IsNullOrEmpty(newId)) return;

            bool exists = LstMods.Items.OfType<ModDisplayItem>().Any(m => m.Id.Equals(newId, StringComparison.OrdinalIgnoreCase))
                || LstMods.Items.OfType<string>().Any(s => s.Equals(newId, StringComparison.OrdinalIgnoreCase));
            if (exists)
            {
                MessageBox.Show($"Mod {newId} is already in the list.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var cached = SteamWorkshopHelper.GetCachedMod(newId);
            var newItem = new ModDisplayItem
            {
                Id = newId,
                Title = cached != null && !string.IsNullOrWhiteSpace(cached.Title) ? cached.Title : $"Loading Mod #{newId}...",
                PreviewUrl = cached?.PreviewUrl ?? "",
                IsDownloaded = _engine.IsModDownloaded(newId)
            };
            LstMods.Items.Add(newItem);
            TxtNewModId.Clear();

            if (!newItem.IsDownloaded)
            {
                var askPreDownload = MessageBox.Show($"Mod #{newId} added to server list.\nWould you like to pre-download it in the background now so it's ready on disk for server restart?", "Pre-Download Mod", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (askPreDownload == MessageBoxResult.Yes)
                {
                    _ = Task.Run(async () => await _engine.PreDownloadModAsync(newId));
                }
            }

            _ = Task.Run(async () =>
            {
                var detail = await SteamWorkshopHelper.GetModDetailsAsync(newId);
                Dispatcher.Invoke(() =>
                {
                    if (detail != null && !string.IsNullOrWhiteSpace(detail.Title))
                    {
                        newItem.Title = detail.Title;
                        newItem.PreviewUrl = detail.PreviewUrl;
                    }
                    else if (newItem.Title.StartsWith("Loading"))
                    {
                        newItem.Title = $"Mod #{newId}";
                    }
                });
            });
        }

        private void LstMods_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.OriginalSource is DependencyObject dep)
                {
                    var item = ItemsControl.ContainerFromElement(LstMods, dep) as ListBoxItem;
                    if (item != null)
                    {
                        item.IsSelected = true;
                    }
                }
            }
            catch { }
        }

        private ModDisplayItem? GetSelectedOrClickedMod(object sender)
        {
            if (sender is MenuItem mi)
            {
                if (mi.DataContext is ModDisplayItem item) return item;
                if (mi.CommandParameter is ModDisplayItem cpItem) return cpItem;
            }
            if (LstMods.SelectedItem is ModDisplayItem selItem)
            {
                return selItem;
            }
            if (LstMods.SelectedItem is string idStr)
            {
                return new ModDisplayItem { Id = idStr, Title = $"Mod #{idStr}" };
            }
            return null;
        }

        private void MnuPreDownloadMod_Click(object sender, RoutedEventArgs e)
        {
            var mod = GetSelectedOrClickedMod(sender);
            if (mod == null || string.IsNullOrWhiteSpace(mod.Id))
            {
                MessageBox.Show("Please select a mod from the list first.", "Pre-Download Mod", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            TriggerModPreDownload(mod.Id);
        }

        private void BtnPreDownloadMod_Click(object sender, RoutedEventArgs e)
        {
            var mod = GetSelectedOrClickedMod(sender);
            if (mod == null || string.IsNullOrWhiteSpace(mod.Id))
            {
                MessageBox.Show("Please select a mod from the list first.", "Pre-Download Mod", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            TriggerModPreDownload(mod.Id);
        }

        private void TriggerModPreDownload(string modId)
        {
            string trimmed = modId.Trim();
            if (_engine.IsModPreDownloading)
            {
                MessageBox.Show($"SteamCMD is currently busy pre-downloading Mod #{_engine.CurrentPreDownloadingModId}.\nPlease wait for it to finish.", "Pre-Download Active", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_engine.ServerStatus == "UPDATING")
            {
                MessageBox.Show("Server is currently performing a full update. Please wait for the update to complete.", "Server Updating", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            bool alreadyDownloaded = _engine.IsModDownloaded(trimmed);
            if (alreadyDownloaded)
            {
                var prompt = MessageBox.Show($"Workshop Mod #{trimmed} is already downloaded and present on disk.\nDo you want to run SteamCMD verification in the background to ensure it is up to date?", "Mod Already On Disk", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (prompt != MessageBoxResult.Yes) return;
            }

            _engine.Log($"[Mods] Initiating background pre-download / validation for Mod #{trimmed}...");
            _ = Task.Run(async () => await _engine.PreDownloadModAsync(trimmed));
        }

        private void MnuOpenWorkshopPage_Click(object sender, RoutedEventArgs e)
        {
            var mod = GetSelectedOrClickedMod(sender);
            if (mod == null || string.IsNullOrWhiteSpace(mod.Id))
            {
                MessageBox.Show("Please select a mod first.", "Open Workshop", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            OpenSteamWorkshopPage(mod.Id);
        }

        private void BtnViewModWorkshop_Click(object sender, RoutedEventArgs e)
        {
            var mod = GetSelectedOrClickedMod(sender);
            if (mod == null || string.IsNullOrWhiteSpace(mod.Id))
            {
                MessageBox.Show("Please select a mod from the list first.", "Open Workshop", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            OpenSteamWorkshopPage(mod.Id);
        }

        private void OpenSteamWorkshopPage(string modId)
        {
            try
            {
                string url = $"https://steamcommunity.com/sharedfiles/filedetails/?id={modId.Trim()}";
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open browser:\n{ex.Message}", "Browser Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && PnlFullPageBrowser.Visibility == Visibility.Visible)
            {
                CloseFullPageBrowser();
                e.Handled = true;
            }
        }

        public void OpenFullPageBrowser(string? initialUrlOrModId = null)
        {
            PnlFullPageBrowser.Visibility = Visibility.Visible;
            if (!string.IsNullOrWhiteSpace(initialUrlOrModId))
            {
                string trimmed = initialUrlOrModId.Trim();
                if (Regex.IsMatch(trimmed, @"^\d{6,12}$"))
                {
                    NavigateBrowserToUrl($"https://steamcommunity.com/sharedfiles/filedetails/?id={trimmed}");
                }
                else if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    NavigateBrowserToUrl(trimmed);
                }
                else
                {
                    NavigateBrowserToUrl($"https://steamcommunity.com/workshop/browse/?appid=440900&searchtext={Uri.EscapeDataString(trimmed)}&browsesort=textsearch&section=readytouseitems");
                }
            }
            else
            {
                if (!_isModBrowserInitialized)
                {
                    _ = InitializeModBrowserAsync();
                }
                else if (string.IsNullOrWhiteSpace(TxtBrowserUrl.Text))
                {
                    NavigateBrowserToUrl(ConanWorkshopHomeUrl);
                }
            }
        }

        public void CloseFullPageBrowser()
        {
            PnlFullPageBrowser.Visibility = Visibility.Collapsed;
        }

        private void BtnCloseFullPageBrowser_Click(object sender, RoutedEventArgs e)
        {
            CloseFullPageBrowser();
        }

        private void BtnOpenWorkshopBrowser_Click(object sender, RoutedEventArgs e)
        {
            OpenFullPageBrowser();
        }

        private async Task InitializeModBrowserAsync()
        {
            if (_isModBrowserInitialized) return;
            try
            {
                await WvModBrowser.EnsureCoreWebView2Async();
                _isModBrowserInitialized = true;
                Dispatcher.Invoke(() =>
                {
                    PnlWebView2Error.Visibility = Visibility.Collapsed;
                    WvModBrowser.Visibility = Visibility.Visible;

                    WvModBrowser.SourceChanged += (s, e) => OnModBrowserSourceChanged();
                    WvModBrowser.NavigationCompleted += (s, e) => OnModBrowserNavigationCompleted();

                    WvModBrowser.CoreWebView2.Navigate(ConanWorkshopHomeUrl);
                    TxtBrowserUrl.Text = ConanWorkshopHomeUrl;
                });
                _engine.Log("[ModBrowser] In-app Chromium WebView2 initialized successfully.");
            }
            catch (Exception ex)
            {
                _engine.Log($"[ModBrowser] WebView2 initialization failed: {ex.Message}");
                Dispatcher.Invoke(() =>
                {
                    PnlWebView2Error.Visibility = Visibility.Visible;
                    WvModBrowser.Visibility = Visibility.Collapsed;
                });
            }
        }

        private void OnModBrowserSourceChanged()
        {
            string url = WvModBrowser.Source?.ToString() ?? "";
            TxtBrowserUrl.Text = url;
            CheckModUrlAndSyncBanner(url);
        }

        private void OnModBrowserNavigationCompleted()
        {
            string url = WvModBrowser.Source?.ToString() ?? "";
            TxtBrowserUrl.Text = url;
            CheckModUrlAndSyncBanner(url);
        }

        private void CheckModUrlAndSyncBanner(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                PnlBrowserModBanner.Visibility = Visibility.Collapsed;
                _currentDetectedModId = null;
                return;
            }

            var match = Regex.Match(url, @"steamcommunity\.com/sharedfiles/filedetails/\?id=(?<id>\d+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string modId = match.Groups["id"].Value;
                _currentDetectedModId = modId;
                TxtBrowserModId.Text = $"Mod ID: {modId}";

                bool isInstalled = _engine.Config.Mods.Any(m => m.Equals(modId, StringComparison.OrdinalIgnoreCase))
                    || LstMods.Items.OfType<ModDisplayItem>().Any(m => m.Id.Equals(modId, StringComparison.OrdinalIgnoreCase))
                    || LstMods.Items.OfType<string>().Any(s => s.Equals(modId, StringComparison.OrdinalIgnoreCase));

                if (isInstalled)
                {
                    if (_engine.IsModPreDownloading && _engine.CurrentPreDownloadingModId == modId)
                    {
                        TxtBrowserModStatus.Text = "📥 Pre-downloading in background...";
                    }
                    else if (_engine.IsModDownloaded(modId))
                    {
                        TxtBrowserModStatus.Text = "✅ Installed & Ready on Disk";
                    }
                    else
                    {
                        TxtBrowserModStatus.Text = "⚠️ Installed on server (Files not pre-downloaded yet)";
                    }
                    BtnAddBrowserModToServer.Visibility = Visibility.Collapsed;
                    BtnRemoveBrowserModFromServer.Visibility = Visibility.Visible;
                }
                else
                {
                    TxtBrowserModStatus.Text = "Not installed on server";
                    BtnAddBrowserModToServer.Visibility = Visibility.Visible;
                    BtnRemoveBrowserModFromServer.Visibility = Visibility.Collapsed;
                }
                PnlBrowserModBanner.Visibility = Visibility.Visible;
            }
            else
            {
                _currentDetectedModId = null;
                PnlBrowserModBanner.Visibility = Visibility.Collapsed;
            }
        }

        private void NavigateBrowserToUrl(string url)
        {
            TxtBrowserUrl.Text = url;
            if (!_isModBrowserInitialized)
            {
                _ = Task.Run(async () =>
                {
                    await InitializeModBrowserAsync();
                    if (_isModBrowserInitialized)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            WvModBrowser.CoreWebView2?.Navigate(url);
                        });
                    }
                });
                return;
            }

            WvModBrowser.CoreWebView2?.Navigate(url);
        }

        private void NavigateToUserUrl()
        {
            string input = TxtBrowserUrl.Text.Trim();
            if (string.IsNullOrWhiteSpace(input)) return;

            if (Regex.IsMatch(input, @"^\d{6,12}$"))
            {
                NavigateBrowserToUrl($"https://steamcommunity.com/sharedfiles/filedetails/?id={input}");
            }
            else if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                NavigateBrowserToUrl(input);
            }
            else
            {
                NavigateBrowserToUrl($"https://steamcommunity.com/workshop/browse/?appid=440900&searchtext={Uri.EscapeDataString(input)}&browsesort=textsearch&section=readytouseitems");
            }
        }

        private void PerformBrowserSearch()
        {
            string query = TxtBrowserSearchQuery.Text.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                NavigateBrowserToUrl(ConanWorkshopHomeUrl);
                return;
            }

            if (Regex.IsMatch(query, @"^\d{6,12}$"))
            {
                NavigateBrowserToUrl($"https://steamcommunity.com/sharedfiles/filedetails/?id={query}");
            }
            else
            {
                NavigateBrowserToUrl($"https://steamcommunity.com/workshop/browse/?appid=440900&searchtext={Uri.EscapeDataString(query)}&browsesort=textsearch&section=readytouseitems");
            }
        }

        private void BtnBrowserBack_Click(object sender, RoutedEventArgs e)
        {
            if (WvModBrowser.CanGoBack) WvModBrowser.GoBack();
        }

        private void BtnBrowserForward_Click(object sender, RoutedEventArgs e)
        {
            if (WvModBrowser.CanGoForward) WvModBrowser.GoForward();
        }

        private void BtnBrowserRefresh_Click(object sender, RoutedEventArgs e)
        {
            WvModBrowser.Reload();
        }

        private void BtnBrowserHome_Click(object sender, RoutedEventArgs e)
        {
            NavigateBrowserToUrl(ConanWorkshopHomeUrl);
        }

        private void BtnBrowserGo_Click(object sender, RoutedEventArgs e)
        {
            NavigateToUserUrl();
        }

        private void TxtBrowserUrl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NavigateToUserUrl();
            }
        }

        private void BtnBrowserSearch_Click(object sender, RoutedEventArgs e)
        {
            PerformBrowserSearch();
        }

        private void TxtBrowserSearchQuery_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PerformBrowserSearch();
            }
        }

        private void BtnOpenModBrowser_Click(object sender, RoutedEventArgs e)
        {
            var mod = GetSelectedOrClickedMod(sender);
            OpenFullPageBrowser(mod?.Id);
        }

        private void MnuViewInModBrowser_Click(object sender, RoutedEventArgs e)
        {
            var mod = GetSelectedOrClickedMod(sender);
            if (mod == null || string.IsNullOrWhiteSpace(mod.Id))
            {
                MessageBox.Show("Please select a mod first.", "Mod Browser", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            OpenFullPageBrowser(mod.Id);
        }

        private void BtnAddBrowserModToServer_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_currentDetectedModId)) return;
            string modId = _currentDetectedModId.Trim();

            bool exists = _engine.Config.Mods.Any(m => m.Equals(modId, StringComparison.OrdinalIgnoreCase));
            if (!exists)
            {
                _engine.Config.Mods.Add(modId);
                _engine.SaveConfig();
                _engine.SyncIniSettings();
                _engine.GenerateModlistFile();
                _engine.Log($"[ModBrowser] Added mod {modId} to server mods.");
            }

            bool uiExists = LstMods.Items.OfType<ModDisplayItem>().Any(m => m.Id.Equals(modId, StringComparison.OrdinalIgnoreCase))
                || LstMods.Items.OfType<string>().Any(s => s.Equals(modId, StringComparison.OrdinalIgnoreCase));

            if (!uiExists)
            {
                var cached = SteamWorkshopHelper.GetCachedMod(modId);
                var newItem = new ModDisplayItem
                {
                    Id = modId,
                    Title = cached != null && !string.IsNullOrWhiteSpace(cached.Title) ? cached.Title : $"Loading Mod #{modId}...",
                    PreviewUrl = cached?.PreviewUrl ?? "",
                    IsDownloaded = _engine.IsModDownloaded(modId)
                };
                LstMods.Items.Add(newItem);

                _ = Task.Run(async () =>
                {
                    var detail = await SteamWorkshopHelper.GetModDetailsAsync(modId);
                    Dispatcher.Invoke(() =>
                    {
                        if (detail != null && !string.IsNullOrWhiteSpace(detail.Title))
                        {
                            newItem.Title = detail.Title;
                            newItem.PreviewUrl = detail.PreviewUrl;
                        }
                        else if (newItem.Title.StartsWith("Loading"))
                        {
                            newItem.Title = $"Mod #{modId}";
                        }
                    });
                });
            }

            bool isDownloaded = _engine.IsModDownloaded(modId);
            if (isDownloaded)
            {
                TxtBrowserModStatus.Text = "✅ Installed & Ready on Disk";
            }
            else
            {
                TxtBrowserModStatus.Text = "📥 Added! Pre-downloading in background...";
                _ = Task.Run(async () => await _engine.PreDownloadModAsync(modId));
            }
            BtnAddBrowserModToServer.Visibility = Visibility.Collapsed;
            BtnRemoveBrowserModFromServer.Visibility = Visibility.Visible;
        }

        private void BtnRemoveBrowserModFromServer_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_currentDetectedModId)) return;
            string modId = _currentDetectedModId.Trim();

            _engine.Config.Mods.RemoveAll(x => x.Equals(modId, StringComparison.OrdinalIgnoreCase));
            _engine.SaveConfig();
            _engine.SyncIniSettings();
            _engine.GenerateModlistFile();
            _engine.Log($"[ModBrowser] Removed mod {modId} from server mods.");

            var toRemove = LstMods.Items.OfType<ModDisplayItem>().FirstOrDefault(m => m.Id.Equals(modId, StringComparison.OrdinalIgnoreCase));
            if (toRemove != null)
            {
                LstMods.Items.Remove(toRemove);
            }
            else
            {
                var strRemove = LstMods.Items.OfType<string>().FirstOrDefault(s => s.Equals(modId, StringComparison.OrdinalIgnoreCase));
                if (strRemove != null) LstMods.Items.Remove(strRemove);
            }

            TxtBrowserModStatus.Text = "Removed from server";
            BtnAddBrowserModToServer.Visibility = Visibility.Visible;
            BtnRemoveBrowserModFromServer.Visibility = Visibility.Collapsed;
        }

        private void BtnDownloadWebView2_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://go.microsoft.com/fwlink/p/?LinkId=2124703",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void BtnOpenWorkshopExternal_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = ConanWorkshopHomeUrl,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void MnuCopyWorkshopUrl_Click(object sender, RoutedEventArgs e)
        {
            var mod = GetSelectedOrClickedMod(sender);
            if (mod == null || string.IsNullOrWhiteSpace(mod.Id)) return;
            string url = $"https://steamcommunity.com/sharedfiles/filedetails/?id={mod.Id.Trim()}";
            try
            {
                Clipboard.SetText(url);
                MessageBox.Show($"Copied Workshop link to Clipboard:\n\n{url}", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _engine.Log($"Clipboard copy note: {ex.Message}");
            }
        }

        private void MnuCopyModId_Click(object sender, RoutedEventArgs e)
        {
            var mod = GetSelectedOrClickedMod(sender);
            if (mod == null || string.IsNullOrWhiteSpace(mod.Id)) return;
            try
            {
                Clipboard.SetText(mod.Id.Trim());
                MessageBox.Show($"Copied Mod ID '{mod.Id.Trim()}' to Clipboard.", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _engine.Log($"Clipboard copy note: {ex.Message}");
            }
        }

        private void BtnRemoveMod_Click(object sender, RoutedEventArgs e)
        {
            var mod = GetSelectedOrClickedMod(sender);
            if (mod != null && LstMods.Items.Contains(mod))
            {
                LstMods.Items.Remove(mod);
                return;
            }

            int idx = LstMods.SelectedIndex;
            if (idx >= 0)
            {
                LstMods.Items.RemoveAt(idx);
            }
        }

        private async void BtnSaveEngineIni_Click(object sender, RoutedEventArgs e)
        {
            if (IsRemoteMode)
            {
                await SaveRemoteIniFileAsync("Engine.ini", TxtEngineIni.Text);
                return;
            }

            _engine.SaveIniText(_engine.EngineIni, TxtEngineIni.Text);
            _engine.AutoDetectAndImportIniSettings();
            LoadUiFromConfig();
            LoadIniFilesToTabs();
            MessageBox.Show("Engine.ini saved successfully and re-synced to Manager controls.", "INI Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnSaveServerSettingsIni_Click(object sender, RoutedEventArgs e)
        {
            if (IsRemoteMode)
            {
                await SaveRemoteIniFileAsync("ServerSettings.ini", TxtServerSettingsIni.Text);
                return;
            }

            _engine.SaveIniText(_engine.ServerSettingsIni, TxtServerSettingsIni.Text);
            _engine.AutoDetectAndImportIniSettings();
            LoadUiFromConfig();
            LoadIniFilesToTabs();
            MessageBox.Show("ServerSettings.ini saved successfully and re-synced to Manager controls.", "INI Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnSaveGameIni_Click(object sender, RoutedEventArgs e)
        {
            if (IsRemoteMode)
            {
                await SaveRemoteIniFileAsync("Game.ini", TxtGameIni.Text);
                return;
            }

            _engine.SaveIniText(_engine.GameIni, TxtGameIni.Text);
            LoadIniFilesToTabs();
            MessageBox.Show("Game.ini saved successfully.", "INI Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnSendRcon_Click(object sender, RoutedEventArgs e)
        {
            string cmd = TxtRconCmd.Text.Trim();
            if (string.IsNullOrEmpty(cmd)) return;

            if (IsRemoteMode)
            {
                string baseUrl = TxtRemoteUrl.Text.Trim().TrimEnd('/');
                try
                {
                    string json = JsonSerializer.Serialize(new { command = cmd });
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var res = await _httpClient.PostAsync($"{baseUrl}/api/control/rcon", content);
                    string body = await res.Content.ReadAsStringAsync();
                    if (TxtServerLog != null) TxtServerLog.AppendText($"[Remote RCON > {cmd}]: {body}\n");
                    TxtRconCmd.Clear();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Remote RCON Failed:\n{ex.Message}", "RCON Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return;
            }

            if (_engine.ServerStatus != "RUNNING")
            {
                MessageBox.Show("Server must be RUNNING to execute RCON commands.", "RCON Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var rcon = new ValveRconClient("127.0.0.1", _engine.Config.RconPort, _engine.Config.RconPassword);
                await rcon.ConnectAsync();
                string response = await rcon.ExecuteAsync(cmd);
                _engine.Log($"[RCON > {cmd}]: {response}");
                TxtRconCmd.Clear();
            }
            catch (Exception ex)
            {
                _engine.Log($"[RCON Error]: {ex.Message}");
            }
        }

        private void ShowAppUpdateBanner(AppUpdateInfo info)
        {
            Dispatcher.Invoke(() =>
            {
                if (PnlAppUpdateBanner == null || TxtAppUpdateNotice == null) return;
                TxtAppUpdateNotice.Text = $"🚀 New application version available: {info.LatestVersion} (Current: v{ServerEngine.CurrentAppVersion})";
                PnlAppUpdateBanner.Visibility = Visibility.Visible;
            });
        }

        private async void BtnApplyAppUpdate_Click(object sender, RoutedEventArgs e)
        {
            var updateInfo = _engine.LatestAppUpdate;
            if (updateInfo == null)
            {
                MessageBox.Show("No update information currently cached. Please check for updates again.", "Update", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrEmpty(updateInfo.DownloadUrl))
            {
                MessageBox.Show($"New version {updateInfo.TagName} was found, but no release ZIP asset was attached to the GitHub release.\n\nPlease visit https://github.com/Nakrom75/ConanEnhancedServerManager/releases to download it manually.", "Asset Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var res = MessageBox.Show(
                $"Update to {updateInfo.TagName}?\n\nThe application will download the new release, stage the update, and automatically restart.\n\nDo you want to proceed?",
                "Confirm Application Update",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res != MessageBoxResult.Yes) return;

            if (BtnApplyAppUpdate != null)
            {
                BtnApplyAppUpdate.IsEnabled = false;
                BtnApplyAppUpdate.Content = "⏳ Updating...";
            }

            try
            {
                string result = await _engine.DownloadAndApplyAppUpdateAsync(updateInfo);
                _engine.Log($"[Updater]: {result}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Update error: {ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                if (BtnApplyAppUpdate != null)
                {
                    BtnApplyAppUpdate.IsEnabled = true;
                    BtnApplyAppUpdate.Content = "📥 Download & Update Now";
                }
            }
        }

        private void BtnDismissUpdateBanner_Click(object sender, RoutedEventArgs e)
        {
            if (PnlAppUpdateBanner != null)
                PnlAppUpdateBanner.Visibility = Visibility.Collapsed;
        }

        private async void BtnCheckAppUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (BtnCheckAppUpdate != null)
            {
                BtnCheckAppUpdate.IsEnabled = false;
                BtnCheckAppUpdate.Content = "⏳ Checking...";
            }

            try
            {
                var info = await _engine.CheckForAppUpdateAsync();
                if (info != null && info.UpdateAvailable)
                {
                    ShowAppUpdateBanner(info);
                    MessageBox.Show($"A new version is available: {info.LatestVersion}!\nCurrent version: v{ServerEngine.CurrentAppVersion}\n\nClick 'Download & Update Now' in the top notification banner to apply it.", "Update Available", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"You are running the latest version (v{ServerEngine.CurrentAppVersion}).", "Up to Date", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not check for updates:\n{ex.Message}", "Check Update Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                if (BtnCheckAppUpdate != null)
                {
                    BtnCheckAppUpdate.IsEnabled = true;
                    BtnCheckAppUpdate.Content = "🔄 Check App Updates";
                }
            }
        }

        private void UpdatePlayersListUi(List<SteamPlayerInfo>? players, int steamCount = 0, int steamMax = 0)
        {
            if (!_isInitialized) return;

            Dispatcher.Invoke(() =>
            {
                var list = players != null ? new List<SteamPlayerInfo>(players) : new List<SteamPlayerInfo>();

                if (list.Count == 0 && steamCount > 0)
                {
                    for (int i = 0; i < steamCount; i++)
                    {
                        list.Add(new SteamPlayerInfo
                        {
                            Index = (byte)i,
                            Name = $"Player #{i + 1}",
                            Score = 0,
                            DurationSeconds = 0
                        });
                    }
                }

                if (LstConnectedPlayers != null)
                {
                    LstConnectedPlayers.ItemsSource = null;
                    LstConnectedPlayers.ItemsSource = list;
                }

                if (PnlNoPlayers != null)
                {
                    PnlNoPlayers.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                }

                if (TxtPlayersCountBadge != null)
                {
                    int maxCap = steamMax > 0 ? steamMax : (_engine?.Config?.MaxPlayers ?? 40);
                    int curCount = list.Count > 0 ? list.Count : steamCount;
                    TxtPlayersCountBadge.Text = $"{curCount} / {maxCap}";
                }
            });
        }

        private async void BtnRefreshPlayers_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (IsRemoteMode)
                {
                    await PollRemoteServerAsync();
                }
                else
                {
                    if (_engine.ServerStatus == "RUNNING")
                    {
                        string host = (_engine.Config.UseMultihome && !string.IsNullOrWhiteSpace(_engine.Config.MultihomeIp))
                            ? _engine.Config.MultihomeIp.Trim()
                            : "127.0.0.1";
                        var a2sPlayers = await SteamQueryHelper.QueryA2sPlayersAsync(host, _engine.Config.QueryPort, 1500);
                        if (a2sPlayers != null && a2sPlayers.Count > 0)
                        {
                            UpdatePlayersListUi(a2sPlayers, a2sPlayers.Count, _engine.Config.MaxPlayers);
                        }
                        else
                        {
                            var rconPlayers = await _engine.QueryRconPlayersAsync();
                            UpdatePlayersListUi(rconPlayers, _engine.SteamStatus.Players, _engine.Config.MaxPlayers);
                        }
                    }
                    else
                    {
                        UpdatePlayersListUi(new List<SteamPlayerInfo>(), 0, _engine.Config.MaxPlayers);
                    }
                }
            }
            catch (Exception ex)
            {
                _engine.Log($"[Players Refresh Error]: {ex.Message}");
            }
        }

        private async void BtnKickSelectedPlayer_Click(object sender, RoutedEventArgs e)
        {
            if (LstConnectedPlayers?.SelectedItem is not SteamPlayerInfo player || string.IsNullOrWhiteSpace(player.Name))
            {
                MessageBox.Show("Please select an active player from the list to kick.", "Kick Player", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show($"Are you sure you want to kick '{player.Name}' from the server?", "Confirm Kick", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                if (IsRemoteMode)
                {
                    string baseUrl = TxtRemoteUrl.Text.Trim().TrimEnd('/');
                    if (!baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        baseUrl = "http://" + baseUrl;
                    }
                    using var content = new StringContent(JsonSerializer.Serialize(new { command = $"kick \"{player.Name}\"" }), Encoding.UTF8, "application/json");
                    var res = await _httpClient.PostAsync($"{baseUrl}/api/control/rcon", content);
                    if (res.IsSuccessStatusCode)
                    {
                        MessageBox.Show($"Kick command sent for '{player.Name}'.", "Player Kicked", MessageBoxButton.OK, MessageBoxImage.Information);
                        await PollRemoteServerAsync();
                    }
                    else
                    {
                        MessageBox.Show($"Failed to kick player: {res.StatusCode}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    bool kicked = await _engine.KickPlayerAsync(player.Name);
                    if (kicked)
                    {
                        MessageBox.Show($"Player '{player.Name}' was kicked successfully.", "Player Kicked", MessageBoxButton.OK, MessageBoxImage.Information);
                        BtnRefreshPlayers_Click(sender, e);
                    }
                    else
                    {
                        MessageBox.Show($"Could not kick '{player.Name}'. Make sure RCON is enabled and server is running.", "Kick Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error executing kick: {ex.Message}", "Kick Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class ModDisplayItem : System.ComponentModel.INotifyPropertyChanged
    {
        private string _id = "";
        private string _title = "";
        private string _previewUrl = "";
        private bool _isDownloaded = false;

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(Id)); OnPropertyChanged(nameof(Subtitle)); }
        }

        public string Title
        {
            get => string.IsNullOrWhiteSpace(_title) ? $"Mod #{Id}" : _title;
            set { _title = value; OnPropertyChanged(nameof(Title)); }
        }

        public string PreviewUrl
        {
            get => _previewUrl;
            set { _previewUrl = value; OnPropertyChanged(nameof(PreviewUrl)); }
        }

        public bool IsDownloaded
        {
            get => _isDownloaded;
            set { _isDownloaded = value; OnPropertyChanged(nameof(IsDownloaded)); OnPropertyChanged(nameof(Subtitle)); }
        }

        public string Subtitle => $"ID: {Id} • {(IsDownloaded ? "✅ Ready on Disk" : "⏳ Pending Download")}";

        public override string ToString() => $"{Title} ({Id})";

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(prop));
    }
}