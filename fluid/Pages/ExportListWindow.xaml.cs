using ClosedXML.Excel;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml.Linq;

namespace fluid_general.Pages
{
    public class ColumnItem : System.ComponentModel.INotifyPropertyChanged
    {
        public string Label { get; set; }
        private bool isChecked;
        public bool IsChecked
        {
            get => isChecked;
            set { isChecked = value; OnPropertyChanged(nameof(IsChecked)); }
        }
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }

    public partial class ExportListWindow : Window
    {
        private Models.EventConfig _eventConfig;
        private string CurrentEvent;
        public System.Collections.ObjectModel.ObservableCollection<ColumnItem> ExportColumns { get; set; } = new System.Collections.ObjectModel.ObservableCollection<ColumnItem>();

        public ExportListWindow(Models.EventConfig eventConfig)
        {
            _eventConfig = eventConfig;
            CurrentEvent = eventConfig.EventName;

            InitializeComponent();
            DataContext = this;
            ColumnsItemsControl.ItemsSource = ExportColumns;
            _ = LoadColumnsAsync();
        }

        private async System.Threading.Tasks.Task LoadColumnsAsync()
        {
            var service = App.GetDataService();
            var config = await service.GetRosterConfigAsync(_eventConfig.RosterName);
            
            ExportColumns.Add(new ColumnItem { Label = "名前", IsChecked = true });
            ExportColumns.Add(new ColumnItem { Label = "名前（かな）", IsChecked = true });
            ExportColumns.Add(new ColumnItem { Label = "学籍番号", IsChecked = true });

            if (config?.Mappings != null)
            {
                foreach (var mapping in config.Mappings)
                {
                    if (mapping.Label == "ID" || mapping.Label == "名前" || mapping.Label == "名前（かな）" || mapping.Label == "学籍番号") continue;
                    ExportColumns.Add(new ColumnItem { Label = mapping.Label, IsChecked = true });
                }
            }
        }

        // ファイルが使用中かどうかを確認するヘルパーメソッド
        private bool IsFileInUse(string filePath)
        {
            try
            {
                using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (IOException)
            {
                return true;
            }
            return false;
        }

        private async System.Threading.Tasks.Task<List<Tuple<string, int, int>>> GetDetailAsync()
        {
            var service = App.GetDataService();
            var members = await service.GetMembersByRosterAsync(_eventConfig.RosterName);
            var logs = await service.GetCheckInLogsAsync(_eventConfig.Id);

            int totalCount = members.Count;
            // 互換性のため "Year" または "区分" を探す
            int firstTotal = members.Count(m => m.CustomFields.GetValueOrDefault("Year") == "新" || m.CustomFields.GetValueOrDefault("区分") == "新");
            int secondTotal = totalCount - firstTotal;
            int maleTotal = members.Count(m => m.CustomFields.GetValueOrDefault("Gender") == "男" || m.CustomFields.GetValueOrDefault("性別") == "男");
            int femaleTotal = totalCount - maleTotal;

            int attendedCount = logs.Count(l => l.Status == "参加済み");
            int firstAttended = members.Count(m => (m.CustomFields.GetValueOrDefault("Year") == "新" || m.CustomFields.GetValueOrDefault("区分") == "新") && logs.Any(l => l.ExcelId == m.ExcelId && l.Status == "参加済み"));
            int secondAttended = attendedCount - firstAttended;
            int maleAttended = members.Count(m => (m.CustomFields.GetValueOrDefault("Gender") == "男" || m.CustomFields.GetValueOrDefault("性別") == "男") && logs.Any(l => l.ExcelId == m.ExcelId && l.Status == "参加済み"));
            int femaleAttended = attendedCount - maleAttended;

            return new List<Tuple<string, int, int>>
            {
                Tuple.Create("合計人数", attendedCount, totalCount),
                Tuple.Create("１年出席人数", firstAttended, firstTotal),
                Tuple.Create("２年以上出席人数", secondAttended, secondTotal),
                Tuple.Create("男子出席人数", maleAttended, maleTotal),
                Tuple.Create("女子出席人数", femaleAttended, femaleTotal)
            };
        }

        private async void ExportExcelFile(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                Title = "エクスポート先を選択してください",
                FileName = $"{CurrentEvent}.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                string excelFilePath = saveFileDialog.FileName;
                var service = App.GetDataService();
                var members = await service.GetMembersByRosterAsync(_eventConfig.RosterName);
                var logs = await service.GetCheckInLogsAsync(_eventConfig.Id);

                // フィルタリング
                bool isChecked = CheckedCheckBox.IsChecked == true;
                bool isUnchecked = UncheckedCheckBox.IsChecked == true;
                bool isNotAttending = NotAttendingCheckBox.IsChecked == true;

                var filteredMembers = members.Where(m =>
                {
                    var log = logs.FirstOrDefault(l => l.Member.StudentNumber == m.StudentNumber);
                    string status = log?.Status ?? "未参加";
                    return (isChecked && status == "参加済み") ||
                           (isUnchecked && status == "未参加") ||
                           (isNotAttending && status == "不参加");
                }).ToList();

                if (!filteredMembers.Any())
                {
                    MessageBox.Show("選択した条件に一致するデータがありません。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add(CurrentEvent);
                    int col = 1;
                    var selectedColumns = ExportColumns.Where(c => c.IsChecked).ToList();

                    foreach (var column in selectedColumns)
                    {
                        worksheet.Cell(1, col++).Value = column.Label;
                    }
                    worksheet.Cell(1, col++).Value = "参加状況";

                    int row = 2;
                    foreach (var m in filteredMembers)
                    {
                        col = 1;
                        var log = logs.FirstOrDefault(l => l.ExcelId == m.ExcelId);
                        string status = log?.Status ?? "未参加";

                        foreach (var column in selectedColumns)
                        {
                            if (column.Label == "名前") worksheet.Cell(row, col++).Value = m.Name;
                            else if (column.Label == "名前（かな）") worksheet.Cell(row, col++).Value = m.Kana;
                            else if (column.Label == "学籍番号") worksheet.Cell(row, col++).Value = m.StudentNumber;
                            else worksheet.Cell(row, col++).Value = m.CustomFields.GetValueOrDefault(column.Label);
                        }
                        worksheet.Cell(row, col++).Value = status;
                        row++;
                    }

                    if (detailCheckBox.IsChecked == true)
                    {
                        var details = await GetDetailAsync();
                        var worksheet2 = workbook.Worksheets.Add("詳細情報");
                        worksheet2.Cell(1, 2).Value = "出席人数";
                        worksheet2.Cell(1, 3).Value = "総数";

                        int dRow = 2;
                        foreach (var d in details)
                        {
                            worksheet2.Cell(dRow, 1).Value = d.Item1;
                            worksheet2.Cell(dRow, 2).Value = d.Item2;
                            worksheet2.Cell(dRow, 3).Value = d.Item3;
                            dRow++;
                        }
                    }

                    workbook.SaveAs(excelFilePath);
                }

                MessageBox.Show("エクスポートが完了しました。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
