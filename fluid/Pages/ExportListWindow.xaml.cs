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
        private string CurrentEvent;
        private string eventFilePath;
        private string excelFilePath;
        private RosterInfo rosterInfo;
        public ExportListWindow(string EventFilePath, string currentEvent)
        {
            eventFilePath = EventFilePath;
            CurrentEvent = currentEvent;

            InitializeComponent();
            LoadRosterInfo();
        }
        private void LoadRosterInfo()
        {
            try
            {
                // XMLファイルを読み込む
                XDocument doc = XDocument.Load(eventFilePath);

                // RosterInfo要素を取得
                var rosterInfoElement = doc.Descendants("RosterInfo").FirstOrDefault();

                if (rosterInfoElement != null)
                {
                    // FromXmlメソッドを使用してRosterInfoを作成
                    rosterInfo = RosterInfo.FromXml(rosterInfoElement);
                }
                else
                {
                    MessageBox.Show("イベントファイルからRosterInfoを読み込めませんでした。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"RosterInfoの読み込み中にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
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
        private List<Tuple<string, int, int>> GetDetail()
        {
            XDocument eventDoc = XDocument.Load(eventFilePath);
            int totalParticipants = eventDoc.Descendants("Entry")
                                     .Count(e => (string)e.Element("Status") == "参加済み" ||
                                                 (string)e.Element("Status") == "未参加");
            int firstTotalParticipants = eventDoc.Descendants("Entry").Count(e => (string)e.Element("Year") == "新");
            int secondTotalParticipants = totalParticipants - firstTotalParticipants;
            int maleTotalParticipants = eventDoc.Descendants("Entry").Count(e => (string)e.Element("Gender") == "男");
            int femaleTotalParticipants = totalParticipants - maleTotalParticipants;

            int doneParticipants = eventDoc.Descendants("Entry").Count(e => (string)e.Element("Status") == "参加済み");
            int firstParticipants = eventDoc.Descendants("Entry")
                                     .Count(e => (string)e.Element("Status") == "参加済み" &&
                                                 (string)e.Element("Year") == "新");
            int secondParticipants = doneParticipants - firstParticipants;
            int maleParticipants = eventDoc.Descendants("Entry").Count(e => (string)e.Element("Status") == "参加済み" &&
                                                                            (string)e.Element("Gender") == "男");
            int femaleParticipants = doneParticipants - maleParticipants;

            // 結果をリストに格納して返す
            return new List<Tuple<string, int, int>>
            {
                Tuple.Create("合計人数", doneParticipants, totalParticipants),
                Tuple.Create("１年出席人数", firstParticipants, firstTotalParticipants),
                Tuple.Create("２年以上出席人数", secondParticipants, secondTotalParticipants),
                Tuple.Create("男子出席人数", maleParticipants, maleTotalParticipants),
                Tuple.Create("女子出席人数", femaleParticipants, femaleTotalParticipants)
            };
        }

        private void ExportExcelFile(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                Title = "エクスポート先を選択してください",
                FileName = $"{CurrentEvent}.xlsx" // デフォルトのファイル名
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                string excelFilePath = saveFileDialog.FileName;

                // XMLファイルを読み込む
                DataSet dataSet = new DataSet();
                dataSet.ReadXml(eventFilePath);

                // チェックボックスの状態を取得
                bool isChecked = CheckedCheckBox.IsChecked == true;
                bool isUnchecked = UncheckedCheckBox.IsChecked == true;
                bool isNotAttending = NotAttendingCheckBox.IsChecked == true;

                // 列のチェックボックスの状態を取得
                bool includeRoomNumber = roomNumCheckBox?.IsChecked == true;
                bool includeGender = genderCheckBox?.IsChecked == true;
                bool includeName = nameCheckBox?.IsChecked == true;
                bool includeKanaName = kanaNameCheckBox?.IsChecked == true;
                bool includeStudentNumber = studentNumCheckBox?.IsChecked == true;
                bool includeDepartment = departCheckBox?.IsChecked == true;
                bool includeCategory = yearCheckBox?.IsChecked == true;

                //詳細情報チェックボックスの状態を取得
                bool includeDetail = detailCheckBox?.IsChecked == true;

                // 少なくとも1つの列が選択されていることを確認
                if (rosterInfo == null)
                {
                    MessageBox.Show("RosterInfo が正しく読み込まれていません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (!includeRoomNumber && !includeGender && !includeName && !includeKanaName &&
                    !includeStudentNumber && !includeDepartment && !includeCategory)
                {
                    MessageBox.Show("出力する生徒情報を少なくとも1つ選択してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Excelファイルを作成する
                using (var workbook = new XLWorkbook())
                {

                    // Roster.Entryのデータを取得
                    DataTable rosterTable = dataSet.Tables["Entry"];

                    if (rosterTable != null && rosterTable.Rows.Count > 0)
                    {

                        // 1行目をタイトル行として設定
                        DataRow titleRow = rosterTable.Rows[0];
                        for (int col = 0; col < rosterTable.Columns.Count; col++)
                        {
                            rosterTable.Columns[col].ColumnName = titleRow[col]?.ToString();
                        }


                        // チェックボックスの状態に基づいてデータをフィルタリング
                        var filteredRows = rosterTable.AsEnumerable().Where(row =>
                        {
                            string status = row["参加状況"]?.ToString();
                            return (isChecked && status == "参加済み") ||
                                    (isUnchecked && status == "未参加") ||
                                    (isNotAttending && status == "不参加");
                        });

                        if (!filteredRows.Any())
                        {
                            MessageBox.Show("選択した条件に一致するデータがありません。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }

                        // フィルタリングされたデータを新しい DataTable にコピー
                        DataTable filteredTable = filteredRows.CopyToDataTable();

                        // 選択する列のインデックスを格納するリスト
                        var selectedColumnIndices = new List<int>();


                        // 列インデックスをリストに追加する関数
                        void AddColumnIndex(int? columnIndex, bool include)
                        {
                            if (include && columnIndex.HasValue && columnIndex.Value > 0 && columnIndex.Value <= rosterTable.Columns.Count)
                            {
                                selectedColumnIndices.Add(columnIndex.Value - 1); // 0ベースのインデックスに変換
                            }
                        }

                        // チェックボックスの状態に応じて列インデックスを追加
                        AddColumnIndex(rosterInfo.RoomNumberCol, includeRoomNumber);
                        AddColumnIndex(rosterInfo.GenderCol, includeGender);
                        AddColumnIndex(rosterInfo.NameCol, includeName);
                        AddColumnIndex(rosterInfo.KanaCol, includeKanaName);
                        AddColumnIndex(rosterInfo.StudentNumberCol, includeStudentNumber);
                        AddColumnIndex(rosterInfo.DepartCol, includeDepartment);
                        AddColumnIndex(rosterInfo.YearCol, includeCategory);

                        // Status列のインデックスを見つける（"参加状況"列）
                        int statusColumnIndex = -1;
                        for (int i = 0; i < rosterTable.Columns.Count; i++)
                        {
                            if (rosterTable.Columns[i].ColumnName == "参加状況")
                            {
                                statusColumnIndex = i;
                                break;
                            }
                        }

                        if (statusColumnIndex >= 0)
                        {
                            selectedColumnIndices.Add(statusColumnIndex);
                        }

                        // 選択されたインデックスを昇順にソート
                        selectedColumnIndices.Sort();

                        // 選択された列名のリストを作成
                        var selectedColumns = new List<string>();

                        foreach (int index in selectedColumnIndices)
                        {
                            if (index < rosterTable.Columns.Count)
                            {
                                // 列名をリストに追加
                                selectedColumns.Add(rosterTable.Columns[index].ColumnName);

                                // フィルタリングされたデータを確認
                                if (!filteredRows.Any())
                                {
                                    MessageBox.Show("選択した条件に一致するデータがありません。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                                    return;
                                }

                                // 選択された列名を確認
                                if (selectedColumns.Count == 0)
                                {
                                    MessageBox.Show("エクスポートする列が選択されていません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                                    return;
                                }
                            }
                        }

                        // selectedColumnsが空でないことを確認
                        if (selectedColumns.Count == 0)
                        {
                            MessageBox.Show("エクスポートする列が選択されていません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);

                            return;
                        }


                        // 選択された列のみを含むテーブルを作成
                        DataTable finalTable = filteredTable.DefaultView.ToTable(false, selectedColumns.ToArray());

                        // Rosterシートを作成
                        var worksheet = workbook.Worksheets.Add(CurrentEvent);

                        // DataTableをExcelのテーブル形式で挿入
                        worksheet.Cell(1, 1).InsertTable(finalTable);

                        if(includeDetail)
                        {
                            var details = GetDetail();

                            var worksheet2 = workbook.Worksheets.Add("詳細情報");
                            worksheet2.Cell(1, 2).Value = "出席人数";
                            worksheet2.Cell(1, 3).Value = "総数";

                            int row = 2;
                            foreach(var detail in details)
                            {
                                worksheet2.Cell(row, 1).Value = detail.Item1; // タイトル
                                worksheet2.Cell(row, 2).Value = detail.Item2; // 出席人数
                                worksheet2.Cell(row, 3).Value = detail.Item3; // 総人数
                                row++;
                            }
                        }
                    }

                    // Excelファイルを保存する
                    workbook.SaveAs(excelFilePath);
                }

                MessageBox.Show("エクスポートが完了しました。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);

            }
        }


    }
}
