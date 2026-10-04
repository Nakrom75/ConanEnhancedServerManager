using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ConanServerManager
{
    public partial class MainWindow : Window
    {
        private readonly ServerEngine _engine;
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly DispatcherTimer _remoteTimer = new DispatcherTimer();

        public bool IsRemoteMode => RadRemoteMode != null && RadRemoteMode.IsChecked == true;

        public MainWindow()
        {
            _engine = new ServerEngine();
            InitializeComponent();

            _engine.OnLog += LogToLauncherConsole;
            _engine.OnSteamCmdLog += LogToSteamCmdConsole;
            _engine.OnErrorLog += LogToErrorConsole;
            _engine.OnStatusChanged += UpdateStatusUi;
            _engine.OnDownloadProgress += UpdateDownloadProgressUi;
            _engine.OnAppUpdateDiscovered += ShowAppUpdateBanner;

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
                _engine.Log($"[System] Conan Enhanced Server Manager v{ServerEngine.CurrentAppVersion} initialized successfully.");
                LoadUiFromConfig();
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
            if (TxtServerPathInfo == null) return; // Guard against XAML initialization ordering

            if (IsRemoteMode)
            {
                if (PnlRemoteStatusIndicator != null) PnlRemoteStatusIndicator.Visibility = Visibility.Visible;
                TxtServerPathInfo.Text = $"Mode: Remote Client | Target: {TxtRemoteUrl?.Text}";
                _remoteTimer.Start();
                _ = PollRemoteServerAsync();
            }
            else
            {
                _remoteTimer.Stop();
                if (PnlRemoteStatusIndicator != null) PnlRemoteStatusIndicator.Visibility = Visibility.Collapsed;
                int webPort = _engine?.Config?.WebPagePort ?? 8088;
                TxtServerPathInfo.Text = $"Mode: Local Server Host | Embedded Web Console: http://0.0.0.0:{webPort} (http://localhost:{webPort})";
            }
        }

        private async void BtnConnectRemote_Click(object sender, RoutedEventArgs e)
        {
            if (RadRemoteMode != null) RadRemoteMode.IsChecked = true;
            _engine.Log($"[Remote Client] Connecting to remote manager at {TxtRemoteUrl.Text}...");
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

                    UpdateStatusUi(status);
                    if (TxtServerPathInfo != null)
                        TxtServerPathInfo.Text = $"Remote Host: {srvName} ({status}) | Uptime: {uptime} | URL: {baseUrl}";

                    SetRemoteConnectionStatus(true, $"CONNECTED to {srvName} ({status})");
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
            if (TxtServerPathInfo != null)
                TxtServerPathInfo.Text = $"Mode: Local Server Host | Web API: http://localhost:{_engine.Config.WebPagePort}";

            TxtServerName.Text = _engine.Config.ServerName;
            TxtServerPass.Text = _engine.Config.ServerPassword;
            TxtAdminPass.Text = _engine.Config.AdminPassword;
            TxtRconPass.Text = _engine.Config.RconPassword;

            TxtGamePort.Text = _engine.Config.GamePort.ToString();
            TxtRawPort.Text = _engine.Config.RawUdpPort.ToString();
            TxtQueryPort.Text = _engine.Config.QueryPort.ToString();
            TxtRconPort.Text = _engine.Config.RconPort.ToString();
            TxtWebPort.Text = _engine.Config.WebPagePort.ToString();

            TxtMaxPlayers.Text = _engine.Config.MaxPlayers.ToString();
            TxtMaxTickRate.Text = _engine.Config.MaxTickRate.ToString();

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
                TxtMacAddress.Text = adapters[0].MacAddress;
            }
            TxtExternalIp.Text = string.IsNullOrEmpty(_engine.Config.ExternalIp) ? "127.0.0.1" : _engine.Config.ExternalIp;

            ChkUseMultihome.IsChecked = _engine.Config.UseMultihome;
            TxtMultihomeIp.Text = _engine.Config.MultihomeIp;

            ChkValidateFiles.IsChecked = _engine.Config.ValidateFiles;
            ChkStartIfNotRunning.IsChecked = _engine.Config.StartServerIfNotRunning;
            ChkAutoStartOnAppLaunch.IsChecked = _engine.Config.AutoStartOnAppLaunch;
            ChkZombieWatch.IsChecked = _engine.Config.ZombieCheckEnabled;
            ChkAutoCheckAppUpdates.IsChecked = _engine.Config.AutoCheckAppUpdates;
            ChkAutoInstallAppUpdates.IsChecked = _engine.Config.AutoInstallAppUpdates;

            ChkRconEnable.IsChecked = _engine.Config.RconEnabled;
            ChkBattlEye.IsChecked = _engine.Config.EnableBattlEye;
            ChkVAC.IsChecked = _engine.Config.EnableVAC;

            ChkDailyRestart.IsChecked = _engine.Config.EnableDailyRestart;
            TxtDailyRestartTime.Text = _engine.Config.DailyRestartTime;
            TxtMinUptime.Text = _engine.Config.MinimumUptime;
            TxtRestartsPerDay.Text = _engine.Config.RestartsPerDay.ToString();

            TxtWarn1Time.Text = _engine.Config.FirstWarningTime;
            TxtWarn1Msg.Text = _engine.Config.FirstWarningMsg;
            TxtWarn2Time.Text = _engine.Config.SecondWarningTime;
            TxtWarn2Msg.Text = _engine.Config.SecondWarningMsg;
            TxtWarn3Time.Text = _engine.Config.ThirdWarningTime;
            TxtWarn3Msg.Text = _engine.Config.ThirdWarningMsg;
            ChkFastRestartZeroPlayers.IsChecked = _engine.Config.FastRestartZeroPlayers;

            ChkShutdownBackup.IsChecked = _engine.Config.OnShutdownBackup;
            TxtBackupDays.Text = _engine.Config.BackupLimitDays.ToString();

            ChkDiscordEnable.IsChecked = _engine.Config.DiscordEnabled;
            ChkDiscordTime.IsChecked = _engine.Config.DiscordIncludeTime;
            TxtDiscordWebhook.Text = _engine.Config.DiscordWebhookUrl;

            ChkUseAllCores.IsChecked = _engine.Config.UseAllAvailableCores;

            RefreshModListUi();
        }

        private void SaveConfigFromUi()
        {
            _engine.Config.ServerName = TxtServerName.Text;
            _engine.Config.ServerPassword = TxtServerPass.Text;
            _engine.Config.AdminPassword = TxtAdminPass.Text;
            _engine.Config.RconPassword = TxtRconPass.Text;

            if (int.TryParse(TxtGamePort.Text, out int gp)) _engine.Config.GamePort = gp;
            if (int.TryParse(TxtRawPort.Text, out int rp)) _engine.Config.RawUdpPort = rp;
            if (int.TryParse(TxtQueryPort.Text, out int qp)) _engine.Config.QueryPort = qp;
            if (int.TryParse(TxtRconPort.Text, out int rcp)) _engine.Config.RconPort = rcp;
            if (int.TryParse(TxtWebPort.Text, out int wp)) _engine.Config.WebPagePort = wp;

            if (int.TryParse(TxtMaxPlayers.Text, out int mp)) _engine.Config.MaxPlayers = mp;
            if (int.TryParse(TxtMaxTickRate.Text, out int mtr)) _engine.Config.MaxTickRate = mtr;

            _engine.Config.UseMultihome = ChkUseMultihome.IsChecked == true;
            _engine.Config.MultihomeIp = TxtMultihomeIp.Text;

            _engine.Config.ValidateFiles = ChkValidateFiles.IsChecked == true;
            _engine.Config.StartServerIfNotRunning = ChkStartIfNotRunning.IsChecked == true;
            _engine.Config.AutoStartOnAppLaunch = ChkAutoStartOnAppLaunch.IsChecked == true;
            _engine.Config.ZombieCheckEnabled = ChkZombieWatch.IsChecked == true;
            _engine.Config.AutoCheckAppUpdates = ChkAutoCheckAppUpdates.IsChecked == true;
            _engine.Config.AutoInstallAppUpdates = ChkAutoInstallAppUpdates.IsChecked == true;

            _engine.Config.RconEnabled = ChkRconEnable.IsChecked == true;
            _engine.Config.EnableBattlEye = ChkBattlEye.IsChecked == true;
            _engine.Config.EnableVAC = ChkVAC.IsChecked == true;

            _engine.Config.EnableDailyRestart = ChkDailyRestart.IsChecked == true;
            _engine.Config.DailyRestartTime = TxtDailyRestartTime.Text;
            _engine.Config.MinimumUptime = TxtMinUptime.Text;
            if (int.TryParse(TxtRestartsPerDay.Text, out int rpd)) _engine.Config.RestartsPerDay = rpd;

            _engine.Config.FirstWarningTime = TxtWarn1Time.Text;
            _engine.Config.FirstWarningMsg = TxtWarn1Msg.Text;
            _engine.Config.SecondWarningTime = TxtWarn2Time.Text;
            _engine.Config.SecondWarningMsg = TxtWarn2Msg.Text;
            _engine.Config.ThirdWarningTime = TxtWarn3Time.Text;
            _engine.Config.ThirdWarningMsg = TxtWarn3Msg.Text;
            _engine.Config.FastRestartZeroPlayers = ChkFastRestartZeroPlayers.IsChecked == true;

            _engine.Config.OnShutdownBackup = ChkShutdownBackup.IsChecked == true;
            if (int.TryParse(TxtBackupDays.Text, out int bd)) _engine.Config.BackupLimitDays = bd;

            _engine.Config.DiscordEnabled = ChkDiscordEnable.IsChecked == true;
            _engine.Config.DiscordIncludeTime = ChkDiscordTime.IsChecked == true;
            _engine.Config.DiscordWebhookUrl = TxtDiscordWebhook.Text;

            _engine.Config.UseAllAvailableCores = ChkUseAllCores.IsChecked == true;

            _engine.Config.Mods = LstMods.Items.Cast<string>().ToList();

            _engine.SaveConfig();
            _engine.Log($"Configuration saved cleanly to manager_config.json. Web API Port: {_engine.Config.WebPagePort}");
            LoadIniFilesToTabs();
        }

        private void CmbNetworkAdapter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
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
            LstMods.Items.Clear();
            foreach (var mod in _engine.Config.Mods)
            {
                LstMods.Items.Add(mod);
            }
        }

        private void LoadIniFilesToTabs()
        {
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

        private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
        {
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
            if (!string.IsNullOrEmpty(newId) && !LstMods.Items.Contains(newId))
            {
                LstMods.Items.Add(newId);
                TxtNewModId.Clear();
            }
        }

        private void BtnRemoveMod_Click(object sender, RoutedEventArgs e)
        {
            int idx = LstMods.SelectedIndex;
            if (idx >= 0)
            {
                LstMods.Items.RemoveAt(idx);
            }
        }

        private void BtnSaveEngineIni_Click(object sender, RoutedEventArgs e)
        {
            _engine.SaveIniText(_engine.EngineIni, TxtEngineIni.Text);
            _engine.AutoDetectAndImportIniSettings();
            LoadUiFromConfig();
            LoadIniFilesToTabs();
            MessageBox.Show("Engine.ini saved successfully and re-synced to Manager controls.", "INI Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnSaveServerSettingsIni_Click(object sender, RoutedEventArgs e)
        {
            _engine.SaveIniText(_engine.ServerSettingsIni, TxtServerSettingsIni.Text);
            _engine.AutoDetectAndImportIniSettings();
            LoadUiFromConfig();
            LoadIniFilesToTabs();
            MessageBox.Show("ServerSettings.ini saved successfully and re-synced to Manager controls.", "INI Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnSaveGameIni_Click(object sender, RoutedEventArgs e)
        {
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
    }
}