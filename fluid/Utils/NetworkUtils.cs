using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace fluid_general.Utils
{
    public static class NetworkUtils
    {
        /// <summary>
        /// ローカルネットワーク内のIPv4アドレスを取得します。
        /// </summary>
        /// <returns>IPアドレスの文字列。取得できない場合は空文字列。</returns>
        public static string GetLocalIPAddress()
        {
            try
            {
                // アクティブなネットワークインターフェースからIPv4アドレスを取得
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var ip = host.AddressList.FirstOrDefault(ip => 
                    ip.AddressFamily == AddressFamily.InterNetwork && 
                    !IPAddress.IsLoopback(ip) &&
                    !ip.ToString().StartsWith("169.254")); // リンクローカルアドレスを除外

                return ip?.ToString() ?? "";
            }
            catch
            {
                return "";
            }
        }
    }
}
