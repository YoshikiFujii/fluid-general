using Avalonia.Controls;
using Avalonia.Interactivity;
using fluid_general.Models;
using System.Linq;

namespace fluid_general.Avalonia.Pages;

public partial class EventSettingsDialog : Window
{
    private readonly EventConfig _event;
    public bool IsApplied { get; private set; }

    public EventSettingsDialog()
    {
        InitializeComponent();
        _event = new EventConfig();
    }

    public EventSettingsDialog(EventConfig ev)
    {
        InitializeComponent();
        _event = ev;

        // 初期値を設定
        var soundItem = TouchSoundComboBox.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(i => (string?)i.Content == ev.TouchSound);
        if (soundItem != null) TouchSoundComboBox.SelectedItem = soundItem;
        
        SameStudentErrorCheckBox.IsChecked = ev.SameStudentSetting;
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        if (TouchSoundComboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            _event.TouchSound = selectedItem.Content?.ToString() ?? "JR";
        }
        _event.SameStudentSetting = SameStudentErrorCheckBox.IsChecked == true;
        
        IsApplied = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        IsApplied = false;
        Close();
    }
}
