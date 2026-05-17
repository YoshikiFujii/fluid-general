using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using fluid_general.Models;

namespace fluid_general.Avalonia.Pages;

public partial class EventDialog : Window
{
    public string EventName { get; private set; } = string.Empty;
    public DateTime EventDate { get; private set; } = DateTime.Now;
    public string SelectedRoster { get; private set; } = string.Empty;
    public bool IsSaved { get; private set; }

    private readonly EventConfig? _editingEvent;
    private string _rosterToSelect = string.Empty;

    public EventDialog()
    {
        InitializeComponent();
        EventDatePicker.SelectedDate = DateTime.Now;
        _ = LoadRostersAsync();
    }

    public EventDialog(EventConfig editingEvent)
    {
        InitializeComponent();
        _editingEvent = editingEvent;
        EventDatePicker.SelectedDate = editingEvent.EventDate;
        _rosterToSelect = editingEvent.RosterName;
        
        _ = LoadRostersAsync();
        
        // Populate fields
        EventNameTextBox.Text = editingEvent.EventName;
        DialogTitle.Text = "イベントの編集";
        Title = "イベントの編集";
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
                if (!string.IsNullOrEmpty(_rosterToSelect) && rosters.Contains(_rosterToSelect))
                {
                    RosterComboBox.SelectedItem = _rosterToSelect;
                }
                else
                {
                    RosterComboBox.SelectedIndex = 0;
                }
            }
        }
        catch (Exception ex)
        {
            fluid_general.Utils.AppEnv.LogError(ex);
        }
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EventNameTextBox.Text))
        {
            return;
        }

        var name = EventNameTextBox.Text.Trim();
        try
        {
            var service = App.GetDataService();
            var events = await service.GetEventsAsync();
            if (events.Any(ev => ev.EventName == name && (_editingEvent == null || ev.Id != _editingEvent.Id)))
            {
                EventNameTextBox.Text = string.Empty;
                EventNameTextBox.Watermark = "同名のイベントが存在します！";
                return;
            }
        }
        catch (Exception ex)
        {
            fluid_general.Utils.AppEnv.LogError(ex);
        }

        EventName = name;
        
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
