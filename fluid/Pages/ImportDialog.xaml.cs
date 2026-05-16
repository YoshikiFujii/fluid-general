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

        private async void ImportAbsentClick(object sender, RoutedEventArgs e)
        {
            await ProcessListImportAsync("不参加", "欠席リストファイルをインポート");
        }

        private async void ImportAttendClick(object sender, RoutedEventArgs e)
        {
            await ProcessListImportAsync("参加済み", "出席リストファイルをインポート");
        }

        private async Task ProcessListImportAsync(string status, string dialogTitle)
        {
            string currentTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            string logFolderPath = Path.Combine(App.AppDataPath, "log");
            string logFilePath = Path.Combine(logFolderPath, $"{CurrentEvent}.txt");

            if (!Directory.Exists(logFolderPath)) Directory.CreateDirectory(logFolderPath);

            string searchCondition = SearchCondition.Text;
            string searchKey = searchCondition == "学籍番号" ? "StudentNumber" : (searchCondition == "部屋番号" ? "RoomNumber" : "");

            if (string.IsNullOrEmpty(searchKey))
            {
                MessageBox.Show("検索条件を選択してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "Excel Files|*.xlsx", Title = dialogTitle };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var dataList = new HashSet<string>();
                    using (var fs = new FileStream(openFileDialog.FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var workbook = new XLWorkbook(fs))
                    {
                        var worksheet = workbook.Worksheets.First();
                        foreach (var cell in worksheet.Column(1).CellsUsed())
                        {
                            dataList.Add(cell.GetValue<string>().Trim());
                        }
                    }

                    var service = App.GetDataService();
                    var eventConfig = (await service.GetEventsAsync()).FirstOrDefault(ev => ev.EventName == CurrentEvent);
                    if (eventConfig == null) { MessageBox.Show("イベントが見つかりません。"); return; }

                    var members = await service.GetMembersByRosterAsync(eventConfig.RosterName);
                    var registeredList = new List<string>();
                    var missingStudents = new List<string>();

                    progressBar.Visibility = Visibility.Visible;
                    progressBar.Maximum = dataList.Count;
                    int processed = 0;

                    foreach (var val in dataList)
                    {
                        var member = members.FirstOrDefault(m => 
                            (searchKey == "StudentNumber" && m.StudentNumber == val) ||
                            (searchKey == "RoomNumber" && m.CustomFields.GetValueOrDefault("RoomNumber") == val));

                        if (member != null)
                        {
                            await service.UpdateCheckInStatusAsync(member.RosterName, member.ExcelId, eventConfig.Id, status);
                            registeredList.Add($"{member.Name} ({member.StudentNumber})");
                        }
                        else
                        {
                            missingStudents.Add(val);
                        }
                        progressBar.Value = ++processed;
                    }

                    using (StreamWriter logWriter = new StreamWriter(logFilePath, true, Encoding.UTF8))
                    {
                        logWriter.WriteLine($"[{currentTime}] {status}リストのインポート開始");
                        if (registeredList.Any())
                        {
                            logWriter.WriteLine($"{status}登録された生徒:");
                            foreach (var s in registeredList) logWriter.WriteLine($"  - {s}");
                        }
                        if (missingStudents.Any())
                        {
                            logWriter.WriteLine("名簿に存在しなかった生徒:");
                            foreach (var s in missingStudents) logWriter.WriteLine($"  - {s}");
                        }
                        logWriter.WriteLine($"[{currentTime}] {status}リストのインポート終了");
                    }

                    MessageBox.Show("インポートが完了しました。", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
                    progressBar.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    App.LogError(ex);
                    MessageBox.Show($"エラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    progressBar.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async void ImportEventClick(object sender, RoutedEventArgs e)
        {
            string currentTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            string logFolderPath = Path.Combine(App.AppDataPath, "log");
            string logFilePath = Path.Combine(logFolderPath, $"{CurrentEvent}.txt");
            if (!Directory.Exists(logFolderPath)) Directory.CreateDirectory(logFolderPath);

            string searchCondition = SearchCondition.Text;
            string searchKey = searchCondition == "学籍番号" ? "StudentNumber" : (searchCondition == "部屋番号" ? "RoomNumber" : "");

            if (string.IsNullOrEmpty(searchKey))
            {
                MessageBox.Show("検索条件を選択してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    progressBar.Maximum = sourceEntries.Count;
                    progressBar.Value = 0;

                    foreach (var entry in sourceEntries)
                    {
                        string sNum = entry.Element("StudentNumber")?.Value;
                        string rNum = entry.Element("RoomNumber")?.Value;
                        string status = entry.Element("Status")?.Value;

                        if (status != "参加済み") { progressBar.Value++; continue; }

                        var member = members.FirstOrDefault(m => 
                            (searchKey == "StudentNumber" && m.StudentNumber == sNum) ||
                            (searchKey == "RoomNumber" && m.CustomFields.GetValueOrDefault("RoomNumber") == rNum));

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
