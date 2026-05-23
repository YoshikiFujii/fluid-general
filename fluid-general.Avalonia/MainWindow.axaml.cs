using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Layout;
using fluid_general.Avalonia.Pages;
using System.Linq;
using System.Threading.Tasks;

namespace fluid_general.Avalonia;

public partial class MainWindow : Window
{
    private EventPage? _eventPage;
    private RosterPage? _rosterPage;

    public MainWindow()
    {
        InitializeComponent();
        
        UpdateTitle();
        fluid_general.Utils.AppEnv.ConnectionModeChanged += (s, e) => global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(UpdateTitle);

        // Default page
        NavigateTo("Event");

        // アプリ起動時の自動アップデート確認
        CheckForUpdatesOnStartupAsync();
    }

    private void Header_PointerPressed(object? sender, global::Avalonia.Input.PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    public void UpdateTitle()
    {
        string baseTitle = "Fluid General";
        string localIp = fluid_general.Utils.NetworkUtils.GetLocalIPAddress();

        if (string.IsNullOrEmpty(fluid_general.Utils.AppEnv.ServerBaseUrl))
        {
            Title = string.IsNullOrEmpty(localIp) ? $"{baseTitle} - 親機モード" : $"{baseTitle} - 親機モード (IP: {localIp})";
            if (IpAddressText != null)
            {
                IpAddressText.Text = string.IsNullOrEmpty(localIp) ? "親機モード" : $"親機 IP: {localIp}";
            }
        }
        else
        {
            string serverUrl = fluid_general.Utils.AppEnv.ServerBaseUrl;
            Title = $"{baseTitle} - 子機モード (接続先: {serverUrl})";
            if (IpAddressText != null)
            {
                IpAddressText.Text = string.IsNullOrEmpty(localIp) 
                    ? $"子機モード (接続先: {serverUrl})" 
                    : $"子機 IP: {localIp}\n(接続先: {serverUrl})";
            }
        }
    }

    private void NavigateTo(string tag)
    {
        switch (tag)
        {
            case "Event":
                _eventPage ??= new EventPage();
                ContentFrame.Content = _eventPage;
                PageHeader.Text = "イベント";
                break;
            case "Roster":
                _rosterPage ??= new RosterPage();
                ContentFrame.Content = _rosterPage;
                PageHeader.Text = "名簿";
                break;
            case "Connect":
                ContentFrame.Content = new ConnectPage();
                PageHeader.Text = "セッションに接続";
                break;
            default:
                ContentFrame.Content = new TextBlock 
                { 
                    Text = $"{tag} ページは準備中です。", 
                    Foreground = Brushes.LightGray,
                    HorizontalAlignment = HorizontalAlignment.Center, 
                    VerticalAlignment = VerticalAlignment.Center 
                };
                PageHeader.Text = tag;
                break;
        }
    }

    private void NaviList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (NaviList.SelectedItem is ListBoxItem item && item.Tag is string tag)
        {
            NavigateTo(tag);
        }
    }

    private async void CheckForUpdatesOnStartupAsync()
    {
        try
        {
            var currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
            // var currentVersion = "0.0.1"; // アップデート機能テスト用
            
            // メイン画面の読み込みが完全に完了するまで少し待つ
            await Task.Delay(1000);
            
            var result = await fluid_general.Services.UpdateCheckerService.CheckForUpdatesAsync(currentVersion);
            if (result.IsNewVersionAvailable)
            {
                var dialog = new UpdateDialog(result);
                await dialog.ShowDialog(this);
            }
        }
        catch (System.Exception ex)
        {
            fluid_general.Utils.AppEnv.LogError(ex);
        }
    }
}