using ClosedXML.Excel;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using fluid_general.Models;

namespace fluid_general.Pages
{
    /// <summary>
    /// ImportDialog.xaml の相互作用ロジック
    /// </summary>
    public partial class ImportDialog : ModernWpf.Controls.ContentDialog
    {
        private string CurrentEvent;

        public bool IsImportSuccessful { get; private set; }

        public ImportDialog(string currentEvent)
        {
            CurrentEvent = currentEvent;
            InitializeComponent();
        }

        private async void ImportDialog_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var service = App.GetDataService();
                var eventConfig = (await service.GetEventsAsync()).FirstOrDefault(ev => ev.EventName == CurrentEvent);
                if (eventConfig == null) return;

                var members = await service.GetMembersByRosterAsync(eventConfig.RosterName);
                
                var allKeys = new List<string> { "名前", "かな", "学籍番号" };
                allKeys.AddRange(members.SelectMany(m => m.CustomFields.Keys).Distinct());
                allKeys = allKeys.Distinct().ToList();

                MatchColumnComboBox.ItemsSource = allKeys;
                if (allKeys.Contains("学籍番号")) MatchColumnComboBox.SelectedItem = "学籍番号";
                else if (allKeys.Count > 0) MatchColumnComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                App.LogError(ex);
            }
        }

        private void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "Excel Files|*.xlsx", Title = "エクセルファイルを選択" };
            if (openFileDialog.ShowDialog() == true)
            {
                FilePathTextBox.Text = openFileDialog.FileName;
            }
        }

        private async void ExecuteBatchImport_Click(ModernWpf.Controls.ContentDialog sender, ModernWpf.Controls.ContentDialogButtonClickEventArgs args)
        {
            // ダイアログが自動的に閉じるのを防ぐ（非同期処理を行うため）
            args.Cancel = true;
            string filePath = FilePathTextBox.Text;
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                MessageBox.Show("エクセルファイルを選択してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string targetStatus = (TargetStatusComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString();
            if (string.IsNullOrEmpty(targetStatus)) return;

            string searchKey = MatchColumnComboBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(searchKey))
            {
                MessageBox.Show("登録に使うカラムを選択してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string currentTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            string logFolderPath = Path.Combine(App.AppDataPath, "log");
            string logFilePath = Path.Combine(logFolderPath, $"{CurrentEvent}.txt");
            if (!Directory.Exists(logFolderPath)) Directory.CreateDirectory(logFolderPath);

            try
            {
                var dataList = new HashSet<string>();
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var workbook = new XLWorkbook(fs))
                {
                    var worksheet = workbook.Worksheets.First();
                    foreach (var cell in worksheet.Column(1).CellsUsed())
                    {
                        string val = cell.GetValue<string>()?.Trim();
                        if (!string.IsNullOrEmpty(val)) dataList.Add(val);
                    }
                }

                var service = App.GetDataService();
                var eventConfig = (await service.GetEventsAsync()).FirstOrDefault(ev => ev.EventName == CurrentEvent);
                if (eventConfig == null) { MessageBox.Show("イベントが見つかりません。"); return; }

                var members = await service.GetMembersByRosterAsync(eventConfig.RosterName);
                var currentLogs = await service.GetCheckInLogsAsync(eventConfig.Id);
                var registeredList = new List<string>();
                var missingStudents = new List<string>();
                var alreadyInStateList = new List<string>();
                var skippedMemberObjects = new List<Member>();

                progressBar.Visibility = Visibility.Visible;
                progressBar.IsIndeterminate = false;
                progressBar.Maximum = dataList.Count;
                progressBar.Value = 0;
                int processed = 0;

                foreach (var val in dataList)
                {
                    var member = members.FirstOrDefault(m => 
                    {
                        if (searchKey == "名前") return m.Name == val;
                        if (searchKey == "かな") return m.Kana == val;
                        if (searchKey == "学籍番号") return m.StudentNumber == val;
                        return m.CustomFields.GetValueOrDefault(searchKey) == val;
                    });

                    if (member != null)
                    {
                        var log = currentLogs.FirstOrDefault(l => l.ExcelId == member.ExcelId);
                        string currentStatus = log?.Status ?? "未参加";

                        if (currentStatus == targetStatus)
                        {
                            alreadyInStateList.Add($"{member.Name} ({member.StudentNumber})");
                            skippedMemberObjects.Add(member);
                        }
                        else
                        {
                            await service.UpdateCheckInStatusAsync(member.RosterName, member.ExcelId, eventConfig.Id, targetStatus);
                            registeredList.Add($"{member.Name} ({member.StudentNumber})");
                        }
                    }
                    else
                    {
                        missingStudents.Add(val);
                    }
                    progressBar.Value = ++processed;
                }

                using (StreamWriter logWriter = new StreamWriter(logFilePath, true, Encoding.UTF8))
                {
                    logWriter.WriteLine($"[{currentTime}] {targetStatus}一括変更のインポート開始");
                    if (registeredList.Any())
                    {
                        logWriter.WriteLine($"{targetStatus}に変更された生徒:");
                        foreach (var s in registeredList) logWriter.WriteLine($"  - {s}");
                    }
                    if (alreadyInStateList.Any())
                    {
                        logWriter.WriteLine($"すでに{targetStatus}だった生徒（変更なし）:");
                        foreach (var s in alreadyInStateList) logWriter.WriteLine($"  - {s}");
                    }
                    if (missingStudents.Any())
                    {
                        logWriter.WriteLine("名簿に存在しなかった生徒（または一致しなかったデータ）:");
                        foreach (var s in missingStudents) logWriter.WriteLine($"  - {s}");
                    }
                    logWriter.WriteLine($"[{currentTime}] {targetStatus}一括変更のインポート終了");
                }

                IsImportSuccessful = true;
                string message = $"一括状態変更が完了しました。\n\n正常更新: {registeredList.Count}名";
                if (alreadyInStateList.Any()) message += $"\nすでに{targetStatus}: {alreadyInStateList.Count}名 (変更なし)";
                if (missingStudents.Any()) message += $"\n名簿不一致: {missingStudents.Count}名";

                if (alreadyInStateList.Any())
                {
                    MessageBox.Show(message, "完了（一部スキップあり）", MessageBoxButton.OK, MessageBoxImage.Warning);

                    // スキップされた生徒をエクセルで出力
                    var saveDialog = new SaveFileDialog
                    {
                        Filter = "Excel Files|*.xlsx",
                        FileName = $"SkippedMembers_{DateTime.Now:yyyyMMddHHmmss}.xlsx",
                        Title = "スキップされた生徒リスト（変更なし）を保存"
                    };

                    if (saveDialog.ShowDialog() == true)
                    {
                        try
                        {
                            using (var workbook = new XLWorkbook())
                            {
                                var ws = workbook.Worksheets.Add("Skipped Members");
                                ws.Cell(1, 1).Value = "名前";
                                ws.Cell(1, 2).Value = "学籍番号";
                                ws.Cell(1, 3).Value = "理由";

                                int row = 2;
                                foreach (var m in skippedMemberObjects)
                                {
                                    ws.Cell(row, 1).Value = m.Name;
                                    ws.Cell(row, 2).Value = m.StudentNumber;
                                    ws.Cell(row, 3).Value = $"すでに{targetStatus}でした";
                                    row++;
                                }
                                ws.Columns().AdjustToContents();
                                workbook.SaveAs(saveDialog.FileName);
                            }
                            MessageBox.Show($"スキップされた生徒リストを保存しました:\n{saveDialog.FileName}", "保存完了", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"エクセルファイルの保存中にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                else
                {
                    MessageBox.Show(message, "完了", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                progressBar.Visibility = Visibility.Collapsed;
                this.Hide();
            }
            catch (Exception ex)
            {
                App.LogError(ex);
                MessageBox.Show($"エラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                progressBar.Visibility = Visibility.Collapsed;
            }
        }

        private async void ImportEventClick(object sender, RoutedEventArgs e)
        {
            string currentTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            string logFolderPath = Path.Combine(App.AppDataPath, "log");
            string logFilePath = Path.Combine(logFolderPath, $"{CurrentEvent}.txt");
            if (!Directory.Exists(logFolderPath)) Directory.CreateDirectory(logFolderPath);

            string searchKey = MatchColumnComboBox.SelectedItem?.ToString();

            if (string.IsNullOrEmpty(searchKey))
            {
                MessageBox.Show("検索条件(登録に使うカラム)を選択してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "XML Files (*.xml)|*.xml", Title = "旧形式イベントファイルをインポート" };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var sourceDoc = XDocument.Load(openFileDialog.FileName);
                    var sourceEntries = sourceDoc.Descendants("Entry").ToList();

                    var service = App.GetDataService();
                    var eventConfig = (await service.GetEventsAsync()).FirstOrDefault(ev => ev.EventName == CurrentEvent);
                    if (eventConfig == null) { MessageBox.Show("イベント情報が見つかりません。"); return; }

                    var members = await service.GetMembersByRosterAsync(eventConfig.RosterName);
                    var importedCount = 0;
                    var missingCount = 0;

                    progressBar.Visibility = Visibility.Visible;
                    progressBar.IsIndeterminate = false;
                    progressBar.Maximum = sourceEntries.Count;
                    progressBar.Value = 0;

                    foreach (var entry in sourceEntries)
                    {
                        string sNum = entry.Element("StudentNumber")?.Value;
                        string rNum = entry.Element("RoomNumber")?.Value;
                        string status = entry.Element("Status")?.Value;

                        if (status != "参加済み") { progressBar.Value++; continue; }

                        var member = members.FirstOrDefault(m => 
                        {
                            if (searchKey == "名前") return m.Name == sNum || m.Name == rNum;
                            if (searchKey == "かな") return m.Kana == sNum || m.Kana == rNum;
                            if (searchKey == "学籍番号") return m.StudentNumber == sNum || m.StudentNumber == rNum;
                            return m.CustomFields.GetValueOrDefault(searchKey) == sNum || m.CustomFields.GetValueOrDefault(searchKey) == rNum;
                        });

                        if (member != null)
                        {
                            await service.UpdateCheckInStatusAsync(member.RosterName, member.ExcelId, eventConfig.Id, "参加済み");
                            importedCount++;
                        }
                        else
                        {
                            missingCount++;
                        }
                        progressBar.Value++;
                    }

                    using (StreamWriter logWriter = new StreamWriter(logFilePath, true, Encoding.UTF8))
                    {
                        logWriter.WriteLine($"[{currentTime}] 旧XMLイベントインポート: 正常={importedCount}, 不明={missingCount}");
                    }

                    IsImportSuccessful = true;
                    progressBar.Visibility = Visibility.Collapsed;
                    MessageBox.Show($"インポートが完了しました。\n(正常: {importedCount}, 名簿不一致: {missingCount})", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.Hide();
                }
                catch (Exception ex)
                {
                    App.LogError(ex);
                    MessageBox.Show($"エラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    progressBar.Visibility = Visibility.Collapsed;
                }
            }
        }
    }
}
