using fluid_general.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;

namespace fluid_general.Pages
{
    public partial class DataMigrationPage : Page
    {
        private string dataFolder = System.IO.Path.Combine(App.AppDataPath, "data");

        public DataMigrationPage()
        {
            InitializeComponent();
        }

        private async void StartMigrationButton_Click(object sender, RoutedEventArgs e)
        {
            StartMigrationButton.IsEnabled = false;
            MigrationProgressBar.Visibility = Visibility.Visible;
            LogTextBox.Text = "移行処理を開始します...\n";

            try
            {
                await RunMigrationAsync();
                LogTextBox.Text += "\n移行処理が正常に完了しました。";
                MessageBox.Show("データ移行が完了しました。\n古いデータファイルは data フォルダ内にそのまま残されています。", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogTextBox.Text += $"\nエラーが発生しました: {ex.Message}";
                MessageBox.Show($"データ移行中にエラーが発生しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                StartMigrationButton.IsEnabled = true;
                MigrationProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private async Task RunMigrationAsync()
        {
            if (!Directory.Exists(dataFolder))
            {
                LogTextBox.Text += "dataフォルダが見つかりません。移行するファイルがありません。\n";
                return;
            }

            var files = Directory.GetFiles(dataFolder, "*.xml");
            if (files.Length == 0)
            {
                LogTextBox.Text += "移行するXMLファイルがありません。\n";
                return;
            }

            var service = App.GetDataService();
            int fileCount = 0;

            foreach (var file in files)
            {
                fileCount++;
                string fileName = Path.GetFileName(file);
                string eventName = Path.GetFileNameWithoutExtension(file);
                
                LogTextBox.Text += $"処理中 ({fileCount}/{files.Length}): {fileName} ... ";
                MigrationProgressBar.Value = (double)fileCount / files.Length * 100;

                try
                {
                    XDocument eventDoc = XDocument.Load(file);

                    // 1. EventConfig の作成
                    var evConfig = new EventConfig
                    {
                        EventName = eventName,
                        EventDate = DateTime.Now, // XMLにはEventDateフィールドがない場合がある
                        TouchSound = eventDoc.Root?.Element("TouchSound")?.Value ?? "JR",
                        SameStudentSetting = (eventDoc.Root?.Element("SameStudentSetting")?.Value ?? "true") == "true",
                        Status = "完了", // 古いデータなので完了扱いとする
                        RosterName = "Migrated_" + eventName // 移行用名簿名を付与
                    };

                    // API経由またはローカルで作成
                    var createdEvent = await service.CreateEventAsync(evConfig);

                    // 2. 名簿データとチェックインログの復元
                    var entries = eventDoc.Descendants("Entry").Skip(1).ToList();
                    int entryCount = 0;
                    foreach (var entry in entries)
                    {
                        entryCount++;
                        string studentNumber = entry.Element("StudentNumber")?.Value ?? $"UNKNOWN_{Guid.NewGuid().ToString().Substring(0,8)}";
                        
                        // 既存Memberのチェック
                        var member = await service.GetMemberAsync(studentNumber);
                        if (member == null)
                        {
                            // 新規Member作成
                            member = new Member
                            {
                                StudentNumber = studentNumber,
                                Name = entry.Element("Name")?.Value ?? "名前なし",
                                Kana = entry.Element("Kana")?.Value ?? "",
                                RosterName = evConfig.RosterName,
                                ExcelId = entryCount
                            };
                            
                            // CustomFieldsに旧XMLの付加情報を詰める
                            if (entry.Element("RoomNumber") != null) member.CustomFields["RoomNumber"] = entry.Element("RoomNumber").Value;
                            if (entry.Element("Gender") != null) member.CustomFields["Gender"] = entry.Element("Gender").Value;
                            if (entry.Element("Department") != null) member.CustomFields["Department"] = entry.Element("Department").Value;
                            if (entry.Element("Year") != null) member.CustomFields["Year"] = entry.Element("Year").Value;

                            await service.CreateMemberAsync(member);
                        }

                        // 全員分のチェックインログ（ステータス記録）を作成し、イベントに紐付ける
                        string status = entry.Element("Status")?.Value ?? "未参加";
                        await service.UpdateCheckInStatusAsync(member.RosterName, member.ExcelId, createdEvent.Id, status);
                    }

                    LogTextBox.Text += $"完了 (イベントID: {createdEvent.Id}, 追加された名簿: {entryCount}名)\n";
                    LogTextBox.ScrollToEnd();
                }
                catch (Exception ex)
                {
                    LogTextBox.Text += $"失敗 ({ex.Message})\n";
                    LogTextBox.ScrollToEnd();
                }
            }
            
            var allMembers = await service.GetMembersAsync();
            LogTextBox.Text += $"\nデータベース全体の登録メンバー数: {allMembers.Count}名\n";
            LogTextBox.ScrollToEnd();
        }
    }
}
