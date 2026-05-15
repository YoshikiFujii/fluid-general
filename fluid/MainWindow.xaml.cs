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
        Settings
    }

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ContentFrame.Navigate(new Pages.EventPage());
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
