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
using System.Windows.Shapes;

namespace fluid_general.Pages
{
    /// <summary>
    /// ProgressWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class ProgressWindow : Window
    {
        public ProgressWindow()
        {
            InitializeComponent();
        }
        public void UpdateProgress(int progress)
        {
            ProgressBar.Value = progress;
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // キャンセル処理を実行
            this.Close();
        }
    }
}
