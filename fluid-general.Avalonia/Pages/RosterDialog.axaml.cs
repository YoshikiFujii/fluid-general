using Avalonia.Controls;
using Avalonia.Interactivity;
using fluid_general.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace fluid_general.Avalonia.Pages;

public partial class RosterDialog : Window
{
    public string RosterName { get; private set; } = string.Empty;
    public ObservableCollection<ColumnMapping> Mappings { get; }

    public bool IsSaved { get; private set; }

    public RosterDialog()
    {
        InitializeComponent();
        Mappings = new ObservableCollection<ColumnMapping>();
    }

    public RosterDialog(string rosterName, List<ColumnMapping>? initialMappings = null)
    {
        InitializeComponent();
        RosterName = rosterName;
        RosterNameTextBox.Text = rosterName;

        if (initialMappings == null || initialMappings.Count == 0)
        {
            Mappings = new ObservableCollection<ColumnMapping>
            {
                new ColumnMapping { Label = "ID", ColumnIndex = 1, IsFixed = true },
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

    private void AddField_Click(object? sender, RoutedEventArgs e)
    {
        Mappings.Add(new ColumnMapping { Label = "新規項目", ColumnIndex = 1 });
    }

    private void DeleteField_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is ColumnMapping mapping)
        {
            if (mapping.IsFixed)
            {
                // In Avalonia, we should use a proper message box or just ignore
                return;
            }
            Mappings.Remove(mapping);
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(RosterNameTextBox.Text))
        {
            return;
        }

        if (Mappings.Any(m => string.IsNullOrWhiteSpace(m.Label)))
        {
            return;
        }

        RosterName = RosterNameTextBox.Text;
        IsSaved = true;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        IsSaved = false;
        Close();
    }
}
