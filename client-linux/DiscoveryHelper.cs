using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ConanServerManager.Linux
{
    public class DiscoveredServer
    {
        public string ServerName { get; set; } = "";
        public string Url { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public int WebPort { get; set; } = 8088;
        public int GamePort { get; set; } = 7777;
        public string Source { get; set; } = "LAN"; // Local, LAN, VM, Recent, Custom

        public string DisplayText
        {
            get
            {
                if (Source == "Local") return $"🖥️ Local Host ({Url})";
                if (Source == "Custom") return "✏️ Custom Server (Enter IP / URL below)";
                if (Source == "Recent") return $"⭐ {ServerName} ({Url})";
                return $"🌐 [{Source}] {ServerName} ({Url})";
            }
        }

        public override string ToString() => DisplayText;
    }

    public static class DiscoveryHelper
    {
        public const int DiscoveryPort = 8089;
        private const string PingMsg = "CONAN_DISCOVER_REQUEST";
        private const string PongPrefix = "CONAN_DISCOVER_RESPONSE";

        public static async Task<List<DiscoveredServer>> DiscoverServersAsync(int timeoutMs = 1500)
        {
            var servers = new Dictionary<string, DiscoveredServer>(StringComparer.OrdinalIgnoreCase);

            // 1. Send UDP Broadcast
            try
            {
                using var client = new UdpClient();
                client.EnableBroadcast = true;
                client.Client.SendTimeout = timeoutMs;
                client.Client.ReceiveTimeout = timeoutMs;

                byte[] pingBytes = Encoding.UTF8.GetBytes(PingMsg);

                // Collect broadcast addresses
                var broadcastAddresses = GetBroadcastAddresses();
                foreach (var bcast in broadcastAddresses)
                {
                    try
                    {
                        var ep = new IPEndPoint(bcast, DiscoveryPort);
                        await client.SendAsync(pingBytes, pingBytes.Length, ep);
                    }
                    catch { }
                }

                // Collect responses
                using var cts = new CancellationTokenSource(timeoutMs);
                var token = cts.Token;

                var receiveLoop = Task.Run(async () =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var res = await client.ReceiveAsync(token);
                            string reply = Encoding.UTF8.GetString(res.Buffer);

                            if (reply.StartsWith(PongPrefix, StringComparison.OrdinalIgnoreCase))
                            {
                                var parts = reply.Split('|');
                                string sName = parts.Length > 1 ? parts[1] : "Conan Server";
                                int wPort = parts.Length > 2 && int.TryParse(parts[2], out int wp) ? wp : 8088;
                                int gPort = parts.Length > 3 && int.TryParse(parts[3], out int gp) ? gp : 7777;

                                string ipStr = res.RemoteEndPoint.Address.ToString();
                                string url = $"http://{ipStr}:{wPort}";

                                bool isVm = ipStr.StartsWith("192.168.56.") || ipStr.StartsWith("172.") || ipStr.StartsWith("10.");
                                string source = isVm ? "VM" : "LAN";

                                lock (servers)
                                {
                                    if (!servers.ContainsKey(url))
                                    {
                                        servers[url] = new DiscoveredServer
                                        {
                                            ServerName = sName,
                                            Url = url,
                                            IpAddress = ipStr,
                                            WebPort = wPort,
                                            GamePort = gPort,
                                            Source = source
                                        };
                                    }
                                }
                            }
                        }
                        catch
                        {
                            break;
                        }
                    }
                }, token);

                await Task.WhenAny(receiveLoop, Task.Delay(timeoutMs));
            }
            catch { }

            // 2. HTTP Probe for Localhost
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(500) };
                string statusJson = await http.GetStringAsync("http://127.0.0.1:8088/api/status");
                using var doc = JsonDocument.Parse(statusJson);
                string sName = doc.RootElement.GetProperty("serverName").GetString() ?? "Local Conan Server";
                int gPort = doc.RootElement.TryGetProperty("gamePort", out var gpProp) ? gpProp.GetInt32() : 7777;

                string localUrl = "http://127.0.0.1:8088";
                lock (servers)
                {
                    servers[localUrl] = new DiscoveredServer
                    {
                        ServerName = sName,
                        Url = localUrl,
                        IpAddress = "127.0.0.1",
                        WebPort = 8088,
                        GamePort = gPort,
                        Source = "Local"
                    };
                }
            }
            catch { }

            return new List<DiscoveredServer>(servers.Values);
        }

        private static List<IPAddress> GetBroadcastAddresses()
        {
            var list = new List<IPAddress> { IPAddress.Broadcast }; // 255.255.255.255

            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    var ipProps = ni.GetIPProperties();
                    foreach (var u in ipProps.UnicastAddresses)
                    {
                        if (u.Address.AddressFamily == AddressFamily.InterNetwork && u.IPv4Mask != null)
                        {
                            byte[] ipBytes = u.Address.GetAddressBytes();
                            byte[] maskBytes = u.IPv4Mask.GetAddressBytes();

                            if (ipBytes.Length == 4 && maskBytes.Length == 4)
                            {
                                byte[] broadcastBytes = new byte[4];
                                for (int i = 0; i < 4; i++)
                                {
                                    broadcastBytes[i] = (byte)(ipBytes[i] | (maskBytes[i] ^ 255));
                                }
                                var bcastIp = new IPAddress(broadcastBytes);
                                if (!list.Contains(bcastIp))
                                {
                                    list.Add(bcastIp);
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return list;
        }
    }
}
