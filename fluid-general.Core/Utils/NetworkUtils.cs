using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace fluid_general.Utils
{
    public static class NetworkUtils
    {
        /// <summary>
        /// ローカルネットワーク内のIPv4アドレスを取得します（ゲートウェイや仮想アダプタの有無を考慮した高度な優先度選定）。
        /// </summary>
        /// <returns>IPアドレスの文字列。取得できない場合は空文字列。</returns>
        public static string GetLocalIPAddress()
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                
                // アクティブかつ、ループバックやトンネルではない物理系のインターフェースを優先度判定
                var activeInterface = interfaces
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up && 
                                 ni.NetworkInterfaceType != NetworkInterfaceType.Loopback && 
                                 ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    .OrderByDescending(ni => {
                        int score = 0;
                        
                        // Wi-Fiやイーサネット（有線）を優先
                        if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) score += 20;
                        if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet) score += 10;
                        
                        var properties = ni.GetIPProperties();
                        
                        // デフォルトゲートウェイ（ルーター）が設定されているインターフェースを最優先
                        if (properties.GatewayAddresses.Any(g => !g.Address.Equals(IPAddress.Any) && !g.Address.Equals(IPAddress.None)))
                        {
                            score += 100;
                        }
                        
                        // 仮想ネットワークアダプタ（Docker, WSL, VPN, Hyper-V, VirtualBox, VMware 等）は大幅減点
                        string desc = ni.Description.ToLower();
                        string name = ni.Name.ToLower();
                        if (desc.Contains("virtual") || desc.Contains("pseudo") || desc.Contains("vpn") ||
                            desc.Contains("docker") || desc.Contains("wsl") || desc.Contains("hyper-v") ||
                            desc.Contains("vbox") || desc.Contains("vmware") || desc.Contains("virtualbox") ||
                            name.Contains("vethernet") || name.Contains("wsl") || name.Contains("docker"))
                        {
                            score -= 50;
                        }
                        
                        return score;
                    })
                    .FirstOrDefault();

                if (activeInterface != null)
                {
                    var ip = activeInterface.GetIPProperties().UnicastAddresses
                        .FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork && 
                                             !IPAddress.IsLoopback(u.Address) && 
                                             !u.Address.ToString().StartsWith("169.254"))?
                        .Address;
                    if (ip != null)
                    {
                        return ip.ToString();
                    }
                }
                
                // フォールバック：従来通りのDNS取得
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var fallbackIp = host.AddressList.FirstOrDefault(ipAddr => 
                    ipAddr.AddressFamily == AddressFamily.InterNetwork && 
                    !IPAddress.IsLoopback(ipAddr) &&
                    !ipAddr.ToString().StartsWith("169.254"));
                
                return fallbackIp?.ToString() ?? "";
            }
            catch
            {
                return "";
            }
        }
    }
}
