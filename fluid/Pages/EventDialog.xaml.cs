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

        private string dataFolder = System.IO.Path.Combine(fluid_general.App.AppDataPath, "data");
        public string rosterFolderPath = System.IO.Path.Combine(fluid_general.App.AppDataPath, "roster");
        private ObservableCollection<Event> events;

        public EventDialog()
        {
            InitializeComponent();

            try
            {

                if (Directory.Exists(rosterFolderPath))
                {
                    List<string> rosterFiles = Directory.GetFiles(rosterFolderPath, "*.xml")
                                   .Select(System.IO.Path.GetFileNameWithoutExtension)
                                   .ToList();

                    foreach (var roster in rosterFiles)
                    {
                        RosterComboBox.Items.Add(roster);
                    }
                }
                else
                {
                    MessageBox.Show("Debug:rosterフォルダ内に名簿が存在しません。");
                }
            }
            catch (Exception ex)
            {

                MessageBox.Show("Error loading roster files: " + ex.Message);
            }

        }

        private void ContentDialog_PrimaryButtonClick(ModernWpf.Controls.ContentDialog sender, ModernWpf.Controls.ContentDialogButtonClickEventArgs args)
        {

            // バリデーション: イベント名と開催日が入力されているか確認
            if (string.IsNullOrWhiteSpace(EventNameTextBox.Text) || !EventDatePicker.SelectedDate.HasValue)
            {
                // 入力が不完全な場合はアラートを表示
                MessageBox.Show("すべてのフィールドに記入してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);

                // イベントをキャンセルしてダイアログを閉じないようにする
                args.Cancel = true;
                return;
            }
            char[]invalidChars = Path.GetInvalidFileNameChars();
            if (EventNameTextBox.Text.Any(c => invalidChars.Contains(c)))
            {
                MessageBox.Show($"イベント名に使用できない文字が含まれています。\n禁止文字: {string.Join(" ", invalidChars)}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Cancel = true;
                return;
            }

            if (Directory.GetFiles(dataFolder, "*.xml").Any(file => System.IO.Path.GetFileNameWithoutExtension(file) == EventNameTextBox.Text))
            {
                MessageBox.Show("同じ名前のファイルが存在します。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
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
