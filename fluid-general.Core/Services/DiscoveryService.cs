using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace fluid_general.Services
{
    public static class DiscoveryService
    {
        private const int DiscoveryPort = 51501;
        private const string DiscoveryRequest = "DISCOVER_FLUID_PARENT";
        private const string DiscoveryResponse = "FLUID_PARENT_ALIVE";

        private static UdpClient? _udpListener;
        private static bool _isRunning;

        /// <summary>
        /// 親機として、子機からの探索リクエストを待ち受けます。
        /// </summary>
        public static void StartServer()
        {
            if (_isRunning) return;
            
            try
            {
                _udpListener = new UdpClient();
                // ポートの再利用を許可（同一PC内でのテスト用）
                _udpListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpListener.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
                
                _isRunning = true;
                
                Task.Run(async () =>
                {
                    while (_isRunning && _udpListener != null)
                    {
                        try
                        {
                            var result = await _udpListener.ReceiveAsync();
                            string message = Encoding.UTF8.GetString(result.Buffer);
                            
                            if (message == DiscoveryRequest)
                            {
                                string response = $"{DiscoveryResponse}|{Environment.MachineName}";
                                byte[] responseData = Encoding.UTF8.GetBytes(response);
                                await _udpListener.SendAsync(responseData, responseData.Length, result.RemoteEndPoint);
                            }
                        }
                        catch (ObjectDisposedException) { break; }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Discovery Server Error: {ex.Message}");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start Discovery Server: {ex.Message}");
            }
        }

        public static void StopServer()
        {
            _isRunning = false;
            _udpListener?.Close();
            _udpListener = null;
        }

        /// <summary>
        /// ネットワーク上の親機を探索します。
        /// </summary>
        /// <returns>見つかった親機のIPアドレスリスト</returns>
        public static async Task<List<string>> DiscoverParentsAsync()
        {
            var foundParents = new List<string>();
            using var client = new UdpClient();
            client.EnableBroadcast = true;
            client.Client.ReceiveTimeout = 1500;

            byte[] data = Encoding.UTF8.GetBytes(DiscoveryRequest);
            var endPoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);
            
            try
            {
                var startTime = DateTime.Now;
                var lastSendTime = DateTime.MinValue;

                // 2秒間応答を待つ（パケットロスト対策で定期的にブロードキャスト送信）
                while ((DateTime.Now - startTime).TotalMilliseconds < 2000)
                {
                    if ((DateTime.Now - lastSendTime).TotalMilliseconds > 500)
                    {
                        await client.SendAsync(data, data.Length, endPoint);
                        lastSendTime = DateTime.Now;
                    }

                    if (client.Available > 0)
                    {
                        var result = await client.ReceiveAsync();
                        string response = Encoding.UTF8.GetString(result.Buffer);
                        if (response.StartsWith(DiscoveryResponse))
                        {
                            var remoteIp = result.RemoteEndPoint.Address;
                            
                            // 自分自身を除外（自分のIPアドレス一覧に含まれているかチェック）
                            var host = Dns.GetHostEntry(Dns.GetHostName());
                            if (host.AddressList.Any(a => a.Equals(remoteIp)))
                            {
                                continue;
                            }

                            string ip = remoteIp.ToString();
                            string machineName = response.Contains("|") ? response.Split('|')[1] : "Unknown";
                            string entry = $"{machineName}|{ip}";
                            
                            if (!foundParents.Contains(entry))
                            {
                                foundParents.Add(entry);
                            }
                        }
                    }
                    await Task.Delay(50);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Discovery Client Error: {ex.Message}");
            }

            return foundParents;
        }
    }
}
