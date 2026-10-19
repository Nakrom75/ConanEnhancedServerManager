using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace ConanServerManager
{
    public class WorldPlayerMarker
    {
        public long CharId { get; set; }
        public string PlayerId { get; set; } = "";
        public string CharacterName { get; set; } = "";
        public int Level { get; set; } = 1;
        public long GuildId { get; set; }
        public string ClanName { get; set; } = "No Clan";
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public string GridSector { get; set; } = "G4";
        public bool IsOnline { get; set; }
        public string LastOnlineText { get; set; } = "";
        public string Rank { get; set; } = "Player";
        public long WalletCopper { get; set; }
        public string WalletFormatted => LiveMapAndAdminService.FormatCurrency(WalletCopper);
        public string TeleportCommand => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"TeleportPlayer {X:F0} {Y:F0} {Z:F0}");
        public string CoordinatesText => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{X:F0}, {Y:F0}, {Z:F0}");
    }

    public class WorldBaseMarker
    {
        public long BaseClusterId { get; set; }
        public long OwnerId { get; set; }
        public string OwnerName { get; set; } = "Unknown";
        public bool IsClanOwned { get; set; }
        public int PieceCount { get; set; }
        public int StructureCount { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public string GridSector { get; set; } = "G4";
        public string Severity => PieceCount >= 1500 ? "HIGH (Mega-Base)" : (PieceCount >= 400 ? "MEDIUM" : "LOW");
        public string TeleportCommand => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"TeleportPlayer {X:F0} {Y:F0} {Z:F0}");
        public string CoordinatesText => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{X:F0}, {Y:F0}, {Z:F0}");
    }

    public class ClanBloatSummary
    {
        public long OwnerId { get; set; }
        public string ClanOrPlayerName { get; set; } = "";
        public int MemberCount { get; set; }
        public int BaseClusterCount { get; set; }
        public int TotalStructureCount { get; set; }
        public int TotalPieceCount { get; set; }
        public string MainBaseGrid { get; set; } = "";
        public string MainBaseTeleport { get; set; } = "";
    }

    public class CeAdminWarp
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "Public";
        public string RequiredRank { get; set; } = "Player";
        public int CostCopper { get; set; } = 0;
        public int CooldownSeconds { get; set; } = 120;
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public string GridSector => LiveMapAndAdminService.WorldToGridSector(X, Y);
        public string TeleportCommand => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"TeleportPlayer {X:F0} {Y:F0} {Z:F0}");
    }

    public class CeAdminHome
    {
        public string PlayerName { get; set; } = "";
        public string HomeName { get; set; } = "home";
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public string GridSector => LiveMapAndAdminService.WorldToGridSector(X, Y);
        public string TeleportCommand => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"TeleportPlayer {X:F0} {Y:F0} {Z:F0}");
    }

    public class CeAdminKit
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string RequiredRank { get; set; } = "Player";
        public int CooldownMinutes { get; set; } = 1440;
        public int CostCopper { get; set; } = 0;
        public string ItemsSummary { get; set; } = "";
    }

    public class CeAdminPlayerProfile
    {
        public string CharacterName { get; set; } = "";
        public string Rank { get; set; } = "Player";
        public long WalletCopper { get; set; } = 100; // 1 Silver starter
    }

    public class CeAdminDataStore
    {
        public List<CeAdminWarp> Warps { get; set; } = new List<CeAdminWarp>();
        public List<CeAdminHome> Homes { get; set; } = new List<CeAdminHome>();
        public List<CeAdminKit> Kits { get; set; } = new List<CeAdminKit>();
        public Dictionary<string, CeAdminPlayerProfile> Profiles { get; set; } = new Dictionary<string, CeAdminPlayerProfile>(StringComparer.OrdinalIgnoreCase);
    }

    public class WorldMapSnapshot
    {
        public string ServerName { get; set; } = "";
        public string ServerStatus { get; set; } = "STOPPED";
        public string StartupMap { get; set; } = "";
        public string DatabasePath { get; set; } = "";
        public bool DatabaseFound { get; set; }
        public string LastUpdated { get; set; } = "";
        public int OnlinePlayerCount { get; set; }
        public int TotalCharacterCount { get; set; }
        public int TotalBaseCount { get; set; }
        public int TotalBuildingPieces { get; set; }
        public List<WorldPlayerMarker> Players { get; set; } = new List<WorldPlayerMarker>();
        public List<WorldBaseMarker> Bases { get; set; } = new List<WorldBaseMarker>();
        public List<ClanBloatSummary> Clans { get; set; } = new List<ClanBloatSummary>();
        public List<CeAdminWarp> Warps { get; set; } = new List<CeAdminWarp>();
        public List<CeAdminHome> Homes { get; set; } = new List<CeAdminHome>();
        public List<CeAdminKit> Kits { get; set; } = new List<CeAdminKit>();
    }

    public class LiveMapAndAdminService
    {
        private readonly ServerEngine _engine;
        private TcpListener? _mapListener;
        private bool _isListening;
        private int _boundPort = 8089;
        private readonly object _storeLock = new object();
        private CeAdminDataStore _store = new CeAdminDataStore();
        private WorldMapSnapshot? _cachedSnapshot;
        private DateTime _lastSnapshotTime = DateTime.MinValue;

        // Real-time player coordinate overrides fed by ModController log stream [CEAdmin_Pos]
        private readonly ConcurrentDictionary<string, (double X, double Y, double Z, DateTime Time)> _liveModPositions
            = new ConcurrentDictionary<string, (double X, double Y, double Z, DateTime Time)>(StringComparer.OrdinalIgnoreCase);

        // Conan Exiles Exiled Lands / Merged Enhanced World Coordinate Bounds (Unreal units, cm)
        public const double WorldMinX = -390000.0;
        public const double WorldMaxX = 450000.0;
        public const double WorldMinY = -360000.0;
        public const double WorldMaxY = 380000.0;

        public bool IsRunning => _isListening;
        public int BoundPort => _boundPort;

        public string StoreFilePath => Path.Combine(ServerEngine.AppWorkingDir, "ceadmin_store.json");

        public LiveMapAndAdminService(ServerEngine engine)
        {
            _engine = engine;
            LoadStore();
        }

        public static string FormatCurrency(long totalCopper)
        {
            long gold = totalCopper / 10000;
            long rem = totalCopper % 10000;
            long silver = rem / 100;
            long copper = rem % 100;
            if (gold > 0) return $"{gold}g {silver}s {copper}c";
            if (silver > 0) return $"{silver}s {copper}c";
            return $"{copper}c";
        }

        public static string WorldToGridSector(double x, double y)
        {
            // Conan Exiles standard map grid: Columns A..P (16 cols across X), Rows 1..16 (16 rows from bottom Y_max to top Y_min)
            double normX = Math.Clamp((x - WorldMinX) / (WorldMaxX - WorldMinX), 0.0, 0.999);
            double normY = Math.Clamp((y - WorldMinY) / (WorldMaxY - WorldMinY), 0.0, 0.999);

            int colIndex = (int)(normX * 16.0); // 0 = A .. 15 = P
            int rowNumber = 16 - (int)(normY * 16.0); // Bottom is Row 1, Top (North) is Row 16
            char colLetter = (char)('A' + Math.Clamp(colIndex, 0, 15));
            return $"{colLetter}{Math.Clamp(rowNumber, 1, 16)}";
        }

        public void LoadStore()
        {
            lock (_storeLock)
            {
                try
                {
                    if (File.Exists(StoreFilePath))
                    {
                        string json = File.ReadAllText(StoreFilePath);
                        var loaded = JsonSerializer.Deserialize<CeAdminDataStore>(json);
                        if (loaded != null)
                        {
                            _store = loaded;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _engine.Log($"[CEAdmin Store] Load warning: {ex.Message}");
                }

                if (_store.Warps == null || _store.Warps.Count == 0)
                {
                    _store.Warps = new List<CeAdminWarp>
                    {
                        new CeAdminWarp { Id = "hub", Name = "Welcome Hub (Sentinels)", Category = "Hubs", RequiredRank = "Player", CostCopper = 0, CooldownSeconds = 60, X = -25240, Y = 261150, Z = -20180 },
                        new CeAdminWarp { Id = "sepermeru", Name = "Sepermeru Trading City", Category = "Cities", RequiredRank = "Player", CostCopper = 50, CooldownSeconds = 180, X = -249850, Y = 95820, Z = -3950 },
                        new CeAdminWarp { Id = "sinkhole", Name = "The Sinkhole Obelisk", Category = "Obelisks", RequiredRank = "Player", CostCopper = 50, CooldownSeconds = 180, X = 18240, Y = 112500, Z = -12400 },
                        new CeAdminWarp { Id = "mounds", Name = "Mounds of the Dead", Category = "Highlands", RequiredRank = "Player", CostCopper = 100, CooldownSeconds = 240, X = -221500, Y = -62400, Z = -1200 },
                        new CeAdminWarp { Id = "jungle", Name = "Buccaneer Bay (Jungle)", Category = "Jungle", RequiredRank = "Player", CostCopper = 100, CooldownSeconds = 240, X = 328400, Y = 88500, Z = -17500 },
                        new CeAdminWarp { Id = "volcano", Name = "Well of Skelos (Volcano)", Category = "North", RequiredRank = "VIP", CostCopper = 150, CooldownSeconds = 300, X = -42800, Y = -268500, Z = 36200 }
                    };
                }

                if (_store.Kits == null || _store.Kits.Count == 0)
                {
                    _store.Kits = new List<CeAdminKit>
                    {
                        new CeAdminKit { Id = "starter", DisplayName = "Exile Starter Survival Kit", RequiredRank = "Player", CooldownMinutes = 0, CostCopper = 0, ItemsSummary = "Iron Pick (51011), Iron Hatchet (51021), Iron Broadsword (51952), Waterskin (41031), 20x Cooked Haunch (13001), 10x Rough Wraps (18005)" },
                        new CeAdminKit { Id = "daily", DisplayName = "Daily Exile Supply Crate", RequiredRank = "Player", CooldownMinutes = 1440, CostCopper = 0, ItemsSummary = "10x Cooked Haunch (13001), 5x Aloe Extract (18006), 250x Stone (11001), 250x Wood (11011)" },
                        new CeAdminKit { Id = "vip", DisplayName = "VIP Exile Care Package", RequiredRank = "VIP", CooldownMinutes = 1440, CostCopper = 0, ItemsSummary = "10x Pure Aloe Extract (18007), 100x Iron Bar (11501), 50x Steel Bar (11502)" }
                    };
                }

                SaveStore();
            }
        }

        public void SaveStore()
        {
            lock (_storeLock)
            {
                try
                {
                    string json = JsonSerializer.Serialize(_store, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(StoreFilePath, json);
                }
                catch (Exception ex)
                {
                    _engine.Log($"[CEAdmin Store] Save warning: {ex.Message}");
                }
            }
        }

        public void ProcessServerLogLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            // 1. Real-time position heartbeat from ConanEnhancedAdmin ModController:
            // Format: [CEAdmin_Pos] CharName|X|Y|Z
            int posIdx = line.IndexOf("[CEAdmin_Pos]", StringComparison.OrdinalIgnoreCase);
            if (posIdx >= 0)
            {
                string payload = line.Substring(posIdx + "[CEAdmin_Pos]".Length).Trim();
                var parts = payload.Split('|');
                if (parts.Length >= 4 &&
                    double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double px) &&
                    double.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double py) &&
                    double.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double pz))
                {
                    _liveModPositions[parts[0].Trim()] = (px, py, pz, DateTime.UtcNow);
                }
            }
        }

        public void AddOrUpdateWarp(CeAdminWarp warp)
        {
            lock (_storeLock)
            {
                if (string.IsNullOrWhiteSpace(warp.Id))
                {
                    warp.Id = RegexSafeId(warp.Name);
                }
                var existing = _store.Warps.FirstOrDefault(w => w.Id.Equals(warp.Id, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.Name = warp.Name;
                    existing.Category = warp.Category;
                    existing.RequiredRank = warp.RequiredRank;
                    existing.CostCopper = warp.CostCopper;
                    existing.CooldownSeconds = warp.CooldownSeconds;
                    existing.X = warp.X;
                    existing.Y = warp.Y;
                    existing.Z = warp.Z;
                }
                else
                {
                    _store.Warps.Add(warp);
                }
                SaveStore();
                _lastSnapshotTime = DateTime.MinValue;
            }
        }

        public void DeleteWarp(string warpId)
        {
            lock (_storeLock)
            {
                _store.Warps.RemoveAll(w => w.Id.Equals(warpId, StringComparison.OrdinalIgnoreCase) || w.Name.Equals(warpId, StringComparison.OrdinalIgnoreCase));
                SaveStore();
                _lastSnapshotTime = DateTime.MinValue;
            }
        }

        public void AddOrUpdateKit(CeAdminKit kit)
        {
            lock (_storeLock)
            {
                if (string.IsNullOrWhiteSpace(kit.Id))
                {
                    kit.Id = RegexSafeId(kit.DisplayName);
                }
                var existing = _store.Kits.FirstOrDefault(k => k.Id.Equals(kit.Id, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.DisplayName = kit.DisplayName;
                    existing.RequiredRank = kit.RequiredRank;
                    existing.CooldownMinutes = kit.CooldownMinutes;
                    existing.CostCopper = kit.CostCopper;
                    existing.ItemsSummary = kit.ItemsSummary;
                }
                else
                {
                    _store.Kits.Add(kit);
                }
                SaveStore();
                _lastSnapshotTime = DateTime.MinValue;
            }
        }

        public void DeleteKit(string kitId)
        {
            lock (_storeLock)
            {
                _store.Kits.RemoveAll(k => k.Id.Equals(kitId, StringComparison.OrdinalIgnoreCase));
                SaveStore();
                _lastSnapshotTime = DateTime.MinValue;
            }
        }

        public void UpdatePlayerProfile(string charName, string rank, long walletCopper)
        {
            if (string.IsNullOrWhiteSpace(charName)) return;
            lock (_storeLock)
            {
                _store.Profiles[charName.Trim()] = new CeAdminPlayerProfile
                {
                    CharacterName = charName.Trim(),
                    Rank = string.IsNullOrWhiteSpace(rank) ? "Player" : rank.Trim(),
                    WalletCopper = Math.Max(0, walletCopper)
                };
                SaveStore();
                _lastSnapshotTime = DateTime.MinValue;
            }
        }

        private static string RegexSafeId(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "item_" + DateTime.Now.Ticks % 10000;
            var sb = new StringBuilder();
            foreach (char c in input.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (c == ' ' || c == '-' || c == '_') sb.Append('_');
            }
            string res = sb.ToString().Trim('_');
            return string.IsNullOrEmpty(res) ? "warp_" + DateTime.Now.Ticks % 10000 : res;
        }

        public WorldMapSnapshot GetWorldMapSnapshot(bool forceRefresh = false)
        {
            if (!forceRefresh && _cachedSnapshot != null && (DateTime.UtcNow - _lastSnapshotTime).TotalSeconds < 4.0)
            {
                return _cachedSnapshot;
            }

            var snapshot = new WorldMapSnapshot
            {
                ServerName = string.IsNullOrWhiteSpace(_engine.Config.ServerName) ? "Conan Exiles Enhanced Server" : _engine.Config.ServerName,
                ServerStatus = _engine.ServerStatus,
                StartupMap = _engine.Config.StartupMap,
                LastUpdated = DateTime.Now.ToString("HH:mm:ss")
            };

            lock (_storeLock)
            {
                snapshot.Warps = _store.Warps.ToList();
                snapshot.Homes = _store.Homes.ToList();
                snapshot.Kits = _store.Kits.ToList();
            }

            string dbPath = _engine.GetActiveGameDbPath();
            snapshot.DatabasePath = dbPath;
            snapshot.DatabaseFound = File.Exists(dbPath);

            var onlineNames = new HashSet<string>(
                _engine.ConnectedPlayers.Select(p => p.Name.Trim()),
                StringComparer.OrdinalIgnoreCase);

            if (snapshot.DatabaseFound)
            {
                ScanSqliteWorldDatabase(dbPath, snapshot, onlineNames);
            }

            // Ensure any currently online players who haven't been flushed to game.db yet are still listed
            foreach (var onlinePlayer in _engine.ConnectedPlayers)
            {
                if (!snapshot.Players.Any(p => p.CharacterName.Equals(onlinePlayer.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    double px = -25240, py = 261150, pz = -20180;
                    if (_liveModPositions.TryGetValue(onlinePlayer.Name, out var livePos))
                    {
                        px = livePos.X;
                        py = livePos.Y;
                        pz = livePos.Z;
                    }
                    var profile = GetOrCreateProfile(onlinePlayer.Name);
                    snapshot.Players.Add(new WorldPlayerMarker
                    {
                        CharId = 0,
                        PlayerId = "",
                        CharacterName = onlinePlayer.Name,
                        Level = 60,
                        ClanName = "Online Exile",
                        X = px,
                        Y = py,
                        Z = pz,
                        GridSector = WorldToGridSector(px, py),
                        IsOnline = true,
                        LastOnlineText = "ONLINE NOW",
                        Rank = profile.Rank,
                        WalletCopper = profile.WalletCopper
                    });
                }
            }

            snapshot.OnlinePlayerCount = snapshot.Players.Count(p => p.IsOnline);
            snapshot.TotalCharacterCount = snapshot.Players.Count;
            snapshot.TotalBaseCount = snapshot.Bases.Count;
            snapshot.TotalBuildingPieces = snapshot.Bases.Sum(b => b.PieceCount);

            _cachedSnapshot = snapshot;
            _lastSnapshotTime = DateTime.UtcNow;
            return snapshot;
        }

        private CeAdminPlayerProfile GetOrCreateProfile(string charName)
        {
            lock (_storeLock)
            {
                if (_store.Profiles.TryGetValue(charName, out var prof))
                {
                    return prof;
                }
                var created = new CeAdminPlayerProfile { CharacterName = charName, Rank = "Player", WalletCopper = 100 };
                _store.Profiles[charName] = created;
                return created;
            }
        }

        private void ScanSqliteWorldDatabase(string dbPath, WorldMapSnapshot snapshot, HashSet<string> onlineNames)
        {
            string tempSnapshotPath = Path.Combine(Path.GetTempPath(), "conan_livemap_readonly.db");
            try
            {
                // Copy with FileShare.ReadWrite so we never lock ConanSandboxServer.exe's active WAL/DB
                using (var srcStream = new FileStream(dbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var dstStream = new FileStream(tempSnapshotPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    srcStream.CopyTo(dstStream);
                }

                string walPath = dbPath + "-wal";
                if (File.Exists(walPath))
                {
                    try
                    {
                        using var srcWal = new FileStream(walPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                        using var dstWal = new FileStream(tempSnapshotPath + "-wal", FileMode.Create, FileAccess.Write, FileShare.Read);
                        srcWal.CopyTo(dstWal);
                    }
                    catch { }
                }
            }
            catch
            {
                // Fallback to direct ReadOnly connection if temp copy fails
                tempSnapshotPath = dbPath;
            }

            try
            {
                using var conn = new SqliteConnection($"Data Source={tempSnapshotPath};Mode=ReadOnly;");
                conn.Open();

                // 1. Load Guilds (Clans)
                var guilds = new Dictionary<long, string>();
                try
                {
                    using var cmdGuilds = conn.CreateCommand();
                    cmdGuilds.CommandText = "SELECT guildId, name FROM guilds";
                    using var rdr = cmdGuilds.ExecuteReader();
                    while (rdr.Read())
                    {
                        long gid = rdr.IsDBNull(0) ? 0 : rdr.GetInt64(0);
                        string gname = rdr.IsDBNull(1) ? "Unnamed Clan" : rdr.GetString(1);
                        if (gid != 0) guilds[gid] = gname;
                    }
                }
                catch { }

                // 2. Load Characters + Actor Positions
                var charNamesById = new Dictionary<long, string>();
                var clanMemberCounts = new Dictionary<long, int>();
                try
                {
                    using var cmdChars = conn.CreateCommand();
                    cmdChars.CommandText = @"
                        SELECT c.id, c.playerId, c.char_name, c.level, c.guild, c.lastTimeOnline,
                               ap.x, ap.y, ap.z
                        FROM characters c
                        LEFT JOIN actor_position ap ON ap.id = c.id";
                    using var rdr = cmdChars.ExecuteReader();
                    while (rdr.Read())
                    {
                        long cid = rdr.IsDBNull(0) ? 0 : rdr.GetInt64(0);
                        string pid = rdr.IsDBNull(1) ? "" : rdr.GetValue(1)?.ToString() ?? "";
                        string cname = rdr.IsDBNull(2) ? $"Exile #{cid}" : rdr.GetString(2);
                        int level = rdr.IsDBNull(3) ? 1 : rdr.GetInt32(3);
                        long guildId = rdr.IsDBNull(4) ? 0 : rdr.GetInt64(4);
                        long lastOnlineUnix = rdr.IsDBNull(5) ? 0 : rdr.GetInt64(5);
                        double x = rdr.IsDBNull(6) ? 0.0 : rdr.GetDouble(6);
                        double y = rdr.IsDBNull(7) ? 0.0 : rdr.GetDouble(7);
                        double z = rdr.IsDBNull(8) ? 0.0 : rdr.GetDouble(8);

                        charNamesById[cid] = cname;
                        if (guildId != 0)
                        {
                            clanMemberCounts[guildId] = clanMemberCounts.GetValueOrDefault(guildId, 0) + 1;
                        }

                        // Check if live mod position override exists
                        if (_liveModPositions.TryGetValue(cname, out var livePos) && (DateTime.UtcNow - livePos.Time).TotalMinutes < 30)
                        {
                            x = livePos.X;
                            y = livePos.Y;
                            z = livePos.Z;
                        }

                        bool isOnline = onlineNames.Contains(cname);
                        string clanName = guildId != 0 && guilds.TryGetValue(guildId, out var gn) ? gn : "Solo Exile";
                        string lastSeenStr = isOnline
                            ? "ONLINE NOW"
                            : (lastOnlineUnix > 0
                                ? DateTimeOffset.FromUnixTimeSeconds(lastOnlineUnix).ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                                : "Sleeping");

                        var profile = GetOrCreateProfile(cname);
                        snapshot.Players.Add(new WorldPlayerMarker
                        {
                            CharId = cid,
                            PlayerId = pid,
                            CharacterName = cname,
                            Level = level,
                            GuildId = guildId,
                            ClanName = clanName,
                            X = x,
                            Y = y,
                            Z = z,
                            GridSector = WorldToGridSector(x, y),
                            IsOnline = isOnline,
                            LastOnlineText = lastSeenStr,
                            Rank = profile.Rank,
                            WalletCopper = profile.WalletCopper
                        });
                    }
                }
                catch { }

                // Sort players: Online first, then by level descending
                snapshot.Players = snapshot.Players
                    .OrderByDescending(p => p.IsOnline)
                    .ThenByDescending(p => p.Level)
                    .ThenBy(p => p.CharacterName)
                    .ToList();

                // 3. Load Buildings & Piece Counts
                var rawStructures = new List<(long ObjectId, long OwnerId, double X, double Y, double Z, int Pieces)>();
                try
                {
                    using var cmdBld = conn.CreateCommand();
                    cmdBld.CommandText = @"
                        SELECT b.object_id, b.owner_id, ap.x, ap.y, ap.z,
                               COUNT(bh.instance_id) AS piece_count
                        FROM buildings b
                        INNER JOIN actor_position ap ON ap.id = b.object_id
                        LEFT JOIN buildable_health bh ON bh.object_id = b.object_id
                        WHERE b.owner_id > 0
                        GROUP BY b.object_id, b.owner_id, ap.x, ap.y, ap.z";
                    using var rdr = cmdBld.ExecuteReader();
                    while (rdr.Read())
                    {
                        long objId = rdr.IsDBNull(0) ? 0 : rdr.GetInt64(0);
                        long ownerId = rdr.IsDBNull(1) ? 0 : rdr.GetInt64(1);
                        double x = rdr.IsDBNull(2) ? 0.0 : rdr.GetDouble(2);
                        double y = rdr.IsDBNull(3) ? 0.0 : rdr.GetDouble(3);
                        double z = rdr.IsDBNull(4) ? 0.0 : rdr.GetDouble(4);
                        int pieces = rdr.IsDBNull(5) ? 1 : Math.Max(1, rdr.GetInt32(5));
                        rawStructures.Add((objId, ownerId, x, y, z, pieces));
                    }
                }
                catch { }

                // Cluster nearby structures belonging to the same owner within 15,000 Unreal units (150 meters)
                const double ClusterRadiusSq = 15000.0 * 15000.0;
                var clusters = new List<WorldBaseMarker>();

                foreach (var st in rawStructures.OrderByDescending(s => s.Pieces))
                {
                    var existingCluster = clusters.FirstOrDefault(c =>
                        c.OwnerId == st.OwnerId &&
                        ((c.X - st.X) * (c.X - st.X) + (c.Y - st.Y) * (c.Y - st.Y)) <= ClusterRadiusSq);

                    if (existingCluster != null)
                    {
                        existingCluster.PieceCount += st.Pieces;
                        existingCluster.StructureCount += 1;
                    }
                    else
                    {
                        bool isClan = guilds.TryGetValue(st.OwnerId, out string? ownerName);
                        if (!isClan || string.IsNullOrEmpty(ownerName))
                        {
                            ownerName = charNamesById.TryGetValue(st.OwnerId, out string? cn) ? $"{cn} (Solo)" : $"Owner #{st.OwnerId}";
                        }

                        clusters.Add(new WorldBaseMarker
                        {
                            BaseClusterId = st.ObjectId,
                            OwnerId = st.OwnerId,
                            OwnerName = ownerName,
                            IsClanOwned = isClan,
                            PieceCount = st.Pieces,
                            StructureCount = 1,
                            X = st.X,
                            Y = st.Y,
                            Z = st.Z,
                            GridSector = WorldToGridSector(st.X, st.Y)
                        });
                    }
                }

                snapshot.Bases = clusters.OrderByDescending(b => b.PieceCount).ToList();

                // 4. Build Clan / Owner Bloat Leaderboard
                snapshot.Clans = snapshot.Bases
                    .GroupBy(b => new { b.OwnerId, b.OwnerName, b.IsClanOwned })
                    .Select(g =>
                    {
                        var mainBase = g.OrderByDescending(x => x.PieceCount).First();
                        int members = g.Key.IsClanOwned ? clanMemberCounts.GetValueOrDefault(g.Key.OwnerId, 1) : 1;
                        return new ClanBloatSummary
                        {
                            OwnerId = g.Key.OwnerId,
                            ClanOrPlayerName = g.Key.OwnerName,
                            MemberCount = members,
                            BaseClusterCount = g.Count(),
                            TotalStructureCount = g.Sum(x => x.StructureCount),
                            TotalPieceCount = g.Sum(x => x.PieceCount),
                            MainBaseGrid = mainBase.GridSector,
                            MainBaseTeleport = mainBase.TeleportCommand
                        };
                    })
                    .OrderByDescending(c => c.TotalPieceCount)
                    .ToList();
            }
            catch (Exception ex)
            {
                _engine.Log($"[Live Map DB Scanner] Warning: {ex.Message}");
            }
        }

        // ====================================================================
        // Standalone Dedicated Live Map Web Server (Default Port 8089)
        // ====================================================================
        public async Task StartLiveMapServerAsync(int port = 8089)
        {
            if (_isListening && _boundPort == port) return;
            StopLiveMapServer();

            _boundPort = port;
            await Task.Run(() =>
            {
                try
                {
                    _mapListener = new TcpListener(IPAddress.Any, port);
                    _mapListener.Start();
                    _isListening = true;
                    _engine.Log($"🗺️ Live World Map & Pippi Portal active on http://0.0.0.0:{port}");
                    _ = Task.Run(ListenLoopAsync);
                }
                catch (Exception ex)
                {
                    _engine.Log($"[Live Map Web Server] Failed to bind port {port}: {ex.Message}");
                }
            });
        }

        public void StopLiveMapServer()
        {
            _isListening = false;
            try { _mapListener?.Stop(); } catch { }
            _mapListener = null;
        }

        private async Task ListenLoopAsync()
        {
            while (_isListening && _mapListener != null)
            {
                try
                {
                    var client = await _mapListener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleMapHttpClientAsync(client));
                }
                catch
                {
                    if (!_isListening) break;
                }
            }
        }

        private async Task HandleMapHttpClientAsync(TcpClient client)
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

                    string rawReq = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    int headerEnd = rawReq.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                    if (headerEnd == -1) headerEnd = rawReq.IndexOf("\n\n", StringComparison.Ordinal);

                    string headersPart = headerEnd != -1 ? rawReq.Substring(0, headerEnd) : rawReq;
                    var lines = headersPart.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    if (lines.Length == 0) return;

                    var reqParts = lines[0].Split(' ');
                    if (reqParts.Length < 2) return;

                    string method = reqParts[0].ToUpperInvariant();
                    string rawUrl = reqParts[1];
                    string path = rawUrl.Split('?')[0].TrimEnd('/').ToLowerInvariant();
                    if (string.IsNullOrEmpty(path)) path = "/";

                    int contentLength = 0;
                    foreach (var h in lines.Skip(1))
                    {
                        if (h.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        {
                            int.TryParse(h.Substring("Content-Length:".Length).Trim(), out contentLength);
                        }
                    }

                    string body = "";
                    if (headerEnd != -1)
                    {
                        int bodyStartOffset = Encoding.UTF8.GetByteCount(rawReq.Substring(0, headerEnd + (rawReq.Contains("\r\n\r\n") ? 4 : 2)));
                        int alreadyRead = bytesRead - bodyStartOffset;
                        using var ms = new MemoryStream();
                        if (alreadyRead > 0) ms.Write(buffer, bodyStartOffset, alreadyRead);
                        while (ms.Length < contentLength)
                        {
                            int r = await stream.ReadAsync(buffer, 0, buffer.Length);
                            if (r <= 0) break;
                            ms.Write(buffer, 0, r);
                        }
                        body = Encoding.UTF8.GetString(ms.ToArray());
                    }

                    if (method == "OPTIONS")
                    {
                        await WriteHttpResponseAsync(stream, "200 OK", "text/plain", "");
                        return;
                    }

                    if (path == "/" || path == "/index.html" || path == "/map")
                    {
                        string html = GetLiveMapHtmlPage();
                        await WriteHttpResponseAsync(stream, "200 OK", "text/html; charset=utf-8", html);
                        return;
                    }

                    if (path == "/map_bg.png" || path == "/map_bg.jpg")
                    {
                        string customImg = Path.Combine(ServerEngine.AppWorkingDir, path.TrimStart('/'));
                        if (File.Exists(customImg))
                        {
                            byte[] imgBytes = File.ReadAllBytes(customImg);
                            string mime = path.EndsWith(".png") ? "image/png" : "image/jpeg";
                            await WriteBinaryHttpResponseAsync(stream, "200 OK", mime, imgBytes);
                            return;
                        }
                    }

                    string jsonResp = HandleMapApiRequest(method, path, body);
                    await WriteHttpResponseAsync(stream, "200 OK", "application/json; charset=utf-8", jsonResp);
                }
                catch { }
            }
        }

        public string HandleMapApiRequest(string method, string path, string body)
        {
            if (path == "/api/map/data")
            {
                bool force = method == "POST";
                var snap = GetWorldMapSnapshot(force);
                return JsonSerializer.Serialize(snap);
            }

            if (method == "POST" && path == "/api/map/warp/add")
            {
                var warp = JsonSerializer.Deserialize<CeAdminWarp>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (warp != null && !string.IsNullOrWhiteSpace(warp.Name))
                {
                    AddOrUpdateWarp(warp);
                    return JsonSerializer.Serialize(new { success = true, message = $"Warp '{warp.Name}' saved." });
                }
                return JsonSerializer.Serialize(new { success = false, error = "Invalid warp data." });
            }

            if (method == "POST" && path == "/api/map/warp/delete")
            {
                using var doc = JsonDocument.Parse(body);
                string id = doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                DeleteWarp(id);
                return JsonSerializer.Serialize(new { success = true });
            }

            if (method == "POST" && path == "/api/map/player/update")
            {
                using var doc = JsonDocument.Parse(body);
                string name = doc.RootElement.TryGetProperty("characterName", out var nEl) ? nEl.GetString() ?? "" : "";
                string rank = doc.RootElement.TryGetProperty("rank", out var rEl) ? rEl.GetString() ?? "Player" : "Player";
                long copper = doc.RootElement.TryGetProperty("walletCopper", out var wEl) ? wEl.GetInt64() : 0;
                UpdatePlayerProfile(name, rank, copper);
                return JsonSerializer.Serialize(new { success = true });
            }

            return JsonSerializer.Serialize(new { error = "Unknown endpoint" });
        }

        private static async Task WriteHttpResponseAsync(NetworkStream stream, string status, string contentType, string content)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(content);
            await WriteBinaryHttpResponseAsync(stream, status, contentType, bodyBytes);
        }

        private static async Task WriteBinaryHttpResponseAsync(NetworkStream stream, string status, string contentType, byte[] bodyBytes)
        {
            string headers =
                $"HTTP/1.1 {status}\r\n" +
                $"Content-Type: {contentType}\r\n" +
                $"Content-Length: {bodyBytes.Length}\r\n" +
                "Access-Control-Allow-Origin: *\r\n" +
                "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
                "Access-Control-Allow-Headers: Content-Type\r\n" +
                "Cache-Control: no-cache\r\n" +
                "Connection: close\r\n\r\n";
            byte[] headerBytes = Encoding.UTF8.GetBytes(headers);
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
            if (bodyBytes.Length > 0)
            {
                await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length);
            }
            await stream.FlushAsync();
        }

        public string GetLiveMapHtmlPage()
        {
            return @"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
<title>Conan Exiles Enhanced — Live World Map &amp; Base Radar</title>
<style>
  :root {
    --bg: #070b14;
    --panel: #0f172a;
    --card: #1e293b;
    --border: #334155;
    --accent: #38bdf8;
    --green: #10b981;
    --amber: #f59e0b;
    --red: #ef4444;
    --purple: #a855f7;
    --text: #f8fafc;
    --muted: #94a3b8;
  }
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body {
    font-family: 'Segoe UI', system-ui, -apple-system, sans-serif;
    background: var(--bg);
    color: var(--text);
    height: 100vh;
    display: flex;
    flex-direction: column;
    overflow: hidden;
  }
  header {
    background: var(--panel);
    border-bottom: 1px solid var(--border);
    padding: 10px 16px;
    display: flex;
    justify-content: space-between;
    align-items: center;
    flex-wrap: wrap;
    gap: 10px;
    z-index: 10;
  }
  .title-wrap { display: flex; align-items: center; gap: 12px; }
  .title-main { font-size: 1.1rem; font-weight: 800; color: var(--accent); }
  .badge {
    padding: 3px 10px;
    border-radius: 999px;
    font-size: 0.75rem;
    font-weight: 700;
    background: var(--card);
    border: 1px solid var(--border);
  }
  .badge-online { color: var(--green); border-color: var(--green); }
  .stats-bar { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
  .btn {
    background: var(--card);
    color: var(--text);
    border: 1px solid var(--border);
    border-radius: 6px;
    padding: 6px 12px;
    font-size: 0.8rem;
    font-weight: 600;
    cursor: pointer;
    transition: all 0.15s;
  }
  .btn:hover { border-color: var(--accent); color: var(--accent); }
  .btn-primary { background: #0284c7; border-color: #38bdf8; color: #fff; }
  .btn-primary:hover { background: #0369a1; color: #fff; }
  .workspace {
    flex: 1;
    display: grid;
    grid-template-columns: 1fr 390px;
    min-height: 0;
  }
  @media (max-width: 960px) {
    .workspace { grid-template-columns: 1fr; grid-template-rows: 1fr 320px; }
  }
  .map-container {
    position: relative;
    background: #050911;
    overflow: hidden;
    user-select: none;
  }
  canvas {
    width: 100%;
    height: 100%;
    display: block;
    cursor: grab;
  }
  canvas:active { cursor: grabbing; }
  .map-hud-top {
    position: absolute;
    top: 12px;
    left: 12px;
    background: rgba(15, 23, 42, 0.88);
    backdrop-filter: blur(8px);
    border: 1px solid var(--border);
    border-radius: 8px;
    padding: 8px 12px;
    display: flex;
    gap: 12px;
    flex-wrap: wrap;
    font-size: 0.8rem;
  }
  .map-hud-top label { display: flex; align-items: center; gap: 5px; cursor: pointer; }
  .map-hud-bottom {
    position: absolute;
    bottom: 12px;
    left: 12px;
    background: rgba(15, 23, 42, 0.9);
    border: 1px solid var(--border);
    border-radius: 8px;
    padding: 6px 12px;
    font-family: Consolas, monospace;
    font-size: 0.8rem;
    color: var(--accent);
  }
  .map-tooltip {
    position: absolute;
    pointer-events: none;
    background: rgba(15, 23, 42, 0.96);
    border: 1.5px solid var(--accent);
    border-radius: 8px;
    padding: 10px 12px;
    font-size: 0.8rem;
    line-height: 1.45;
    max-width: 300px;
    display: none;
    z-index: 20;
    box-shadow: 0 10px 25px rgba(0,0,0,0.6);
  }
  .sidebar {
    background: var(--panel);
    border-left: 1px solid var(--border);
    display: flex;
    flex-direction: column;
    min-height: 0;
  }
  .tabs {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    border-bottom: 1px solid var(--border);
    background: #090e1a;
  }
  .tab-btn {
    padding: 10px 6px;
    text-align: center;
    font-size: 0.78rem;
    font-weight: 700;
    color: var(--muted);
    cursor: pointer;
    border-bottom: 2px solid transparent;
  }
  .tab-btn.active {
    color: var(--accent);
    border-bottom-color: var(--accent);
    background: var(--panel);
  }
  .tab-panel {
    flex: 1;
    overflow-y: auto;
    padding: 10px;
    display: none;
  }
  .tab-panel.active { display: block; }
  .item-card {
    background: var(--card);
    border: 1px solid var(--border);
    border-radius: 8px;
    padding: 9px 11px;
    margin-bottom: 8px;
    cursor: pointer;
    transition: border-color 0.15s;
  }
  .item-card:hover { border-color: var(--accent); }
  .item-row { display: flex; justify-content: space-between; align-items: center; gap: 6px; }
  .item-title { font-weight: 700; font-size: 0.88rem; }
  .item-sub { font-size: 0.75rem; color: var(--muted); margin-top: 3px; }
  .tp-chip {
    font-family: Consolas, monospace;
    font-size: 0.7rem;
    background: #0f172a;
    color: #38bdf8;
    padding: 2px 6px;
    border-radius: 4px;
    border: 1px solid #334155;
  }
</style>
</head>
<body>
<header>
  <div class=""title-wrap"">
    <span style=""font-size:1.35rem;"">🗺️</span>
    <div>
      <div class=""title-main"" id=""lblServerName"">Conan Exiles Enhanced — Live World Map</div>
      <div style=""font-size:0.73rem;color:var(--muted);"">Live SQLite game.db &amp; Mod Telemetry • Click any marker or right-click to create a Warp</div>
    </div>
  </div>
  <div class=""stats-bar"">
    <span class=""badge badge-online"" id=""badgeOnline"">🟢 0 Online</span>
    <span class=""badge"" id=""badgeChars"">👥 0 Exiles</span>
    <span class=""badge"" id=""badgeBases"">🏰 0 Bases</span>
    <span class=""badge"" id=""badgePieces"">🧱 0 Pieces</span>
    <button class=""btn"" onclick=""resetView()"">🎯 Reset Zoom</button>
    <button class=""btn btn-primary"" onclick=""fetchMapData(true)"">🔄 Refresh Now</button>
  </div>
</header>

<div class=""workspace"">
  <div class=""map-container"" id=""mapWrap"">
    <canvas id=""mapCanvas""></canvas>
    <div class=""map-hud-top"">
      <label><input type=""checkbox"" id=""chkOnline"" checked onchange=""drawMap()""> 🟢 Online Players</label>
      <label><input type=""checkbox"" id=""chkOffline"" checked onchange=""drawMap()""> 💤 Sleeping Players</label>
      <label><input type=""checkbox"" id=""chkBases"" checked onchange=""drawMap()""> 🏰 Clan Bases &amp; Heatmap</label>
      <label><input type=""checkbox"" id=""chkWarps"" checked onchange=""drawMap()""> 📍 Public Warps</label>
      <label><input type=""checkbox"" id=""chkGrid"" checked onchange=""drawMap()""> #️⃣ A1–P16 Grid</label>
    </div>
    <div class=""map-hud-bottom"" id=""lblCursorCoords"">Grid: G8 | X: 0, Y: 0 | Right-click to copy TeleportPlayer or add Warp</div>
    <div class=""map-tooltip"" id=""mapTooltip""></div>
  </div>

  <aside class=""sidebar"">
    <div class=""tabs"">
      <div class=""tab-btn active"" onclick=""switchSideTab('players', this)"">👥 Players</div>
      <div class=""tab-btn"" onclick=""switchSideTab('bases', this)"">🏰 Bases &amp; Clans</div>
      <div class=""tab-btn"" onclick=""switchSideTab('warps', this)"">📍 Warps &amp; Kits</div>
    </div>
    <div class=""tab-panel active"" id=""panel-players""></div>
    <div class=""tab-panel"" id=""panel-bases""></div>
    <div class=""tab-panel"" id=""panel-warps""></div>
  </aside>
</div>

<script>
const WORLD_MIN_X = -390000, WORLD_MAX_X = 450000;
const WORLD_MIN_Y = -360000, WORLD_MAX_Y = 380000;

let mapData = { players: [], bases: [], clans: [], warps: [], homes: [], kits: [] };
let camX = 0, camY = 0, zoom = 1.0;
let isDragging = false, dragStartX = 0, dragStartY = 0;
let selectedMarker = null;
let bgImage = new Image();
let bgLoaded = false;
bgImage.onload = () => { bgLoaded = true; drawMap(); };
bgImage.src = '/map_bg.png';

const canvas = document.getElementById('mapCanvas');
const ctx = canvas.getContext('2d');
const tooltip = document.getElementById('mapTooltip');

function resizeCanvas() {
  const rect = canvas.parentElement.getBoundingClientRect();
  canvas.width = rect.width;
  canvas.height = rect.height;
  drawMap();
}
window.addEventListener('resize', resizeCanvas);

function worldToScreen(wx, wy) {
  const size = Math.min(canvas.width, canvas.height) * 0.88 * zoom;
  const ox = (canvas.width - size) / 2 + camX;
  const oy = (canvas.height - size) / 2 + camY;
  const nx = (wx - WORLD_MIN_X) / (WORLD_MAX_X - WORLD_MIN_X);
  const ny = (wy - WORLD_MIN_Y) / (WORLD_MAX_Y - WORLD_MIN_Y);
  return { x: ox + nx * size, y: oy + ny * size, size };
}

function screenToWorld(sx, sy) {
  const size = Math.min(canvas.width, canvas.height) * 0.88 * zoom;
  const ox = (canvas.width - size) / 2 + camX;
  const oy = (canvas.height - size) / 2 + camY;
  const nx = (sx - ox) / size;
  const ny = (sy - oy) / size;
  return {
    x: WORLD_MIN_X + nx * (WORLD_MAX_X - WORLD_MIN_X),
    y: WORLD_MIN_Y + ny * (WORLD_MAX_Y - WORLD_MIN_Y)
  };
}

function worldToGrid(wx, wy) {
  const nx = Math.max(0, Math.min(0.999, (wx - WORLD_MIN_X) / (WORLD_MAX_X - WORLD_MIN_X)));
  const ny = Math.max(0, Math.min(0.999, (wy - WORLD_MIN_Y) / (WORLD_MAX_Y - WORLD_MIN_Y)));
  const col = String.fromCharCode(65 + Math.floor(nx * 16));
  const row = 16 - Math.floor(ny * 16);
  return col + row;
}

function resetView() {
  camX = 0; camY = 0; zoom = 1.0;
  drawMap();
}

function focusWorldPos(wx, wy) {
  zoom = 2.4;
  const size = Math.min(canvas.width, canvas.height) * 0.88 * zoom;
  const nx = (wx - WORLD_MIN_X) / (WORLD_MAX_X - WORLD_MIN_X);
  const ny = (wy - WORLD_MIN_Y) / (WORLD_MAX_Y - WORLD_MIN_Y);
  camX = (0.5 - nx) * size;
  camY = (0.5 - ny) * size;
  drawMap();
}

function drawTopographicalBiomes(ox, oy, size) {
  // Base map background
  const grad = ctx.createLinearGradient(ox, oy, ox, oy + size);
  grad.addColorStop(0.0, '#1e293b');   // Frozen North & Volcano
  grad.addColorStop(0.32, '#14332a');  // Highlands
  grad.addColorStop(0.55, '#3b2f1e');  // Central Desert / Savanna
  grad.addColorStop(0.82, '#45301c');  // Noob River & Southern Desert
  grad.addColorStop(1.0, '#0f1d2e');   // Southern/Eastern Sea
  ctx.fillStyle = grad;
  ctx.fillRect(ox, oy, size, size);

  // Biome Labels
  ctx.save();
  ctx.fillStyle = 'rgba(226, 232, 240, 0.16)';
  ctx.font = `bold ${Math.max(10, 13 * zoom)}px Segoe UI`;
  ctx.textAlign = 'center';
  const landmarks = [
    { name: 'VOLCANO & WELL OF SKELOS', x: -42800, y: -268500 },
    { name: 'FROZEN NORTH & TEMPLE OF FROST', x: -165000, y: -225000 },
    { name: 'HIGHLANDS & NEW ASAGARTH', x: -65000, y: -75000 },
    { name: 'MOUNDS OF THE DEAD', x: -221500, y: -62400 },
    { name: 'SEPERMERU OASIS', x: -249850, y: 95820 },
    { name: 'UNNAMED CITY', x: -115000, y: 135000 },
    { name: 'THE SINKHOLE & ARENA', x: 18240, y: 112500 },
    { name: 'NOOB RIVER (SENTINELS)', x: -25240, y: 261150 },
    { name: 'JUNGLE & BUCCANEER BAY', x: 300000, y: 95000 }
  ];
  landmarks.forEach(lm => {
    const p = worldToScreen(lm.x, lm.y);
    ctx.fillText(lm.name, p.x, p.y);
  });
  ctx.restore();
}

function drawMap() {
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  const topLeft = worldToScreen(WORLD_MIN_X, WORLD_MIN_Y);
  const ox = topLeft.x, oy = topLeft.y, size = topLeft.size;

  if (bgLoaded) {
    ctx.drawImage(bgImage, ox, oy, size, size);
  } else {
    drawTopographicalBiomes(ox, oy, size);
  }

  // Border
  ctx.strokeStyle = '#334155';
  ctx.lineWidth = 2;
  ctx.strokeRect(ox, oy, size, size);

  // A1 - P16 Grid
  if (document.getElementById('chkGrid').checked) {
    const cell = size / 16;
    ctx.strokeStyle = 'rgba(148, 163, 184, 0.16)';
    ctx.lineWidth = 1;
    ctx.fillStyle = 'rgba(148, 163, 184, 0.55)';
    ctx.font = '10px Consolas, monospace';
    for (let i = 0; i < 16; i++) {
      const gx = ox + i * cell;
      const gy = oy + i * cell;
      ctx.beginPath(); ctx.moveTo(gx, oy); ctx.lineTo(gx, oy + size); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(ox, gy); ctx.lineTo(ox + size, gy); ctx.stroke();
      ctx.fillText(String.fromCharCode(65 + i), gx + cell / 2 - 4, oy + 12);
      ctx.fillText(String(16 - i), ox + 4, gy + cell / 2 + 4);
    }
  }

  // 1. Clan Bases & Heatmap
  if (document.getElementById('chkBases').checked && mapData.bases) {
    mapData.bases.forEach(b => {
      const p = worldToScreen(b.X, b.Y);
      const radius = Math.min(26, Math.max(6, Math.sqrt(b.PieceCount) * 0.42)) * Math.min(1.6, Math.sqrt(zoom));
      const color = b.PieceCount >= 1500 ? '#ef4444' : (b.PieceCount >= 400 ? '#f59e0b' : '#10b981');

      ctx.beginPath();
      ctx.arc(p.x, p.y, radius * 1.6, 0, Math.PI * 2);
      ctx.fillStyle = b.PieceCount >= 1500 ? 'rgba(239,68,68,0.22)' : (b.PieceCount >= 400 ? 'rgba(245,158,11,0.18)' : 'rgba(16,185,129,0.16)');
      ctx.fill();

      ctx.beginPath();
      ctx.arc(p.x, p.y, radius, 0, Math.PI * 2);
      ctx.fillStyle = color;
      ctx.fill();
      ctx.strokeStyle = '#0f172a';
      ctx.lineWidth = 1.5;
      ctx.stroke();

      if (zoom >= 1.3 || b.PieceCount >= 600) {
        ctx.fillStyle = '#f8fafc';
        ctx.font = 'bold 10px Segoe UI';
        ctx.fillText(`🏰 ${b.OwnerName} (${b.PieceCount})`, p.x + radius + 4, p.y + 3);
      }
    });
  }

  // 2. Public Warps
  if (document.getElementById('chkWarps').checked && mapData.warps) {
    mapData.warps.forEach(w => {
      const p = worldToScreen(w.X, w.Y);
      ctx.save();
      ctx.translate(p.x, p.y);
      ctx.rotate(Math.PI / 4);
      ctx.fillStyle = '#38bdf8';
      ctx.fillRect(-5, -5, 10, 10);
      ctx.strokeStyle = '#0f172a';
      ctx.lineWidth = 1.5;
      ctx.strokeRect(-5, -5, 10, 10);
      ctx.restore();

      ctx.fillStyle = '#7dd3fc';
      ctx.font = 'bold 10px Segoe UI';
      ctx.fillText(`📍 ${w.Name}`, p.x + 8, p.y - 6);
    });
  }

  // 3. Players (Offline then Online on top)
  const showOnline = document.getElementById('chkOnline').checked;
  const showOffline = document.getElementById('chkOffline').checked;
  if (mapData.players) {
    mapData.players.forEach(pl => {
      if (pl.IsOnline && !showOnline) return;
      if (!pl.IsOnline && !showOffline) return;

      const p = worldToScreen(pl.X, pl.Y);
      if (pl.IsOnline) {
        ctx.beginPath();
        ctx.arc(p.x, p.y, 12, 0, Math.PI * 2);
        ctx.fillStyle = 'rgba(16, 185, 129, 0.32)';
        ctx.fill();
      }
      ctx.beginPath();
      ctx.arc(p.x, p.y, pl.IsOnline ? 6.5 : 4.5, 0, Math.PI * 2);
      ctx.fillStyle = pl.IsOnline ? '#10b981' : '#64748b';
      ctx.fill();
      ctx.strokeStyle = '#ffffff';
      ctx.lineWidth = 1.5;
      ctx.stroke();

      ctx.fillStyle = pl.IsOnline ? '#6ee7b7' : '#cbd5e1';
      ctx.font = pl.IsOnline ? 'bold 11px Segoe UI' : '10px Segoe UI';
      ctx.fillText(`${pl.CharacterName} (Lv.${pl.Level})`, p.x + 9, p.y + 4);
    });
  }
}

canvas.addEventListener('mousedown', e => {
  if (e.button === 0) {
    isDragging = true;
    dragStartX = e.clientX - camX;
    dragStartY = e.clientY - camY;
  }
});
window.addEventListener('mouseup', () => isDragging = false);
canvas.addEventListener('mousemove', e => {
  const rect = canvas.getBoundingClientRect();
  const mx = e.clientX - rect.left;
  const my = e.clientY - rect.top;

  if (isDragging) {
    camX = e.clientX - dragStartX;
    camY = e.clientY - dragStartY;
    drawMap();
    return;
  }

  const w = screenToWorld(mx, my);
  const grid = worldToGrid(w.x, w.y);
  document.getElementById('lblCursorCoords').textContent =
    `Grid: ${grid} | X: ${Math.round(w.x)}, Y: ${Math.round(w.y)} | Right-click to Copy TeleportPlayer or Add Warp`;

  // Hit-test markers for hover tooltip
  let hit = null;
  if (mapData.players) {
    for (const pl of mapData.players) {
      const sp = worldToScreen(pl.X, pl.Y);
      if (Math.hypot(sp.x - mx, sp.y - my) < 11) {
        hit = `<b>👤 ${pl.CharacterName}</b> (Lv.${pl.Level})<br>Clan: ${pl.ClanName}<br>Rank: ${pl.Rank} | Wallet: ${pl.WalletFormatted}<br>Status: ${pl.LastOnlineText}<br>Grid: <b>${pl.GridSector}</b> (${pl.CoordinatesText})<br><span style='color:#38bdf8'>Click to copy TeleportPlayer</span>`;
        selectedMarker = pl.TeleportCommand;
        break;
      }
    }
  }
  if (!hit && mapData.bases) {
    for (const b of mapData.bases) {
      const sp = worldToScreen(b.X, b.Y);
      if (Math.hypot(sp.x - mx, sp.y - my) < 14) {
        hit = `<b>🏰 ${b.OwnerName}</b><br>Building Pieces: <b>${b.PieceCount}</b> (${b.StructureCount} structures)<br>Load Impact: ${b.Severity}<br>Grid: <b>${b.GridSector}</b> (${b.CoordinatesText})<br><span style='color:#38bdf8'>Click to copy TeleportPlayer</span>`;
        selectedMarker = b.TeleportCommand;
        break;
      }
    }
  }
  if (!hit && mapData.warps) {
    for (const wp of mapData.warps) {
      const sp = worldToScreen(wp.X, wp.Y);
      if (Math.hypot(sp.x - mx, sp.y - my) < 10) {
        hit = `<b>📍 Warp: ${wp.Name}</b> (/warp ${wp.Id})<br>Grid: <b>${wp.GridSector}</b><br>Command: <code>${wp.TeleportCommand}</code>`;
        selectedMarker = wp.TeleportCommand;
        break;
      }
    }
  }

  if (hit) {
    tooltip.innerHTML = hit;
    tooltip.style.display = 'block';
    tooltip.style.left = Math.min(canvas.width - 280, mx + 14) + 'px';
    tooltip.style.top = Math.max(10, my + 14) + 'px';
  } else {
    tooltip.style.display = 'none';
    selectedMarker = null;
  }
});

canvas.addEventListener('click', () => {
  if (selectedMarker) {
    navigator.clipboard.writeText(selectedMarker);
    alert('Copied to clipboard:\n' + selectedMarker);
  }
});

canvas.addEventListener('contextmenu', async e => {
  e.preventDefault();
  const rect = canvas.getBoundingClientRect();
  const w = screenToWorld(e.clientX - rect.left, e.clientY - rect.top);
  const tp = `TeleportPlayer ${Math.round(w.x)} ${Math.round(w.y)} 0`;
  const warpName = prompt(`Location Grid ${worldToGrid(w.x, w.y)} (${Math.round(w.x)}, ${Math.round(w.y)})\n\nEnter a name to save a new Public Warp here (or Cancel to just copy '${tp}'):`);
  if (warpName && warpName.trim()) {
    await fetch('/api/map/warp/add', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ Name: warpName.trim(), X: Math.round(w.x), Y: Math.round(w.y), Z: 0, Category: 'Custom', RequiredRank: 'Player' })
    });
    fetchMapData(true);
  } else {
    navigator.clipboard.writeText(tp);
  }
});

canvas.addEventListener('wheel', e => {
  e.preventDefault();
  const factor = e.deltaY < 0 ? 1.15 : 0.87;
  zoom = Math.max(0.6, Math.min(6.0, zoom * factor));
  drawMap();
}, { passive: false });

function switchSideTab(tab, el) {
  document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
  document.querySelectorAll('.tab-panel').forEach(p => p.classList.remove('active'));
  el.classList.add('active');
  document.getElementById('panel-' + tab).classList.add('active');
}

function copyTp(cmd, ev) {
  if (ev) ev.stopPropagation();
  navigator.clipboard.writeText(cmd);
  alert('Copied: ' + cmd);
}

function renderSidebars() {
  document.getElementById('lblServerName').textContent = mapData.ServerName + ' — Live World Map';
  document.getElementById('badgeOnline').textContent = `🟢 ${mapData.OnlinePlayerCount || 0} Online`;
  document.getElementById('badgeChars').textContent = `👥 ${mapData.TotalCharacterCount || 0} Exiles`;
  document.getElementById('badgeBases').textContent = `🏰 ${mapData.TotalBaseCount || 0} Bases`;
  document.getElementById('badgePieces').textContent = `🧱 ${mapData.TotalBuildingPieces || 0} Pieces`;

  // Players
  const pEl = document.getElementById('panel-players');
  if (!mapData.players || mapData.players.length === 0) {
    pEl.innerHTML = `<div style='color:var(--muted);padding:12px;font-size:0.82rem;'>No characters found in game.db yet. Start the server and create a character to see live player tracking!</div>`;
  } else {
    pEl.innerHTML = mapData.players.map(p => `
      <div class='item-card' onclick='focusWorldPos(${p.X}, ${p.Y})'>
        <div class='item-row'>
          <span class='item-title' style='color:${p.IsOnline ? '#10b981' : '#e2e8f0'}'>${p.IsOnline ? '🟢' : '💤'} ${p.CharacterName} (Lv.${p.Level})</span>
          <span class='tp-chip' onclick=""copyTp('${p.TeleportCommand}', event)"">📋 TP (${p.GridSector})</span>
        </div>
        <div class='item-sub'>Clan: <b>${p.ClanName}</b> • Rank: <b>${p.Rank}</b> • Wallet: <b>${p.WalletFormatted}</b></div>
        <div class='item-sub'>Coords: ${p.CoordinatesText} • ${p.LastOnlineText}</div>
      </div>
    `).join('');
  }

  // Bases
  const bEl = document.getElementById('panel-bases');
  if (!mapData.bases || mapData.bases.length === 0) {
    bEl.innerHTML = `<div style='color:var(--muted);padding:12px;font-size:0.82rem;'>No player bases found in game.db yet.</div>`;
  } else {
    bEl.innerHTML = mapData.bases.map(b => `
      <div class='item-card' onclick='focusWorldPos(${b.X}, ${b.Y})'>
        <div class='item-row'>
          <span class='item-title'>🏰 ${b.OwnerName}</span>
          <span class='tp-chip' onclick=""copyTp('${b.TeleportCommand}', event)"">📋 TP (${b.GridSector})</span>
        </div>
        <div class='item-sub'>Pieces: <b style='color:${b.PieceCount >= 1500 ? '#ef4444' : '#38bdf8'}'>${b.PieceCount}</b> (${b.StructureCount} structures) • ${b.Severity}</div>
        <div class='item-sub'>Coords: ${b.CoordinatesText}</div>
      </div>
    `).join('');
  }

  // Warps
  const wEl = document.getElementById('panel-warps');
  wEl.innerHTML = (mapData.warps || []).map(w => `
    <div class='item-card' onclick='focusWorldPos(${w.X}, ${w.Y})'>
      <div class='item-row'>
        <span class='item-title' style='color:#38bdf8'>📍 ${w.Name}</span>
        <span class='tp-chip' onclick=""copyTp('${w.TeleportCommand}', event)"">📋 TP (${w.GridSector})</span>
      </div>
      <div class='item-sub'>Command: <code>/warp ${w.Id}</code> • Rank: ${w.RequiredRank} • Cost: ${w.CostCopper}c</div>
    </div>
  `).join('');
}

async function fetchMapData(force = false) {
  try {
    const res = await fetch('/api/map/data', { method: force ? 'POST' : 'GET' });
    const data = await res.json();
    // Normalize casing
    mapData = {
      ServerName: data.ServerName || 'Conan Exiles Enhanced',
      OnlinePlayerCount: data.OnlinePlayerCount || 0,
      TotalCharacterCount: data.TotalCharacterCount || 0,
      TotalBaseCount: data.TotalBaseCount || 0,
      TotalBuildingPieces: data.TotalBuildingPieces || 0,
      players: data.Players || [],
      bases: data.Bases || [],
      clans: data.Clans || [],
      warps: data.Warps || [],
      kits: data.Kits || []
    };
    renderSidebars();
    drawMap();
  } catch (e) { console.error(e); }
}

resizeCanvas();
fetchMapData();
setInterval(() => fetchMapData(false), 5000);
</script>
</body>
</html>";
        }
    }
}
