using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Threading.Tasks;

namespace fluid_general.Pages
{
    public partial class EventPage : System.Windows.Controls.Page
    {
        private ObservableCollection<fluid_general.Models.EventConfig> Events = new ObservableCollection<fluid_general.Models.EventConfig>();

        public EventPage()
        {
            InitializeComponent();
            _ = LoadEventsAsync();
            
            EventList.ItemsSource = Events;
            Events.CollectionChanged += Events_CollectionChanged;
            UpdateEventListVisibility();
        }

        private void TextBlock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (EventList.SelectedItem != null)
            {
                var selectedEvent = (fluid_general.Models.EventConfig)EventList.SelectedItem;
                OpenEventWindow(selectedEvent);
            }
        }

        private void Events_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateEventListVisibility();
        }

        private void Event_Open(object sender, RoutedEventArgs e)
        {
            if (EventList.SelectedItem != null)
            {
                var selectedEvent = (fluid_general.Models.EventConfig)EventList.SelectedItem;
                OpenEventWindow(selectedEvent);
            }
        }

        private void OpenEventWindow(fluid_general.Models.EventConfig selectedEvent)
        {
            EventWindow eventWindow = new EventWindow(selectedEvent);
            eventWindow.Show();
            var mainWindow = Application.Current.MainWindow;
            mainWindow?.Close();
        }

        private void UpdateEventListVisibility()
        {
            if (Events.Count == 0)
            {
                // エラー表示が出ていない場合のみ「イベントがありません」を出す
                if (ErrorMessageTextBlock.Visibility != Visibility.Visible)
                {
                    EmptyMessageTextBlock.Visibility = Visibility.Visible;
                }
                EventList.Visibility = Visibility.Collapsed;
            }
            else
            {
                EmptyMessageTextBlock.Visibility = Visibility.Collapsed;
                ErrorMessageTextBlock.Visibility = Visibility.Collapsed;
                EventList.Visibility = Visibility.Visible;
            }
        }

        private async Task LoadEventsAsync()
        {
            try
            {
                ErrorMessageTextBlock.Visibility = Visibility.Collapsed;
                var service = fluid_general.App.GetDataService();
                var dbEvents = await service.GetEventsAsync();
                
                Events.Clear();
                foreach (var ev in dbEvents)
                {
                    Events.Add(ev);
                }
            }
            catch (Exception ex)
            {
                App.LogError(ex);
                // 通信エラーの場合はインラインで表示
                Events.Clear();
                ErrorMessageTextBlock.Text = $"イベントの読み込みに失敗しました: {ex.Message}";
                ErrorMessageTextBlock.Visibility = Visibility.Visible;
                EmptyMessageTextBlock.Visibility = Visibility.Collapsed;
                EventList.Visibility = Visibility.Collapsed;
            }
        }

        private async Task SaveEventToDbAsync(fluid_general.Models.EventConfig ev)
        {
            try
            {
                var service = fluid_general.App.GetDataService();
                await service.CreateEventAsync(ev);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"イベントの保存に失敗しました {ev.EventName}: {ex.Message}");
            }
        }

        private async void NewEvent_Click(object sender, RoutedEventArgs e)
        {
            await LoadEventsAsync();
            var dialog = new EventDialog();
            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                string eventName = dialog.EventName;
                DateTime eventDate = dialog.EventDate;
                
                var newEvent = new fluid_general.Models.EventConfig
                {
                    EventName = eventName,
                    EventDate = eventDate,
                    Participants = 0,
                    Status = "予定",
                    TouchSound = "JR",
                    SameStudentSetting = true,
                    RosterName = dialog.Roster,
                };
                
                await SaveEventToDbAsync(newEvent);
                Events.Add(newEvent);
            }
        }

        private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                var queryText = sender.Text;
                var filteredEvents = Events
                    .Where(ev => ev.EventName.IndexOf(queryText, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
                EventList.ItemsSource = filteredEvents;
            }
        }

        private async void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (EventList.SelectedItem is fluid_general.Models.EventConfig selectedEvent)
            {
                ContentDialog deleteDialog = new ContentDialog
                {
                    Title = "削除",
                    Content = $"本当にイベント '{selectedEvent.EventName}' を削除しますか？",
                    PrimaryButtonText = "削除",
                    CloseButtonText = "キャンセル"
                };

                if (await deleteDialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    try
                    {
                        var service = fluid_general.App.GetDataService();
                        await service.DeleteEventAsync(selectedEvent.Id);
                        await LoadEventsAsync();
                        MessageBox.Show("イベントの削除に成功しました。");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"イベントの削除に失敗しました: {ex.Message}");
                    }
                }
            }
        }
    }
}
