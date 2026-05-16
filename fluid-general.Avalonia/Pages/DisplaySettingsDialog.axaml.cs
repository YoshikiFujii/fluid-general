using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace fluid_general.Avalonia.Pages;

public class FieldItem : INotifyPropertyChanged
{
    private string _name = "";
    private bool _isSelected;

    public string Name 
    { 
        get => _name; 
        set { _name = value; OnPropertyChanged(nameof(Name)); } 
    }
    public bool IsSelected 
    { 
        get => _isSelected; 
        set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); } 
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class DisplaySettingsDialog : Window
{
    public ObservableCollection<FieldItem> FieldItems { get; } = new();
    public bool IsApplied { get; private set; }

    public DisplaySettingsDialog()
    {
        InitializeComponent();
    }

    public DisplaySettingsDialog(List<string> allKeys, List<string> currentSelection)
    {
        InitializeComponent();

        // 選択済みの項目を順序通りに追加
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

    private void OnMoveUpClick(object sender, RoutedEventArgs e)
    {
        int index = FieldsListBox.SelectedIndex;
        if (index > 0)
        {
            FieldItems.Move(index, index - 1);
            FieldsListBox.SelectedIndex = index - 1;
        }
    }

    private void OnMoveDownClick(object sender, RoutedEventArgs e)
    {
        int index = FieldsListBox.SelectedIndex;
        if (index >= 0 && index < FieldItems.Count - 1)
        {
            FieldItems.Move(index, index + 1);
            FieldsListBox.SelectedIndex = index + 1;
        }
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        IsApplied = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        IsApplied = false;
        Close();
    }

    public List<string> GetSelectedColumns()
    {
        return FieldItems.Where(i => i.IsSelected).Select(i => i.Name).ToList();
    }
}
