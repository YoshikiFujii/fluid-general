using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ui = ModernWpf.Controls;


namespace fluid_general
{
    public enum NaviIcon
    {
        Event,
        Roster,
        Library,
        About,
        Settings,
        ConnectSession,
        DataMigration
    }

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => UpdateTitle();
            App.ConnectionModeChanged += (s, e) => Dispatcher.Invoke(UpdateTitle);
            ContentFrame.Navigate(new Pages.EventPage());
        }

        public void UpdateTitle()
        {
            string baseTitle = "Fluid General";
            if (string.IsNullOrEmpty(App.ServerBaseUrl))
            {
                string localIp = Utils.NetworkUtils.GetLocalIPAddress();
                int connectionCount = App.GetActiveConnectionCount();
                string connectionText = connectionCount > 0 ? $" (接続数: {connectionCount})" : "";
                this.Title = string.IsNullOrEmpty(localIp) ? $"{baseTitle} - 親機モード{connectionText}" : $"{baseTitle} - 親機モード (IP: {localIp}){connectionText}";
                
                // 親機モードは薄い赤色にする
                var parentBrush = new SolidColorBrush(Color.FromRgb(255, 235, 235));
                ModernWpf.Controls.TitleBar.SetBackground(this, parentBrush);
            }
            else
            {
                this.Title = $"{baseTitle} - 子機モード (接続先: {App.ServerBaseUrl})";
                
                // 子機モードはデフォルトの色
                ModernWpf.Controls.TitleBar.SetBackground(this, null);
            }
        }

        private void NaviView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem selectedItem)
            {
                NaviIcon tag = (NaviIcon)selectedItem.Tag;

                switch (tag)
                {
                    case NaviIcon.Event:
                        ContentFrame.Navigate(new Pages.EventPage());
                        break;
                    case NaviIcon.Roster:
                        ContentFrame.Navigate(new Pages.RosterPage());
                        break;
                    case NaviIcon.Library:
                        ContentFrame.Navigate(new Pages.guide());
                        break;
                    case NaviIcon.About:
                        ContentFrame.Navigate(new Pages.aboutPage());
                        break;
                    case NaviIcon.ConnectSession:
                        ContentFrame.Navigate(new Pages.ConnectSessionPage());
                        break;
                    case NaviIcon.DataMigration:
                        ContentFrame.Navigate(new Pages.DataMigrationPage());
                        break;
                    default:
                        break;
                }
            }
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            // ページ遷移後に何か処理が必要な場合はここに記述
        }
    }
}
