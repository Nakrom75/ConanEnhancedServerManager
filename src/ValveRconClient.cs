using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ConanServerManager
{
    public class ValveRconClient : IDisposable
    {
        public const int SERVERDATA_AUTH = 3;
        public const int SERVERDATA_AUTH_RESPONSE = 2;
        public const int SERVERDATA_EXECCOMMAND = 2;
        public const int SERVERDATA_RESPONSE_VALUE = 0;

        private readonly string _host;
        private readonly int _port;
        private readonly string _password;
        private TcpClient? _client;
        private NetworkStream? _stream;
        private int _requestId = 0;

        public ValveRconClient(string host, int port, string password)
        {
            _host = host;
            _port = port;
            _password = password;
        }

        public async Task ConnectAsync(int timeoutMs = 5000)
        {
            _client = new TcpClient();
            var connectTask = _client.ConnectAsync(_host, _port);
            var timeoutTask = Task.Delay(timeoutMs);

            if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
            {
                _client.Close();
                throw new TimeoutException($"Connection to RCON server {_host}:{_port} timed out.");
            }

            _stream = _client.GetStream();

            // Send Auth
            await SendPacketAsync(SERVERDATA_AUTH, _password);
            var (reqId, type, _) = await ReadPacketAsync();

            if (reqId == -1)
            {
                throw new UnauthorizedAccessException("RCON Authentication Failed: Invalid Password");
            }
        }

        public async Task<string> ExecuteAsync(string command)
        {
            if (_stream == null) throw new InvalidOperationException("RCON Not connected.");
            await SendPacketAsync(SERVERDATA_EXECCOMMAND, command);
            var (_, _, response) = await ReadPacketAsync();
            return response;
        }

        private async Task SendPacketAsync(int packetType, string body)
        {
            _requestId++;
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            int packetSize = 4 + 4 + bodyBytes.Length + 2;

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            writer.Write(packetSize);
            writer.Write(_requestId);
            writer.Write(packetType);
            writer.Write(bodyBytes);
            writer.Write((byte)0);
            writer.Write((byte)0);

            byte[] packet = ms.ToArray();
            await _stream!.WriteAsync(packet, 0, packet.Length);
        }

        private async Task<(int ReqId, int Type, string Body)> ReadPacketAsync()
        {
            byte[] headerBuffer = new byte[12];
            int readBytes = 0;
            while (readBytes < 12)
            {
                int r = await _stream!.ReadAsync(headerBuffer, readBytes, 12 - readBytes);
                if (r == 0) throw new EndOfStreamException("RCON Connection closed by server.");
                readBytes += r;
            }

            int size = BitConverter.ToInt32(headerBuffer, 0);
            int reqId = BitConverter.ToInt32(headerBuffer, 4);
            int type = BitConverter.ToInt32(headerBuffer, 8);

            int remaining = size - 8;
            byte[] bodyBuffer = new byte[remaining];
            readBytes = 0;
            while (readBytes < remaining)
            {
                int r = await _stream!.ReadAsync(bodyBuffer, readBytes, remaining - readBytes);
                if (r == 0) break;
                readBytes += r;
            }

            string body = Encoding.UTF8.GetString(bodyBuffer, 0, Math.Max(0, remaining - 2));
            return (reqId, type, body);
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _client?.Dispose();
        }
    }
}
