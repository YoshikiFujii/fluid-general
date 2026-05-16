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
    /// <summary>
    /// ExportListWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class ExportListWindow : Window
    {
        private Models.EventConfig _eventConfig;
        private string CurrentEvent;

        public ExportListWindow(Models.EventConfig eventConfig)
        {
            _eventConfig = eventConfig;
            CurrentEvent = eventConfig.EventName;

            InitializeComponent();
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
            int firstTotal = members.Count(m => m.CustomFields.GetValueOrDefault("Year") == "新");
            int secondTotal = totalCount - firstTotal;
            int maleTotal = members.Count(m => m.CustomFields.GetValueOrDefault("Gender") == "男");
            int femaleTotal = totalCount - maleTotal;

            int attendedCount = logs.Count(l => l.Status == "参加済み");
            int firstAttended = members.Count(m => m.CustomFields.GetValueOrDefault("Year") == "新" && logs.Any(l => l.Member.StudentNumber == m.StudentNumber && l.Status == "参加済み"));
            int secondAttended = attendedCount - firstAttended;
            int maleAttended = members.Count(m => m.CustomFields.GetValueOrDefault("Gender") == "男" && logs.Any(l => l.Member.StudentNumber == m.StudentNumber && l.Status == "参加済み"));
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

                    // ヘッダー
                    if (roomNumCheckBox.IsChecked == true) worksheet.Cell(1, col++).Value = "部屋番号";
                    if (genderCheckBox.IsChecked == true) worksheet.Cell(1, col++).Value = "性別";
                    if (nameCheckBox.IsChecked == true) worksheet.Cell(1, col++).Value = "名前";
                    if (kanaNameCheckBox.IsChecked == true) worksheet.Cell(1, col++).Value = "名前(カナ)";
                    if (studentNumCheckBox.IsChecked == true) worksheet.Cell(1, col++).Value = "学生番号";
                    if (departCheckBox.IsChecked == true) worksheet.Cell(1, col++).Value = "学科";
                    if (yearCheckBox.IsChecked == true) worksheet.Cell(1, col++).Value = "区分";
                    worksheet.Cell(1, col++).Value = "参加状況";

                    // データ
                    int row = 2;
                    foreach (var m in filteredMembers)
                    {
                        col = 1;
                        var log = logs.FirstOrDefault(l => l.Member.StudentNumber == m.StudentNumber);
                        string status = log?.Status ?? "未参加";

                        if (roomNumCheckBox.IsChecked == true) worksheet.Cell(row, col++).Value = m.CustomFields.GetValueOrDefault("RoomNumber");
                        if (genderCheckBox.IsChecked == true) worksheet.Cell(row, col++).Value = m.CustomFields.GetValueOrDefault("Gender");
                        if (nameCheckBox.IsChecked == true) worksheet.Cell(row, col++).Value = m.Name;
                        if (kanaNameCheckBox.IsChecked == true) worksheet.Cell(row, col++).Value = m.Kana;
                        if (studentNumCheckBox.IsChecked == true) worksheet.Cell(row, col++).Value = m.StudentNumber;
                        if (departCheckBox.IsChecked == true) worksheet.Cell(row, col++).Value = m.CustomFields.GetValueOrDefault("Department");
                        if (yearCheckBox.IsChecked == true) worksheet.Cell(row, col++).Value = m.CustomFields.GetValueOrDefault("Year");
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
