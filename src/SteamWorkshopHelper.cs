using System;
using System.Collections.Generic;
using System.Net.Http;
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
    }

    public static class SteamWorkshopHelper
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public static async Task<List<WorkshopModItem>> SearchModsAsync(string query, int count = 25)
        {
            var results = new List<WorkshopModItem>();
            if (string.IsNullOrWhiteSpace(query)) return results;

            // If query is an exact mod ID (digits)
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

                // Match publishedfileid and subsequent preview_url and title
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

                    results.Add(new WorkshopModItem
                    {
                        Id = id,
                        Title = title,
                        PreviewUrl = preview
                    });

                    if (results.Count >= count) break;
                }
            }
            catch { }

            return results;
        }

        public static async Task<WorkshopModItem?> GetModDetailsAsync(string modId)
        {
            if (string.IsNullOrWhiteSpace(modId)) return null;

            try
            {
                using var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("itemcount", "1"),
                    new KeyValuePair<string, string>("publishedfileids[0]", modId.Trim())
                });

                var resp = await _httpClient.PostAsync("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/", content);
                if (!resp.IsSuccessStatusCode) return null;

                string json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("response", out var respEl) &&
                    respEl.TryGetProperty("publishedfiledetails", out var detailsArr) &&
                    detailsArr.GetArrayLength() > 0)
                {
                    var item = detailsArr[0];
                    int result = item.TryGetProperty("result", out var r) ? r.GetInt32() : 0;
                    if (result != 1) return null; // 1 = Success

                    string title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    string creator = item.TryGetProperty("creator", out var c) ? c.GetString() ?? "" : "";
                    string previewUrl = item.TryGetProperty("preview_url", out var p) ? p.GetString() ?? "" : "";
                    string desc = item.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                    int subs = item.TryGetProperty("subscriptions", out var s) ? s.GetInt32() : 0;
                    long fileSize = item.TryGetProperty("file_size", out var fs) ? fs.GetInt64() : 0;

                    return new WorkshopModItem
                    {
                        Id = modId.Trim(),
                        Title = title,
                        Creator = creator,
                        PreviewUrl = previewUrl,
                        ShortDescription = desc.Length > 200 ? desc.Substring(0, 200) + "..." : desc,
                        Subscriptions = subs,
                        FileSize = fileSize
                    };
                }
            }
            catch { }

            return null;
        }
    }
}
