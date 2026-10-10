using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ConanServerManager
{
    public class WorkshopModItem
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Creator { get; set; } = "";
        public string PreviewUrl { get; set; } = "";
        public string ShortDescription { get; set; } = "";
        public long FileSize { get; set; } = 0;
        public int Subscriptions { get; set; } = 0;
        public bool IsInstalled { get; set; } = false;
        public bool IsDownloaded { get; set; } = false;
        public long TimeUpdated { get; set; } = 0;
    }

    public static class SteamWorkshopHelper
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly ConcurrentDictionary<string, WorkshopModItem> _cache = new ConcurrentDictionary<string, WorkshopModItem>(StringComparer.OrdinalIgnoreCase);
        private static readonly string CacheFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workshop_cache.json");
        private static readonly object _cacheLock = new object();

        static SteamWorkshopHelper()
        {
            LoadCache();
        }

        private static void LoadCache()
        {
            try
            {
                if (File.Exists(CacheFilePath))
                {
                    string json = File.ReadAllText(CacheFilePath);
                    var list = JsonSerializer.Deserialize<List<WorkshopModItem>>(json);
                    if (list != null)
                    {
                        foreach (var item in list)
                        {
                            if (!string.IsNullOrEmpty(item.Id))
                                _cache[item.Id] = item;
                        }
                    }
                }
            }
            catch { }
        }

        private static void SaveCache()
        {
            try
            {
                lock (_cacheLock)
                {
                    var list = new List<WorkshopModItem>(_cache.Values);
                    string json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(CacheFilePath, json);
                }
            }
            catch { }
        }

        public static WorkshopModItem? GetCachedMod(string modId)
        {
            if (string.IsNullOrWhiteSpace(modId)) return null;
            if (_cache.TryGetValue(modId.Trim(), out var item) && !string.IsNullOrWhiteSpace(item.Title) && !item.Title.StartsWith("Mod #"))
            {
                return item;
            }
            return null;
        }

        public static async Task<Dictionary<string, WorkshopModItem>> GetMultipleModDetailsAsync(IEnumerable<string> modIds)
        {
            var result = new Dictionary<string, WorkshopModItem>(StringComparer.OrdinalIgnoreCase);
            var idList = modIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct().ToList() ?? new List<string>();
            if (idList.Count == 0) return result;

            var missingIds = new List<string>();
            foreach (var id in idList)
            {
                if (_cache.TryGetValue(id, out var cached) && !string.IsNullOrWhiteSpace(cached.Title) && !cached.Title.StartsWith("Mod #"))
                {
                    result[id] = cached;
                }
                else
                {
                    missingIds.Add(id);
                }
            }

            if (missingIds.Count > 0)
            {
                const int batchSize = 50;
                for (int i = 0; i < missingIds.Count; i += batchSize)
                {
                    var chunk = missingIds.Skip(i).Take(batchSize).ToList();
                    try
                    {
                        var sb = new StringBuilder();
                        sb.Append("itemcount=").Append(chunk.Count);
                        for (int c = 0; c < chunk.Count; c++)
                        {
                            sb.Append("&publishedfileids[").Append(c).Append("]=").Append(Uri.EscapeDataString(chunk[c]));
                        }

                        using var content = new StringContent(sb.ToString(), Encoding.UTF8, "application/x-www-form-urlencoded");
                        var resp = await _httpClient.PostAsync("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/", content);
                        if (resp.IsSuccessStatusCode)
                        {
                            string json = await resp.Content.ReadAsStringAsync();
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("response", out var respEl) &&
                                respEl.TryGetProperty("publishedfiledetails", out var detailsArr))
                            {
                                foreach (var item in detailsArr.EnumerateArray())
                                {
                                    string id = item.TryGetProperty("publishedfileid", out var idProp) ? idProp.GetString() ?? "" : "";
                                    int resCode = 0;
                                    if (item.TryGetProperty("result", out var r))
                                    {
                                        if (r.ValueKind == JsonValueKind.Number) resCode = r.GetInt32();
                                        else if (r.ValueKind == JsonValueKind.String && int.TryParse(r.GetString(), out var parsedR)) resCode = parsedR;
                                    }

                                    string title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                                    string creator = item.TryGetProperty("creator", out var c) ? c.GetString() ?? "" : "";
                                    string previewUrl = item.TryGetProperty("preview_url", out var p) ? p.GetString() ?? "" : "";
                                    string desc = item.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                                    
                                    int subs = 0;
                                    if (item.TryGetProperty("subscriptions", out var s))
                                    {
                                        if (s.ValueKind == JsonValueKind.Number) subs = s.GetInt32();
                                        else if (s.ValueKind == JsonValueKind.String && int.TryParse(s.GetString(), out var parsedS)) subs = parsedS;
                                    }

                                    long fileSize = 0;
                                    if (item.TryGetProperty("file_size", out var fs))
                                    {
                                        if (fs.ValueKind == JsonValueKind.Number) fileSize = fs.GetInt64();
                                        else if (fs.ValueKind == JsonValueKind.String && long.TryParse(fs.GetString(), out var parsedFs)) fileSize = parsedFs;
                                    }

                                    long timeUpdated = 0;
                                    if (item.TryGetProperty("time_updated", out var tu))
                                    {
                                        if (tu.ValueKind == JsonValueKind.Number) timeUpdated = tu.GetInt64();
                                        else if (tu.ValueKind == JsonValueKind.String && long.TryParse(tu.GetString(), out var parsedTu)) timeUpdated = parsedTu;
                                    }

                                    if (!string.IsNullOrEmpty(id) && resCode == 1 && !string.IsNullOrWhiteSpace(title))
                                    {
                                        var modObj = new WorkshopModItem
                                        {
                                            Id = id,
                                            Title = title,
                                            Creator = creator,
                                            PreviewUrl = previewUrl,
                                            ShortDescription = desc.Length > 200 ? desc.Substring(0, 200) + "..." : desc,
                                            Subscriptions = subs,
                                            FileSize = fileSize,
                                            TimeUpdated = timeUpdated
                                        };
                                        _cache[id] = modObj;
                                        result[id] = modObj;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SteamWorkshopHelper] Error querying mod details: {ex.Message}");
                    }
                }

                SaveCache();
            }

            foreach (var id in idList)
            {
                if (!result.ContainsKey(id))
                {
                    result[id] = new WorkshopModItem
                    {
                        Id = id,
                        Title = $"Mod #{id}"
                    };
                }
            }

            return result;
        }

        public static async Task<Dictionary<string, WorkshopModItem>> QueryRemoteDetailsForceRefreshAsync(IEnumerable<string> modIds)
        {
            var result = new Dictionary<string, WorkshopModItem>(StringComparer.OrdinalIgnoreCase);
            var idList = modIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct().ToList() ?? new List<string>();
            if (idList.Count == 0) return result;

            const int batchSize = 50;
            for (int i = 0; i < idList.Count; i += batchSize)
            {
                var chunk = idList.Skip(i).Take(batchSize).ToList();
                try
                {
                    var sb = new StringBuilder();
                    sb.Append("itemcount=").Append(chunk.Count);
                    for (int c = 0; c < chunk.Count; c++)
                    {
                        sb.Append("&publishedfileids[").Append(c).Append("]=").Append(Uri.EscapeDataString(chunk[c]));
                    }

                    using var content = new StringContent(sb.ToString(), Encoding.UTF8, "application/x-www-form-urlencoded");
                    var resp = await _httpClient.PostAsync("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/", content);
                    if (resp.IsSuccessStatusCode)
                    {
                        string json = await resp.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("response", out var respEl) &&
                            respEl.TryGetProperty("publishedfiledetails", out var detailsArr))
                        {
                            foreach (var item in detailsArr.EnumerateArray())
                            {
                                string id = item.TryGetProperty("publishedfileid", out var idProp) ? idProp.GetString() ?? "" : "";
                                int resCode = 0;
                                if (item.TryGetProperty("result", out var r))
                                {
                                    if (r.ValueKind == JsonValueKind.Number) resCode = r.GetInt32();
                                    else if (r.ValueKind == JsonValueKind.String && int.TryParse(r.GetString(), out var parsedR)) resCode = parsedR;
                                }

                                string title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                                string creator = item.TryGetProperty("creator", out var c) ? c.GetString() ?? "" : "";
                                string previewUrl = item.TryGetProperty("preview_url", out var p) ? p.GetString() ?? "" : "";
                                string desc = item.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";

                                int subs = 0;
                                if (item.TryGetProperty("subscriptions", out var s))
                                {
                                    if (s.ValueKind == JsonValueKind.Number) subs = s.GetInt32();
                                    else if (s.ValueKind == JsonValueKind.String && int.TryParse(s.GetString(), out var parsedS)) subs = parsedS;
                                }

                                long fileSize = 0;
                                if (item.TryGetProperty("file_size", out var fs))
                                {
                                    if (fs.ValueKind == JsonValueKind.Number) fileSize = fs.GetInt64();
                                    else if (fs.ValueKind == JsonValueKind.String && long.TryParse(fs.GetString(), out var parsedFs)) fileSize = parsedFs;
                                }

                                long timeUpdated = 0;
                                if (item.TryGetProperty("time_updated", out var tu))
                                {
                                    if (tu.ValueKind == JsonValueKind.Number) timeUpdated = tu.GetInt64();
                                    else if (tu.ValueKind == JsonValueKind.String && long.TryParse(tu.GetString(), out var parsedTu)) timeUpdated = parsedTu;
                                }

                                if (!string.IsNullOrEmpty(id) && resCode == 1 && !string.IsNullOrWhiteSpace(title))
                                {
                                    var modObj = new WorkshopModItem
                                    {
                                        Id = id,
                                        Title = title,
                                        Creator = creator,
                                        PreviewUrl = previewUrl,
                                        ShortDescription = desc.Length > 200 ? desc.Substring(0, 200) + "..." : desc,
                                        Subscriptions = subs,
                                        FileSize = fileSize,
                                        TimeUpdated = timeUpdated
                                    };
                                    _cache[id] = modObj;
                                    result[id] = modObj;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SteamWorkshopHelper] Error force-refreshing mod details: {ex.Message}");
                }
            }

            SaveCache();
            return result;
        }

        public static async Task<WorkshopModItem?> GetModDetailsAsync(string modId)
        {
            if (string.IsNullOrWhiteSpace(modId)) return null;
            string cleanId = modId.Trim();
            if (_cache.TryGetValue(cleanId, out var cached) && !string.IsNullOrWhiteSpace(cached.Title) && !cached.Title.StartsWith("Mod #"))
            {
                return cached;
            }

            var map = await GetMultipleModDetailsAsync(new[] { cleanId });
            map.TryGetValue(cleanId, out var found);
            return found;
        }

        public static async Task<List<WorkshopModItem>> SearchModsAsync(string query, int count = 25)
        {
            var results = new List<WorkshopModItem>();
            if (string.IsNullOrWhiteSpace(query)) return results;

            string trimmed = query.Trim();
            if (Regex.IsMatch(trimmed, @"^\d{6,12}$"))
            {
                var single = await GetModDetailsAsync(trimmed);
                if (single != null)
                {
                    results.Add(single);
                    return results;
                }
            }

            try
            {
                string url = $"https://steamcommunity.com/workshop/browse/?appid=440900&searchtext={Uri.EscapeDataString(trimmed)}&browsesort=textsearch&section=readytouseitems";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                var resp = await _httpClient.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return results;

                string html = await resp.Content.ReadAsStringAsync();

                var pattern = @"publishedfileid\\+"":\\+""(?<id>\d+).*?preview_url\\+"":\\+""(?<preview>https:[^\\""]+).*?title\\+"":\\+""(?<title>[^\\""]+)";
                var matches = Regex.Matches(html, pattern, RegexOptions.Singleline);

                var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (Match m in matches)
                {
                    string id = m.Groups["id"].Value;
                    if (seenIds.Contains(id)) continue;
                    seenIds.Add(id);

                    string title = Regex.Unescape(m.Groups["title"].Value).Trim();
                    string preview = m.Groups["preview"].Value.Replace("\\/", "/").Trim();

                    var modItem = new WorkshopModItem
                    {
                        Id = id,
                        Title = title,
                        PreviewUrl = preview
                    };

                    _cache[id] = modItem;
                    results.Add(modItem);

                    if (results.Count >= count) break;
                }

                SaveCache();
            }
            catch { }

            return results;
        }
    }
}
