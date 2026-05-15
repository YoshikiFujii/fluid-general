using System.IO;
using System.Linq;
using System.Windows;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace fluid_general.Pages
{
    public partial class RosterDialog : ModernWpf.Controls.ContentDialog
    {
        public string rostername { get; private set; } = string.Empty;
        public ObservableCollection<ColumnMapping> Mappings { get; }

        public RosterDialog(string rostername, List<ColumnMapping>? initialMappings = null)
        {
            InitializeComponent();
            this.rostername = rostername;
            RosterNameTextBox.Text = rostername;

            if (initialMappings == null || initialMappings.Count == 0)
            {
                Mappings = new ObservableCollection<ColumnMapping>
                {
                    new ColumnMapping { Label = "名前", ColumnIndex = 6, IsFixed = true },
                    new ColumnMapping { Label = "名前（かな）", ColumnIndex = 7 },
                    new ColumnMapping { Label = "部屋番号", ColumnIndex = 1 },
                    new ColumnMapping { Label = "学籍番号", ColumnIndex = 9, IsFixed = true },
                    new ColumnMapping { Label = "性別", ColumnIndex = 4 },
                    new ColumnMapping { Label = "学科", ColumnIndex = 10 },
                    new ColumnMapping { Label = "学年", ColumnIndex = 5 }
                };
            }
            else
            {
                Mappings = new ObservableCollection<ColumnMapping>(initialMappings);
            }

            ColumnMappingsItemsControl.ItemsSource = Mappings;
        }

        private void AddField_Click(object sender, RoutedEventArgs e)
        {
            Mappings.Add(new ColumnMapping { Label = "新規項目", ColumnIndex = 1 });
        }

        private void DeleteField_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is ColumnMapping mapping)
            {
                if (mapping.IsFixed)
                {
                    MessageBox.Show("この項目は削除できません。");
                    return;
                }
                Mappings.Remove(mapping);
            }
        }

        private void ContentDialog_PrimaryButtonClick(ModernWpf.Controls.ContentDialog sender, ModernWpf.Controls.ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(RosterNameTextBox.Text))
            {
                MessageBox.Show("名簿名を入力してください。");
                args.Cancel = true;
                return;
            }

            if (Mappings.Any(m => string.IsNullOrWhiteSpace(m.Label)))
            {
                MessageBox.Show("項目の名前をすべて入力してください。");
                args.Cancel = true;
                return;
            }

            rostername = RosterNameTextBox.Text;
        }
    }

    public class ColumnMapping
    {
        public string Label { get; set; } = string.Empty;
        public int ColumnIndex { get; set; } = 1;
        public bool IsFixed { get; set; } = false;
    }
}
