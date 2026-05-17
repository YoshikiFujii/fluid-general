using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace fluid_general.Services
{
    public static class DiscoveryService
    {
        private const int DiscoveryPort = 5001;
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
        /// ネットワーク上の親機を探索します（すべての有効なIPv4インターフェースから並列でブロードキャストを送信）。
        /// </summary>
        /// <returns>見つかった親機のIPアドレスリスト</returns>
        public static async Task<List<string>> DiscoverParentsAsync()
        {
            var foundParents = new List<string>();
            var tasks = new List<Task<List<string>>>();
            var ipv4Addresses = new List<(IPAddress Address, IPAddress SubnetMask)>();

            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var ni in interfaces)
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    
                    var ipProps = ni.GetIPProperties();
                    foreach (var unicast in ipProps.UnicastAddresses)
                    {
                        if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            ipv4Addresses.Add((unicast.Address, unicast.IPv4Mask));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get network interfaces for discovery: {ex.Message}");
            }

            // 有効なインターフェース情報が得られなかった場合のフォールバックとしてIPAddress.Anyを使用する
            if (ipv4Addresses.Count == 0)
            {
                ipv4Addresses.Add((IPAddress.Any, IPAddress.Any));
            }

            byte[] requestData = Encoding.UTF8.GetBytes(DiscoveryRequest);

            foreach (var (localIp, subnetMask) in ipv4Addresses)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var resultsOnInterface = new List<string>();
                    using var client = new UdpClient();
                    
                    try
                    {
                        // 各インターフェースのIPにソケットを明示的バインドしてブロードキャストを強制
                        client.Client.Bind(new IPEndPoint(localIp, 0));
                        client.EnableBroadcast = true;
                        client.Client.ReceiveTimeout = 1500;

                        // 1. グローバルブロードキャスト (255.255.255.255) 送信
                        var globalEndPoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);
                        await client.SendAsync(requestData, requestData.Length, globalEndPoint);

                        // 2. サブネットブロードキャスト送信（利用可能かつ無効値でない場合）
                        if (subnetMask != null && !subnetMask.Equals(IPAddress.Any) && !subnetMask.Equals(IPAddress.None))
                        {
                            try
                            {
                                var ipBytes = localIp.GetAddressBytes();
                                var maskBytes = subnetMask.GetAddressBytes();
                                var broadcastBytes = new byte[ipBytes.Length];
                                for (int i = 0; i < ipBytes.Length; i++)
                                {
                                    broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
                                }
                                var subnetBroadcast = new IPAddress(broadcastBytes);
                                var subnetEndPoint = new IPEndPoint(subnetBroadcast, DiscoveryPort);
                                await client.SendAsync(requestData, requestData.Length, subnetEndPoint);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Subnet broadcast send failed on interface {localIp}: {ex.Message}");
                            }
                        }

                        // 応答の受信ループ（2秒間待機）
                        var startTime = DateTime.Now;
                        while ((DateTime.Now - startTime).TotalMilliseconds < 2000)
                        {
                            if (client.Available > 0)
                            {
                                var result = await client.ReceiveAsync();
                                string response = Encoding.UTF8.GetString(result.Buffer);
                                if (response.StartsWith(DiscoveryResponse))
                                {
                                    var remoteIp = result.RemoteEndPoint.Address;
                                    
                                    // 自分自身を除外
                                    var host = Dns.GetHostEntry(Dns.GetHostName());
                                    if (host.AddressList.Any(a => a.Equals(remoteIp)))
                                    {
                                        continue;
                                    }

                                    string ip = remoteIp.ToString();
                                    string machineName = response.Contains("|") ? response.Split('|')[1] : "Unknown";
                                    string entry = $"{machineName}|{ip}";
                                    
                                    if (!resultsOnInterface.Contains(entry))
                                    {
                                        resultsOnInterface.Add(entry);
                                    }
                                }
                            }
                            await Task.Delay(50);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Interface {localIp} Discovery Error: {ex.Message}");
                    }
                    return resultsOnInterface;
                }));
            }

            try
            {
                var allResults = await Task.WhenAll(tasks);
                foreach (var list in allResults)
                {
                    foreach (var entry in list)
                    {
                        if (!foundParents.Contains(entry))
                        {
                            foundParents.Add(entry);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Discovery parallel wait error: {ex.Message}");
            }

            return foundParents;
        }
    }
}
