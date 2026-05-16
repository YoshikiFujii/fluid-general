using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Layout;
using fluid_general.Avalonia.Pages;
using System.Linq;

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
    }

    public void UpdateTitle()
    {
        string baseTitle = "Fluid General";
        if (string.IsNullOrEmpty(fluid_general.Utils.AppEnv.ServerBaseUrl))
        {
            string localIp = fluid_general.Utils.NetworkUtils.GetLocalIPAddress();
            Title = string.IsNullOrEmpty(localIp) ? $"{baseTitle} - 親機モード" : $"{baseTitle} - 親機モード (IP: {localIp})";
        }
        else
        {
            Title = $"{baseTitle} - 子機モード (接続先: {fluid_general.Utils.AppEnv.ServerBaseUrl})";
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
}