using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using ModernWpf.Controls;

namespace fluid_general.Pages
{
    public partial class DisplaySettingsDialog : ContentDialog
    {
        public class FieldItem
        {
            public string Name { get; set; } = string.Empty;
            public bool IsSelected { get; set; }
        }

        public ObservableCollection<FieldItem> FieldItems { get; set; } = new ObservableCollection<FieldItem>();

        public List<string> SelectedColumns => FieldItems.Where(i => i.IsSelected).Select(i => i.Name).ToList();

        public DisplaySettingsDialog(List<string> allKeys, List<string> currentSelection)
        {
            InitializeComponent();

            // まず現在の選択項目を順番通りに追加
            foreach (var key in currentSelection)
            {
                if (allKeys.Contains(key))
                {
                    FieldItems.Add(new FieldItem { Name = key, IsSelected = true });
                }
            }

            // 残りの項目を追加
            foreach (var key in allKeys)
            {
                if (!currentSelection.Contains(key))
                {
                    FieldItems.Add(new FieldItem { Name = key, IsSelected = false });
                }
            }

            FieldsListBox.ItemsSource = FieldItems;
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            int index = FieldsListBox.SelectedIndex;
            if (index > 0)
            {
                var item = FieldItems[index];
                FieldItems.RemoveAt(index);
                FieldItems.Insert(index - 1, item);
                FieldsListBox.SelectedIndex = index - 1;
            }
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            int index = FieldsListBox.SelectedIndex;
            if (index >= 0 && index < FieldItems.Count - 1)
            {
                var item = FieldItems[index];
                FieldItems.RemoveAt(index);
                FieldItems.Insert(index + 1, item);
                FieldsListBox.SelectedIndex = index + 1;
            }
        }
    }
}
