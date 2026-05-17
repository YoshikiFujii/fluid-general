using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace fluid_general.Pages
{
    public partial class EventDialog : ModernWpf.Controls.ContentDialog
    {
        public string EventName { get; private set; }
        public DateTime EventDate { get; private set; }
        public string Roster { get; private set; }

        private fluid_general.Models.EventConfig _editingEvent;

        public EventDialog()
        {
            InitializeComponent();
            _ = LoadRostersAsync();
        }

        public EventDialog(fluid_general.Models.EventConfig editingEvent) : this()
        {
            _editingEvent = editingEvent;
            Title = "イベントの編集";
            EventNameTextBox.Text = editingEvent.EventName;
            EventDatePicker.SelectedDate = editingEvent.EventDate;
        }

        private async System.Threading.Tasks.Task LoadRostersAsync()
        {
            try
            {
                var service = App.GetDataService();
                var members = await service.GetMembersAsync();
                var rosters = members
                    .Where(m => !string.IsNullOrEmpty(m.RosterName))
                    .Select(m => m.RosterName)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();

                foreach (var roster in rosters)
                {
                    RosterComboBox.Items.Add(roster);
                }

                if (_editingEvent != null && !string.IsNullOrEmpty(_editingEvent.RosterName))
                {
                    RosterComboBox.SelectedItem = _editingEvent.RosterName;
                }
                
                if (rosters.Count == 0)
                {
                    MessageBox.Show("利用可能な名簿がありません。先に名簿をインポートしてください。");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("名簿の読み込みに失敗しました: " + ex.Message);
            }
        }

        private async void ContentDialog_PrimaryButtonClick(ModernWpf.Controls.ContentDialog sender, ModernWpf.Controls.ContentDialogButtonClickEventArgs args)
        {
            // バリデーション: イベント名と開催日が入力されているか確認
            if (string.IsNullOrWhiteSpace(EventNameTextBox.Text) || !EventDatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("すべてのフィールドに記入してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Cancel = true;
                return;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            if (EventNameTextBox.Text.Any(c => invalidChars.Contains(c)))
            {
                MessageBox.Show($"イベント名に使用できない文字が含まれています。\n禁止文字: {string.Join(" ", invalidChars)}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Cancel = true;
                return;
            }

            // 既存イベント名のチェック
            try
            {
                var service = App.GetDataService();
                var events = await service.GetEventsAsync();
                if (events.Any(ev => ev.EventName == EventNameTextBox.Text && (_editingEvent == null || ev.Id != _editingEvent.Id)))
                {
                    MessageBox.Show("同じ名前のイベントが既に存在します。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    args.Cancel = true;
                    return;
                }
            }
            catch (Exception ex)
            {
                // エラー時は警告を出して続行を止めるか、ログに吐く
                MessageBox.Show($"イベント名の確認中にエラーが発生しました: {ex.Message}");
                args.Cancel = true;
                return;
            }

            // 入力されたイベント名と開催日を取得
            EventName = EventNameTextBox.Text;
            EventDate = EventDatePicker.SelectedDate.Value.Date;
            Roster = RosterComboBox.Text;
        }
    }
}
