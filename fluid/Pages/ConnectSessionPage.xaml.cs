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
            ConnectButton.IsEnabled = false;
            StatusTextBlock.Text = "接続テスト中...";
            StatusTextBlock.Foreground = new SolidColorBrush(Colors.Black);

            string url = ServerAddressTextBox.Text.Trim();
            if (!url.EndsWith("/"))
            {
                url += "/";
            }

            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                
                // 親機のAPIを叩いて生存確認 (GET /api/members は空でも200OKを返すはず)
                var response = await client.GetAsync($"{url}api/members");
                
                if (response.IsSuccessStatusCode)
                {
                    StatusTextBlock.Text = "接続成功！これ以降の認証は親機に送信されます。";
                    StatusTextBlock.Foreground = new SolidColorBrush(Colors.Green);
                    
                    // アプリケーション全体でこのURLを使用するように設定
                    App.ServerBaseUrl = url;

                    // タイトルを更新
                    UpdateMainWindowTitle();
                    DisconnectButton.IsEnabled = true;
                }
                else
                {
                    StatusTextBlock.Text = $"接続に失敗しました (ステータスコード: {response.StatusCode})";
                    StatusTextBlock.Foreground = new SolidColorBrush(Colors.Red);
                    App.ServerBaseUrl = null;
                    UpdateMainWindowTitle();
                    DisconnectButton.IsEnabled = false;
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
                // MainWindowに公開されたメソッドを呼ぶか、直接Titleを操作する
                // 先ほどMainWindowにUpdateTitleを追加したので、それをリフレクションで呼ぶか
                // 直接操作する
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
                    // entry は "MachineName|IP" 形式
                    var parts = entry.Split('|');
                    string machineName = parts[0];
                    string ip = parts[1];
                    ParentListBox.Items.Add($"{machineName} ({ip})");
                }
                SearchStatusTextBlock.Text = $"{parents.Count} 台の親機が見つかりました。";
            }

            SearchButton.IsEnabled = true;
        }

        private void ParentListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ParentListBox.SelectedItem is string selectedText)
            {
                // "MachineName (IP)" 形式から IP を抽出
                int start = selectedText.LastIndexOf('(');
                int end = selectedText.LastIndexOf(')');
                if (start != -1 && end != -1)
                {
                    string ip = selectedText.Substring(start + 1, end - start - 1);
                    ServerAddressTextBox.Text = $"http://{ip}:5000/";
                }
            }
        }
    }
}
