using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace fluid_general.Avalonia.Pages;

public partial class EventDialog : Window
{
    public string EventName { get; private set; } = string.Empty;
    public DateTime EventDate { get; private set; } = DateTime.Now;
    public string SelectedRoster { get; private set; } = string.Empty;
    public bool IsSaved { get; private set; }

    public EventDialog()
    {
        InitializeComponent();
        EventDatePicker.SelectedDate = DateTime.Now;
        _ = LoadRostersAsync();
    }

    private async Task LoadRostersAsync()
    {
        try
        {
            var service = App.GetDataService();
            var members = await service.GetMembersAsync();
            var rosters = members
                .Select(m => m.RosterName)
                .Where(r => !string.IsNullOrEmpty(r))
                .Distinct()
                .ToList();
            
            RosterComboBox.ItemsSource = rosters;
            if (rosters.Count > 0)
            {
                RosterComboBox.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            fluid_general.Utils.AppEnv.LogError(ex);
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EventNameTextBox.Text))
        {
            return;
        }

        EventName = EventNameTextBox.Text;
        
        // DateTimeOffset? から DateTime への変換
        if (EventDatePicker.SelectedDate.HasValue)
        {
            // Avalonia のバージョンによって DateTime か DateTimeOffset かが異なる場合があるため
            // 一旦 object で受けてから適切に変換します
            object val = EventDatePicker.SelectedDate.Value;
            if (val is DateTimeOffset dto) EventDate = dto.DateTime;
            else if (val is DateTime dt) EventDate = dt;
        }
        else
        {
            EventDate = DateTime.Now;
        }

        SelectedRoster = RosterComboBox.SelectedItem as string ?? string.Empty;
        
        IsSaved = true;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        IsSaved = false;
        Close();
    }
}
