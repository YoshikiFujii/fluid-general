using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace fluid_general.Pages
{
    public partial class ConnectSessionPage : Page
    {
        public ConnectSessionPage()
        {
            InitializeComponent();
            
            if (!string.IsNullOrEmpty(App.ServerBaseUrl))
            {
                ServerAddressTextBox.Text = App.ServerBaseUrl;
                StatusTextBlock.Text = "現在、子機モードで動作しています。";
                StatusTextBlock.Foreground = new SolidColorBrush(Colors.Green);
                DisconnectButton.IsEnabled = true;
            }
            else
            {
                StatusTextBlock.Text = "現在、親機モード（ローカル接続）で動作しています。";
                DisconnectButton.IsEnabled = false;
            }
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            await ProcessConnectionAsync(ServerAddressTextBox.Text.Trim());
        }

        private async Task ProcessConnectionAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || url == "http://")
            {
                StatusTextBlock.Text = "有効なURLを入力してください。";
                StatusTextBlock.Foreground = new SolidColorBrush(Colors.Red);
                return;
            }

            if (!url.EndsWith("/")) url += "/";

            ConnectButton.IsEnabled = false;
            StatusTextBlock.Text = "接続テスト中...";
            StatusTextBlock.Foreground = new SolidColorBrush(Colors.Black);

            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                client.DefaultRequestHeaders.Add("X-Fluid-MachineName", Environment.MachineName);
                
                // 親機のAPIを叩いて生存確認
                var response = await client.GetAsync($"{url}api/members");

                if (response.IsSuccessStatusCode)
                {
                    StatusTextBlock.Text = "接続成功！これ以降の認証は親機に送信されます。";
                    StatusTextBlock.Foreground = new SolidColorBrush(Colors.Green);
                    ServerAddressTextBox.Text = url;
                    App.ServerBaseUrl = url;
                    UpdateMainWindowTitle();
                    DisconnectButton.IsEnabled = true;
                }
                else
                {
                    StatusTextBlock.Text = $"接続に失敗しました (ステータスコード: {response.StatusCode})";
                    StatusTextBlock.Foreground = new SolidColorBrush(Colors.Red);
                    App.ServerBaseUrl = null;
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"エラー: {ex.Message}";
                StatusTextBlock.Foreground = new SolidColorBrush(Colors.Red);
                App.ServerBaseUrl = null;
            }
            finally
            {
                ConnectButton.IsEnabled = true;
                UpdateMainWindowTitle();
            }
        }

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            App.ServerBaseUrl = null;
            ServerAddressTextBox.Text = "http://";
            StatusTextBlock.Text = "接続を解除しました。親機モード（ローカル接続）に戻ります。";
            StatusTextBlock.Foreground = new SolidColorBrush(Colors.Black);
            DisconnectButton.IsEnabled = false;

            UpdateMainWindowTitle();
        }

        private void UpdateMainWindowTitle()
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.UpdateTitle();
            }
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchButton.IsEnabled = false;
            SearchStatusTextBlock.Text = "親機を探索中...";
            ParentListBox.Items.Clear();

            var parents = await Services.DiscoveryService.DiscoverParentsAsync();

            if (parents.Count == 0)
            {
                SearchStatusTextBlock.Text = "親機は見つかりませんでした。";
            }
            else
            {
                foreach (var entry in parents)
                {
                    var parts = entry.Split('|');
                    string machineName = parts[0];
                    string ip = parts[1];
                    ParentListBox.Items.Add($"{machineName} ({ip})");
                }
                SearchStatusTextBlock.Text = $"{parents.Count} 台の親機が見つかりました。";
            }

            SearchButton.IsEnabled = true;
        }

        private async void ParentListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ParentListBox.SelectedItem is string selectedText)
            {
                int start = selectedText.LastIndexOf('(');
                int end = selectedText.LastIndexOf(')');
                if (start != -1 && end != -1)
                {
                    string ip = selectedText.Substring(start + 1, end - start - 1);
                    string url = $"http://{ip}:5010/";
                    await ProcessConnectionAsync(url);
                }
            }
        }
    }
}
