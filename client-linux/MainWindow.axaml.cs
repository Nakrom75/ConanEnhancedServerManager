using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace ConanServerManager.Linux
{
    public partial class MainWindow : Window
    {
        private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        private readonly DispatcherTimer _pollTimer = new DispatcherTimer();
        private bool _isConnected = false;
        private string _serverBaseUrl = "http://127.0.0.1:8088";
        private int _lastLogCount = 0;
        private readonly ObservableCollection<ModItemModel> _mods = new ObservableCollection<ModItemModel>();
        private readonly ObservableCollection<DiscoveredServer> _discoveredServers = new ObservableCollection<DiscoveredServer>();

        public MainWindow()
        {
            InitializeComponent();

            LstMods.ItemsSource = _mods;
            CmbDiscoveredServers.ItemsSource = _discoveredServers;

            _pollTimer.Interval = TimeSpan.FromSeconds(1.5);
            _pollTimer.Tick += OnPollTick;

            // Wire UI Events
            BtnConnect.Click += OnConnectClick;
            BtnScanLan.Click += OnScanLanClick;
            CmbDiscoveredServers.SelectionChanged += OnServerSelectionChanged;

            BtnStartServer.Click += async (s, e) => await ExecuteServerControlAsync("start");
            BtnStopServer.Click += async (s, e) => await ExecuteServerControlAsync("stop");
            BtnRestartServer.Click += async (s, e) => await ExecuteServerControlAsync("restart");
            BtnBackupServer.Click += async (s, e) => await ExecuteServerControlAsync("backup");
            BtnRefreshStatus.Click += async (s, e) => await RefreshStatusAsync();

            BtnClearLogs.Click += (s, e) => TxtServerLogs.Text = "";
            BtnCopyLogs.Click += async (s, e) => await CopyLogsToClipboardAsync();

            BtnSendRcon.Click += async (s, e) => await SendRconCommandAsync();
            TxtRconCommand.KeyDown += async (s, e) =>
            {
                if (e.Key == Avalonia.Input.Key.Enter)
                {
                    await SendRconCommandAsync();
                }
            };
            BtnRconSave.Click += async (s, e) => await ExecuteRconQuickAsync("save");
            BtnRconListPlayers.Click += async (s, e) => await ExecuteRconQuickAsync("listplayers");
            BtnRconBroadcast.Click += async (s, e) => await PromptAndExecuteBroadcastAsync();
            BtnRconKick.Click += async (s, e) => await PromptAndExecuteKickAsync();

            BtnRefreshMods.Click += async (s, e) => await RefreshModsAsync();
            BtnAddMod.Click += async (s, e) => await PromptAddModAsync();
            BtnPreDownloadMod.Click += async (s, e) => await PreDownloadSelectedModAsync();
            BtnRemoveMod.Click += async (s, e) => await RemoveSelectedModAsync();
            BtnOpenWorkshopBrowser.Click += (s, e) => OpenBrowser("https://steamcommunity.com/app/440900/workshop/");

            BtnFetchIni.Click += async (s, e) => await FetchIniFileAsync();
            BtnSaveIni.Click += async (s, e) => await SaveIniFileAsync();
            CmbIniFiles.SelectionChanged += async (s, e) => await FetchIniFileAsync();

            BtnReloadConfig.Click += async (s, e) => await FetchConfigAsync();
            BtnSaveConfig.Click += async (s, e) => await PushConfigAsync();

            Loaded += OnWindowLoaded;
        }

        private async void OnWindowLoaded(object? sender, RoutedEventArgs e)
        {
            LoadClientConfig();
            await DiscoverLanServersAsync();
        }

        #region Config & LAN Discovery

        private void LoadClientConfig()
        {
            try
            {
                string configPath = GetClientConfigPath();
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    var cfg = JsonSerializer.Deserialize<ClientConfig>(json);
                    if (cfg != null && !string.IsNullOrEmpty(cfg.LastServerUrl))
                    {
                        TxtServerUrl.Text = cfg.LastServerUrl;
                        if (cfg.AutoConnectOnLaunch)
                        {
                            Dispatcher.UIThread.Post(async () => await ConnectToServerAsync(cfg.LastServerUrl));
                        }
                    }
                }
            }
            catch { }
        }

        private void SaveClientConfig()
        {
            try
            {
                string configPath = GetClientConfigPath();
                string dir = Path.GetDirectoryName(configPath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var cfg = new ClientConfig
                {
                    LastServerUrl = TxtServerUrl.Text?.Trim() ?? "http://127.0.0.1:8088",
                    AutoConnectOnLaunch = true
                };

                File.WriteAllText(configPath, JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private static string GetClientConfigPath()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".config", "conanservermanager", "client_config.json");
        }

        private async Task DiscoverLanServersAsync()
        {
            TxtFooterStatus.Text = "Scanning local network for Conan Server Managers...";
            try
            {
                var servers = await DiscoveryHelper.DiscoverServersAsync(1500);
                _discoveredServers.Clear();
                foreach (var s in servers)
                {
                    _discoveredServers.Add(s);
                }

                if (_discoveredServers.Count > 0)
                {
                    TxtFooterStatus.Text = $"Discovered {_discoveredServers.Count} server(s) on network.";
                }
                else
                {
                    TxtFooterStatus.Text = "No LAN servers responded to UDP broadcast. You can enter IP manually.";
                }
            }
            catch (Exception ex)
            {
                TxtFooterStatus.Text = $"Discovery notice: {ex.Message}";
            }
        }

        private async void OnScanLanClick(object? sender, RoutedEventArgs e)
        {
            await DiscoverLanServersAsync();
        }

        private void OnServerSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (CmbDiscoveredServers.SelectedItem is DiscoveredServer s && !string.IsNullOrEmpty(s.Url))
            {
                TxtServerUrl.Text = s.Url;
            }
        }

        #endregion

        #region Connection Management

        private async void OnConnectClick(object? sender, RoutedEventArgs e)
        {
            if (_isConnected)
            {
                Disconnect();
            }
            else
            {
                string url = TxtServerUrl.Text?.Trim() ?? "http://127.0.0.1:8088";
                await ConnectToServerAsync(url);
            }
        }

        private async Task ConnectToServerAsync(string url)
        {
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "http://" + url;
            }

            _serverBaseUrl = url.TrimEnd('/');
            TxtFooterStatus.Text = $"Connecting to {_serverBaseUrl}...";

            try
            {
                string statusJson = await _httpClient.GetStringAsync($"{_serverBaseUrl}/api/status");
                using var doc = JsonDocument.Parse(statusJson);

                _isConnected = true;
                BtnConnect.Content = "🔌 Disconnect";
                BtnConnect.Classes.Clear();
                BtnConnect.Classes.Add("danger");

                LedConnection.Background = new SolidColorBrush(Color.Parse("#10B981"));
                TxtConnectionStatus.Text = "CONNECTED";
                TxtConnectionStatus.Foreground = new SolidColorBrush(Color.Parse("#10B981"));

                SaveClientConfig();

                _pollTimer.Start();
                await RefreshStatusAsync();
                await RefreshModsAsync();
                await FetchIniFileAsync();
                await FetchConfigAsync();

                TxtFooterStatus.Text = $"Connected to {_serverBaseUrl}. Live monitoring active.";
            }
            catch (Exception ex)
            {
                Disconnect();
                TxtFooterStatus.Text = $"Connection failed to {_serverBaseUrl}: {ex.Message}";
            }
        }

        private void Disconnect()
        {
            _isConnected = false;
            _pollTimer.Stop();

            BtnConnect.Content = "🔌 Connect";
            BtnConnect.Classes.Clear();
            BtnConnect.Classes.Add("primary");

            LedConnection.Background = new SolidColorBrush(Color.Parse("#EF4444"));
            TxtConnectionStatus.Text = "DISCONNECTED";
            TxtConnectionStatus.Foreground = new SolidColorBrush(Color.Parse("#EF4444"));

            TxtStatusText.Text = "DISCONNECTED";
            TxtStatusText.Foreground = new SolidColorBrush(Color.Parse("#EF4444"));
            LedStatusBadge.Background = new SolidColorBrush(Color.Parse("#EF4444"));

            TxtFooterStatus.Text = "Disconnected from remote server.";
        }

        #endregion

        #region Polling & Status

        private async void OnPollTick(object? sender, EventArgs e)
        {
            if (!_isConnected) return;
            await RefreshStatusAsync();
            await PollLogsAsync();
        }

        private async Task RefreshStatusAsync()
        {
            if (!_isConnected) return;
            try
            {
                string statusJson = await _httpClient.GetStringAsync($"{_serverBaseUrl}/api/status");
                var status = JsonSerializer.Deserialize<ServerStatusModel>(statusJson);
                if (status == null) return;

                // Status Badge & LED
                if (status.IsUpdating)
                {
                    TxtStatusText.Text = "UPDATING";
                    TxtStatusText.Foreground = new SolidColorBrush(Color.Parse("#F59E0B"));
                    LedStatusBadge.Background = new SolidColorBrush(Color.Parse("#F59E0B"));
                }
                else if (status.IsRunning)
                {
                    TxtStatusText.Text = "RUNNING";
                    TxtStatusText.Foreground = new SolidColorBrush(Color.Parse("#10B981"));
                    LedStatusBadge.Background = new SolidColorBrush(Color.Parse("#10B981"));
                }
                else
                {
                    TxtStatusText.Text = "STOPPED";
                    TxtStatusText.Foreground = new SolidColorBrush(Color.Parse("#EF4444"));
                    LedStatusBadge.Background = new SolidColorBrush(Color.Parse("#EF4444"));
                }

                // Uptime
                TimeSpan t = TimeSpan.FromSeconds(status.UptimeSeconds);
                TxtUptime.Text = $"Uptime: {(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";

                // Hardware
                TxtCpuRam.Text = $"CPU: {status.CpuPercent:F1}% | RAM: {status.RamUsedMb:F0} MB";
                ProgRam.Value = status.RamPercent;

                // Players
                TxtPlayerCount.Text = $"{status.PlayerCount} / {status.MaxPlayers} Online";
                TxtServerNameDisplay.Text = status.ServerName;

                // Mods
                TxtModsSummary.Text = $"{status.ActiveModsCount} Installed Mods";
                if (status.IsPreDownloading)
                {
                    TxtPreDownloadStatus.Text = $"⏳ Pre-Downloading Mod {status.PreDownloadingModId}...";
                    TxtPreDownloadStatus.Foreground = new SolidColorBrush(Color.Parse("#F59E0B"));
                }
                else
                {
                    TxtPreDownloadStatus.Text = "All Ready on Disk";
                    TxtPreDownloadStatus.Foreground = new SolidColorBrush(Color.Parse("#10B981"));
                }

                // Download Progress
                if (status.IsUpdating || status.IsPreDownloading || status.DownloadPercent > 0)
                {
                    PnlDownloadProgress.IsVisible = true;
                    ProgDownload.Value = status.DownloadPercent;
                    TxtDownloadProgress.Text = $"Downloading: {status.DownloadPercent}% ({status.DownloadSpeedMBs:F2} MB/s)";
                    TxtDownloadEta.Text = string.IsNullOrEmpty(status.DownloadEtaString) ? "" : $" | ETA: {status.DownloadEtaString}";
                }
                else
                {
                    PnlDownloadProgress.IsVisible = false;
                }
            }
            catch (Exception ex)
            {
                TxtFooterStatus.Text = $"Poll warning: {ex.Message}";
            }
        }

        private async Task PollLogsAsync()
        {
            if (!_isConnected) return;
            try
            {
                string logsJson = await _httpClient.GetStringAsync($"{_serverBaseUrl}/api/logs");
                using var doc = JsonDocument.Parse(logsJson);
                if (doc.RootElement.TryGetProperty("logs", out var logsArr))
                {
                    int count = logsArr.GetArrayLength();
                    if (count > _lastLogCount || (count < _lastLogCount && count > 0))
                    {
                        var sb = new StringBuilder();
                        foreach (var item in logsArr.EnumerateArray())
                        {
                            sb.AppendLine(item.GetString());
                        }
                        TxtServerLogs.Text = sb.ToString();
                        _lastLogCount = count;

                        if (ChkAutoScroll.IsChecked == true)
                        {
                            TxtServerLogs.CaretIndex = TxtServerLogs.Text.Length;
                        }
                    }
                }
            }
            catch { }
        }

        private async Task CopyLogsToClipboardAsync()
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.Clipboard != null && !string.IsNullOrEmpty(TxtServerLogs.Text))
                {
                    await topLevel.Clipboard.SetTextAsync(TxtServerLogs.Text);
                    TxtFooterStatus.Text = "Console logs copied to clipboard.";
                }
            }
            catch (Exception ex)
            {
                TxtFooterStatus.Text = $"Copy note: {ex.Message}";
            }
        }

        #endregion

        #region Server Controls

        private async Task ExecuteServerControlAsync(string action)
        {
            if (!_isConnected)
            {
                TxtFooterStatus.Text = "Cannot execute control: Not connected to remote server.";
                return;
            }

            TxtFooterStatus.Text = $"Sending command: {action.ToUpper()} to server...";
            try
            {
                var response = await _httpClient.PostAsync($"{_serverBaseUrl}/api/control/{action}", null);
                if (response.IsSuccessStatusCode)
                {
                    TxtFooterStatus.Text = $"Server command '{action.ToUpper()}' acknowledged by server.";
                    await RefreshStatusAsync();
                }
                else
                {
                    string err = await response.Content.ReadAsStringAsync();
                    TxtFooterStatus.Text = $"Server returned error for '{action}': {err}";
                }
            }
            catch (Exception ex)
            {
                TxtFooterStatus.Text = $"Failed to execute '{action}': {ex.Message}";
            }
        }

        #endregion

        #region RCON Console

        private async Task SendRconCommandAsync()
        {
            string cmd = TxtRconCommand.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(cmd)) return;

            TxtRconCommand.Text = "";
            await ExecuteRconQuickAsync(cmd);
        }

        private async Task ExecuteRconQuickAsync(string cmd)
        {
            if (!_isConnected)
            {
                TxtRconLogs.Text += $"\n[CLIENT] Error: Not connected to remote server.\n";
                return;
            }

            TxtRconLogs.Text += $"\n> {cmd}\n";
            try
            {
                var payload = new { command = cmd };
                string json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_serverBaseUrl}/api/control/rcon", content);
                string reply = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(reply);
                string output = doc.RootElement.TryGetProperty("response", out var rProp) ? rProp.GetString() ?? "" : reply;

                TxtRconLogs.Text += $"{output}\n";
                TxtRconLogs.CaretIndex = TxtRconLogs.Text.Length;
            }
            catch (Exception ex)
            {
                TxtRconLogs.Text += $"[ERROR] Failed to execute RCON command: {ex.Message}\n";
            }
        }

        private async Task PromptAndExecuteBroadcastAsync()
        {
            string msg = "Server announcement from Linux Administration Client";
            await ExecuteRconQuickAsync($"broadcast {msg}");
        }

        private async Task PromptAndExecuteKickAsync()
        {
            await ExecuteRconQuickAsync("listplayers");
        }

        #endregion

        #region Mods Management

        private async Task RefreshModsAsync()
        {
            if (!_isConnected) return;
            try
            {
                string json = await _httpClient.GetStringAsync($"{_serverBaseUrl}/api/mods");
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("mods", out var modsArr))
                {
                    _mods.Clear();
                    foreach (var m in modsArr.EnumerateArray())
                    {
                        string id = m.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                        string name = m.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? $"Mod {id}" : $"Mod {id}";
                        bool isDl = m.TryGetProperty("isDownloaded", out var dlProp) && dlProp.GetBoolean();
                        string pak = m.TryGetProperty("pakPath", out var pProp) ? pProp.GetString() ?? "" : "";

                        _mods.Add(new ModItemModel
                        {
                            Id = id,
                            Name = name,
                            IsDownloaded = isDl,
                            PakPath = pak
                        });
                    }
                    TxtFooterStatus.Text = $"Loaded {_mods.Count} mod(s) from server.";
                }
            }
            catch (Exception ex)
            {
                TxtFooterStatus.Text = $"Mod refresh notice: {ex.Message}";
            }
        }

        private async Task PromptAddModAsync()
        {
            // Prompt input dialog or add directly
            var dialog = new Window
            {
                Title = "Add Steam Workshop Mod",
                Width = 450,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = new SolidColorBrush(Color.Parse("#0F172A"))
            };

            var sp = new StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 10 };
            sp.Children.Add(new TextBlock { Text = "Enter Steam Workshop Mod ID (numeric):", Foreground = new SolidColorBrush(Color.Parse("#F8FAFC")), FontWeight = FontWeight.Bold });
            var txtMod = new TextBox { PlaceholderText = "e.g. 3722388367" };
            sp.Children.Add(txtMod);

            var btnRow = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
            var btnOk = new Button { Content = "Add & Pre-Download", Classes = { "success" }, Padding = new Avalonia.Thickness(12, 6) };
            var btnCancel = new Button { Content = "Cancel", Classes = { "secondary" }, Padding = new Avalonia.Thickness(12, 6) };
            btnRow.Children.Add(btnCancel);
            btnRow.Children.Add(btnOk);
            sp.Children.Add(btnRow);

            dialog.Content = sp;

            btnCancel.Click += (s, e) => dialog.Close();
            btnOk.Click += async (s, e) =>
            {
                string modId = txtMod.Text?.Trim() ?? "";
                if (!string.IsNullOrEmpty(modId) && modId.All(char.IsDigit))
                {
                    dialog.Close();
                    await AddModToServerAsync(modId);
                }
            };

            await dialog.ShowDialog(this);
        }

        private async Task AddModToServerAsync(string modId)
        {
            if (!_isConnected) return;
            try
            {
                var payload = new { modId = modId };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var res = await _httpClient.PostAsync($"{_serverBaseUrl}/api/mods/add", content);
                if (res.IsSuccessStatusCode)
                {
                    TxtFooterStatus.Text = $"Mod {modId} added to server. Pre-download triggered in background!";
                    await RefreshModsAsync();
                }
                else
                {
                    TxtFooterStatus.Text = $"Failed to add mod: {await res.Content.ReadAsStringAsync()}";
                }
            }
            catch (Exception ex)
            {
                TxtFooterStatus.Text = $"Error adding mod: {ex.Message}";
            }
        }

        private async Task PreDownloadSelectedModAsync()
        {
            if (LstMods.SelectedItem is not ModItemModel mod)
            {
                TxtFooterStatus.Text = "Please select a mod in the list first.";
                return;
            }

            try
            {
                var payload = new { modId = mod.Id };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var res = await _httpClient.PostAsync($"{_serverBaseUrl}/api/mods/predownload", content);
                if (res.IsSuccessStatusCode)
                {
                    TxtFooterStatus.Text = $"Pre-download / validation triggered for Mod {mod.Id}!";
                    await RefreshStatusAsync();
                }
                else
                {
                    TxtFooterStatus.Text = $"Pre-download failed: {await res.Content.ReadAsStringAsync()}";
                }
            }
            catch (Exception ex)
            {
                TxtFooterStatus.Text = $"Pre-download notice: {ex.Message}";
            }
        }

        private async Task RemoveSelectedModAsync()
        {
            if (LstMods.SelectedItem is not ModItemModel mod)
            {
                TxtFooterStatus.Text = "Please select a mod in the list first.";
                return;
            }

            try
            {
                var payload = new { modId = mod.Id };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var res = await _httpClient.PostAsync($"{_serverBaseUrl}/api/mods/remove", content);
                if (res.IsSuccessStatusCode)
                {
                    TxtFooterStatus.Text = $"Mod {mod.Id} removed from server configuration.";
                    await RefreshModsAsync();
                }
            }
            catch (Exception ex)
            {
                TxtFooterStatus.Text = $"Error removing mod: {ex.Message}";
            }
        }

        #endregion

        #region INI Files

        private async Task FetchIniFileAsync()
        {
            if (!_isConnected) return;
            string fileName = "ServerSettings.ini";
            if (CmbIniFiles.SelectedItem is ComboBoxItem item && item.Content != null)
            {
                fileName = item.Content.ToString()!;
            }

            try
            {
                string text = await _httpClient.GetStringAsync($"{_serverBaseUrl}/api/ini?file={fileName}");
                TxtIniContent.Text = text;
                TxtFooterStatus.Text = $"Fetched {fileName} from server ({text.Length} characters).";
            }
            catch (Exception ex)
            {
                TxtFooterStatus.Text = $"Failed to fetch INI: {ex.Message}";
            }
        }

        private async Task SaveIniFileAsync()
        {
            if (!_isConnected) return;
            string fileName = "ServerSettings.ini";
            if (CmbIniFiles.SelectedItem is ComboBoxItem item && item.Content != null)
            {
                fileName = item.Content.ToString()!;
            }

            try
            {
                var payload = new { file = fileName, content = TxtIniContent.Text ?? "" };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var res = await _httpClient.PostAsync($"{_serverBaseUrl}/api/ini", content);
                if (res.IsSuccessStatusCode)
                {
                    TxtFooterStatus.Text = $"Saved {fileName} to remote server successfully!";
                }
                else
                {
                    TxtFooterStatus.Text = $"Failed to save INI: {await res.Content.ReadAsStringAsync()}";
                }
            }
            catch (Exception ex)
            {
                TxtFooterStatus.Text = $"Error saving INI: {ex.Message}";
            }
        }

        #endregion

        #region Remote Configuration

        private async Task FetchConfigAsync()
        {
            if (!_isConnected) return;
            try
            {
                string json = await _httpClient.GetStringAsync($"{_serverBaseUrl}/api/config");
                var cfg = JsonSerializer.Deserialize<ServerConfigModel>(json);
                if (cfg != null)
                {
                    TxtConfigServerName.Text = cfg.ServerName;
                    TxtConfigServerPassword.Text = cfg.ServerPassword;
                    TxtConfigAdminPassword.Text = cfg.AdminPassword;
                    TxtConfigGamePort.Text = cfg.GamePort.ToString();
                    TxtConfigRawPort.Text = cfg.RawPort.ToString();
                    TxtConfigQueryPort.Text = cfg.QueryPort.ToString();
                    TxtConfigRconPort.Text = cfg.RconPort.ToString();
                    TxtFooterStatus.Text = "Remote configuration loaded into settings form.";
                }
            }
            catch (Exception ex)
            {
                TxtFooterStatus.Text = $"Config fetch notice: {ex.Message}";
            }
        }

        private async Task PushConfigAsync()
        {
            if (!_isConnected) return;
            try
            {
                var cfg = new ServerConfigModel
                {
                    ServerName = TxtConfigServerName.Text ?? "",
                    ServerPassword = TxtConfigServerPassword.Text ?? "",
                    AdminPassword = TxtConfigAdminPassword.Text ?? "",
                    GamePort = int.TryParse(TxtConfigGamePort.Text, out int gp) ? gp : 7777,
                    RawPort = int.TryParse(TxtConfigRawPort.Text, out int rp) ? rp : 7778,
                    QueryPort = int.TryParse(TxtConfigQueryPort.Text, out int qp) ? qp : 27015,
                    RconPort = int.TryParse(TxtConfigRconPort.Text, out int rcp) ? rcp : 25575
                };

                var content = new StringContent(JsonSerializer.Serialize(cfg), Encoding.UTF8, "application/json");
                var res = await _httpClient.PostAsync($"{_serverBaseUrl}/api/config", content);
                if (res.IsSuccessStatusCode)
                {
                    TxtFooterStatus.Text = "Pushed configuration updates to remote server!";
                    await RefreshStatusAsync();
                }
                else
                {
                    TxtFooterStatus.Text = $"Config update rejected: {await res.Content.ReadAsStringAsync()}";
                }
            }
            catch (Exception ex)
            {
                TxtFooterStatus.Text = $"Error pushing config: {ex.Message}";
            }
        }

        #endregion

        #region Cross-Platform Helpers

        public static void OpenBrowser(string url)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
            }
            catch { }
        }

        #endregion
    }
}
