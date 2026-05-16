using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using fluid_general.Models;
using fluid_general.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace fluid_general.Avalonia.Pages;

public partial class EventPage : UserControl
{
    public EventPage()
    {
        InitializeComponent();
        _ = LoadEventsAsync();
        fluid_general.Utils.AppEnv.ConnectionModeChanged += (s, e) => _ = LoadEventsAsync();

        // 子機モードの場合、定期的に更新を確認する
        var timer = new global::Avalonia.Threading.DispatcherTimer
        {
            Interval = System.TimeSpan.FromSeconds(5)
        };
        timer.Tick += (s, e) => {
            if (!string.IsNullOrEmpty(fluid_general.Utils.AppEnv.ServerBaseUrl))
            {
                _ = LoadEventsAsync();
            }
        };
        timer.Start();
    }

    public async Task LoadEventsAsync()
    {
        try
        {
            var service = App.GetDataService();
            var events = await service.GetEventsAsync();
            EventGrid.ItemsSource = events;
        }
        catch (System.Exception ex)
        {
            fluid_general.Utils.AppEnv.LogError(ex);
        }
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await LoadEventsAsync();
    }

    private async void OnNewEventClick(object sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var dialog = new EventDialog();
        await dialog.ShowDialog(topLevel as Window ?? throw new System.InvalidOperationException());

        if (dialog.IsSaved)
        {
            var newEvent = new EventConfig
            {
                EventName = dialog.EventName,
                EventDate = dialog.EventDate,
                Participants = 0,
                Status = "予定",
                TouchSound = "JR",
                SameStudentSetting = true,
                RosterName = dialog.SelectedRoster
            };

            try
            {
                var service = App.GetDataService();
                await service.CreateEventAsync(newEvent);
                await LoadEventsAsync();
            }
            catch (System.Exception ex)
            {
                fluid_general.Utils.AppEnv.LogError(ex);
            }
        }
    }

    private void OnEventDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (EventGrid.SelectedItem is EventConfig selected)
        {
            var eventWindow = new EventWindow(selected);
            eventWindow.Show();
            
            // WPF 版と同様にメインウィンドウを閉じるか検討
            // ここではシンプルに新しいウィンドウを開くだけにします
        }
    }
}
