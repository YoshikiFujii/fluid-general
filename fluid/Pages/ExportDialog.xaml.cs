using System.Windows;

namespace fluid_general.Pages
{
    /// <summary>
    /// ExportDialog.xaml の相互作用ロジック
    /// </summary>
    public partial class ExportDialog : ModernWpf.Controls.ContentDialog
    {
        private string CurrentEvent;
        private string eventFilePath;
        public ExportDialog(string currentEvent)
        {
            CurrentEvent = currentEvent;
            eventFilePath = System.IO.Path.Combine(fluid_general.App.AppDataPath, "data", $"{currentEvent}.xml");
            InitializeComponent();
        }
        private void OutputListClick(object sender, RoutedEventArgs e)
        {
            ExportListWindow exportListWindow = new ExportListWindow(eventFilePath, CurrentEvent);
            exportListWindow.Show();
        }
        private void OutputPDFClick(object sender, RoutedEventArgs e)
        {
            ExportPDFWindow exportPDFWindow = new ExportPDFWindow(eventFilePath, CurrentEvent);
            exportPDFWindow.Show();
        }
    }
}
