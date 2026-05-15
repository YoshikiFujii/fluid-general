using System.Windows;

namespace fluid_general.Pages
{
    /// <summary>
    /// ExportDialog.xaml の相互作用ロジック
    /// </summary>
    public partial class ExportDialog : ModernWpf.Controls.ContentDialog
    {
        private Models.EventConfig _eventConfig;

        public ExportDialog(Models.EventConfig eventConfig)
        {
            _eventConfig = eventConfig;
            InitializeComponent();
        }
        private void OutputListClick(object sender, RoutedEventArgs e)
        {
            ExportListWindow exportListWindow = new ExportListWindow(_eventConfig);
            exportListWindow.Show();
        }
        private void OutputPDFClick(object sender, RoutedEventArgs e)
        {
            ExportPDFWindow exportPDFWindow = new ExportPDFWindow(_eventConfig);
            exportPDFWindow.Show();
        }
    }
}
