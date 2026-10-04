using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConanServerManager
{
    public class SteamServerInfo
    {
        public bool IsOnline { get; set; }
        public string ServerName { get; set; } = "";
        public string Map { get; set; } = "";
        public string GameFolder { get; set; } = "";
        public string GameName { get; set; } = "";
        public int Players { get; set; }
        public int MaxPlayers { get; set; }
        public int PingMs { get; set; }
        public string ErrorMessage { get; set; } = "";

        public override string ToString()
        {
            if (!IsOnline) return "OFFLINE";
            return $"{ServerName} | Map: {Map} | Players: {Players}/{MaxPlayers} ({PingMs}ms)";
        }
    }

    public static class SteamQueryHelper
    {
        // Valve A2S_INFO query request packet header
        private static readonly byte[] A2S_INFO_PAYLOAD = new byte[]
        {
            0xFF, 0xFF, 0xFF, 0xFF,
            0x54, // 'T' = A2S_INFO
            0x53, 0x6F, 0x75, 0x72, 0x63, 0x65, 0x20, 0x45, 0x6E, 0x67, 0x69, 0x6E, 0x65, 0x20, 0x51, 0x75, 0x65, 0x72, 0x79, 0x00 // "Source Engine Query\0"
        };

        public static async Task<SteamServerInfo> QueryA2sInfoAsync(string host, int queryPort, int timeoutMs = 2500)
        {
            var info = new SteamServerInfo();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                using var client = new UdpClient();
                client.Client.SendTimeout = timeoutMs;
                client.Client.ReceiveTimeout = timeoutMs;

                IPAddress ip;
                if (!IPAddress.TryParse(host, out ip!))
                {
                    var addresses = await Dns.GetHostAddressesAsync(host);
                    if (addresses.Length == 0)
                    {
                        info.ErrorMessage = $"Could not resolve host: {host}";
                        return info;
                    }
                    ip = addresses[0];
                }

                var ep = new IPEndPoint(ip, queryPort);

                // 1. Send initial A2S_INFO request
                await client.SendAsync(A2S_INFO_PAYLOAD, A2S_INFO_PAYLOAD.Length, ep);

                using var cts = new CancellationTokenSource(timeoutMs);
                var receiveTask = client.ReceiveAsync();
                var completedTask = await Task.WhenAny(receiveTask, Task.Delay(timeoutMs, cts.Token));

                if (completedTask != receiveTask)
                {
                    info.ErrorMessage = "Query timed out.";
                    return info;
                }

                var result = await receiveTask;
                byte[] data = result.Buffer;

                if (data.Length < 5 || data[0] != 0xFF || data[1] != 0xFF || data[2] != 0xFF || data[3] != 0xFF)
                {
                    info.ErrorMessage = "Invalid packet header.";
                    return info;
                }

                // 2. Handle modern Steam Challenge Response: Type 0x41 ('A')
                if (data[4] == 0x41 && data.Length >= 9)
                {
                    // Construct challenged packet: A2S_INFO payload + 4 challenge bytes
                    byte[] challengedPayload = new byte[A2S_INFO_PAYLOAD.Length + 4];
                    Buffer.BlockCopy(A2S_INFO_PAYLOAD, 0, challengedPayload, 0, A2S_INFO_PAYLOAD.Length);
                    Buffer.BlockCopy(data, 5, challengedPayload, A2S_INFO_PAYLOAD.Length, 4);

                    await client.SendAsync(challengedPayload, challengedPayload.Length, ep);

                    using var ctsChallenge = new CancellationTokenSource(timeoutMs);
                    var challengeReceiveTask = client.ReceiveAsync();
                    var challengeCompleted = await Task.WhenAny(challengeReceiveTask, Task.Delay(timeoutMs, ctsChallenge.Token));

                    if (challengeCompleted != challengeReceiveTask)
                    {
                        info.ErrorMessage = "Challenge response timed out.";
                        return info;
                    }

                    var challengeResult = await challengeReceiveTask;
                    data = challengeResult.Buffer;
                }

                sw.Stop();
                info.PingMs = (int)sw.ElapsedMilliseconds;

                // 3. Process A2S_INFO Response: Type 0x49 ('I')
                if (data.Length >= 5 && data[0] == 0xFF && data[1] == 0xFF && data[2] == 0xFF && data[3] == 0xFF)
                {
                    info.IsOnline = true;

                    if (data[4] == 0x49 && data.Length > 6)
                    {
                        int offset = 5;
                        byte protocol = data[offset++];
                        info.ServerName = ReadNullTerminatedString(data, ref offset);
                        info.Map = ReadNullTerminatedString(data, ref offset);
                        info.GameFolder = ReadNullTerminatedString(data, ref offset);
                        info.GameName = ReadNullTerminatedString(data, ref offset);

                        if (offset + 2 <= data.Length)
                        {
                            offset += 2; // Steam App ID
                        }

                        if (offset < data.Length) info.Players = data[offset++];
                        if (offset < data.Length) info.MaxPlayers = data[offset++];
                    }
                    else
                    {
                        info.ServerName = "Conan Dedicated Server";
                    }

                    return info;
                }

                info.ErrorMessage = "Unexpected response type.";
                return info;
            }
            catch (Exception ex)
            {
                info.ErrorMessage = ex.Message;
                return info;
            }
        }

        private static string ReadNullTerminatedString(byte[] data, ref int offset)
        {
            if (offset >= data.Length) return "";
            int start = offset;
            while (offset < data.Length && data[offset] != 0)
            {
                offset++;
            }
            string str = Encoding.UTF8.GetString(data, start, offset - start);
            if (offset < data.Length && data[offset] == 0) offset++;
            return str;
        }
    }
}
