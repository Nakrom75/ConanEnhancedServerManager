using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace ConanServerManager.Linux
{
    public class ServerStatusModel
    {
        [JsonPropertyName("isRunning")]
        public bool IsRunning { get; set; }

        [JsonPropertyName("isUpdating")]
        public bool IsUpdating { get; set; }

        [JsonPropertyName("statusText")]
        public string StatusText { get; set; } = "STOPPED";

        [JsonPropertyName("uptimeSeconds")]
        public int UptimeSeconds { get; set; }

        [JsonPropertyName("playerCount")]
        public int PlayerCount { get; set; }

        [JsonPropertyName("maxPlayers")]
        public int MaxPlayers { get; set; } = 40;

        [JsonPropertyName("activeModsCount")]
        public int ActiveModsCount { get; set; }

        [JsonPropertyName("cpuPercent")]
        public double CpuPercent { get; set; }

        [JsonPropertyName("ramPercent")]
        public double RamPercent { get; set; }

        [JsonPropertyName("ramUsedMb")]
        public double RamUsedMb { get; set; }

        [JsonPropertyName("ramTotalMb")]
        public double RamTotalMb { get; set; }

        [JsonPropertyName("downloadPercent")]
        public int DownloadPercent { get; set; }

        [JsonPropertyName("downloadSpeedMBs")]
        public double DownloadSpeedMBs { get; set; }

        [JsonPropertyName("downloadEtaString")]
        public string DownloadEtaString { get; set; } = "";

        [JsonPropertyName("isPreDownloading")]
        public bool IsPreDownloading { get; set; }

        [JsonPropertyName("preDownloadingModId")]
        public string PreDownloadingModId { get; set; } = "";

        [JsonPropertyName("serverName")]
        public string ServerName { get; set; } = "Conan Dedicated Server";

        [JsonPropertyName("appVersion")]
        public string AppVersion { get; set; } = "1.3.0";
    }

    public class ModItemModel : INotifyPropertyChanged
    {
        private string _id = "";
        private string _name = "";
        private bool _isDownloaded;
        private string _pakPath = "";

        [JsonPropertyName("id")]
        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("name")]
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("isDownloaded")]
        public bool IsDownloaded
        {
            get => _isDownloaded;
            set { _isDownloaded = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusBadge)); }
        }

        [JsonPropertyName("pakPath")]
        public string PakPath
        {
            get => _pakPath;
            set { _pakPath = value; OnPropertyChanged(); }
        }

        public string StatusBadge => IsDownloaded ? "✅ Ready on Disk" : "⏳ Pending Download";
        public string StatusColor => IsDownloaded ? "#10B981" : "#F59E0B";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }

    public class ServerConfigModel
    {
        [JsonPropertyName("serverName")]
        public string ServerName { get; set; } = "";

        [JsonPropertyName("serverPassword")]
        public string ServerPassword { get; set; } = "";

        [JsonPropertyName("adminPassword")]
        public string AdminPassword { get; set; } = "";

        [JsonPropertyName("gamePort")]
        public int GamePort { get; set; } = 7777;

        [JsonPropertyName("rawPort")]
        public int RawPort { get; set; } = 7778;

        [JsonPropertyName("queryPort")]
        public int QueryPort { get; set; } = 27015;

        [JsonPropertyName("rconPort")]
        public int RconPort { get; set; } = 25575;

        [JsonPropertyName("maxPlayers")]
        public int MaxPlayers { get; set; } = 40;

        [JsonPropertyName("autoRestart")]
        public bool AutoRestart { get; set; } = false;

        [JsonPropertyName("mods")]
        public List<string> Mods { get; set; } = new();
    }

    public class ClientConfig
    {
        public string LastServerUrl { get; set; } = "http://127.0.0.1:8088";
        public List<string> RecentServers { get; set; } = new();
        public bool AutoConnectOnLaunch { get; set; } = true;
    }
}
