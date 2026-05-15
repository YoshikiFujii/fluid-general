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

        public bool IsImportSuccessful { get; private set; } // インポート結果を保持するプロパティ

        public ImportDialog(string currentEvent)
        {
            CurrentEvent = currentEvent;
            InitializeComponent();
        }
        private async void ImportAbsentClick(object sender, RoutedEventArgs e)
        {
            string currentTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"); // 現在の時間
            string logFolderPath = System.IO.Path.Combine(fluid_general.App.AppDataPath, "log");
            string logFilePath = System.IO.Path.Combine(logFolderPath, $"{CurrentEvent}.txt");

            // ログフォルダが存在しない場合は作成
            if (!Directory.Exists(logFolderPath))
            {
                Directory.CreateDirectory(logFolderPath);
            }

            string searchCondition = SearchCondition.Text;
            string SearchKey = "";

            if (searchCondition == "学籍番号")
            {
                SearchKey = "StudentNumber";
            }
            else if (searchCondition == "部屋番号")
            {
                SearchKey = "RoomNumber";
            }
            else
            {
                MessageBox.Show("検索条件を選択してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return; // エラー時は処理を終了
            }

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx",
                Title = "欠席リストファイルをインポート"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string selectedFilePath = openFileDialog.FileName;

                    // 現在のイベントファイルパスを取得
                    string dataFolder = System.IO.Path.Combine(fluid_general.App.AppDataPath, "data");
                    string currentEventFilePath = System.IO.Path.Combine(dataFolder, $"{CurrentEvent}.xml");

                    if (!Directory.Exists(dataFolder))
                    {
                        MessageBox.Show("データフォルダが見つかりません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (!File.Exists(currentEventFilePath))
                    {
                        MessageBox.Show("現在のイベントファイルが見つかりません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 欠席者リストを取得
                    var absentList = new HashSet<string>();
                    using (var fs = new FileStream(selectedFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var workbook = new XLWorkbook(fs))
                    {
                        var worksheet = workbook.Worksheets.First();
                        foreach (var cell in worksheet.Column(1).CellsUsed())
                        {
                            string value = cell.GetValue<string>().Trim();
                            absentList.Add(value);
                        }
                    }

                    XDocument xmlDoc = XDocument.Load(currentEventFilePath);
                    var entries = xmlDoc.Descendants("Entry").ToList();
                    var registeredList = new HashSet<string>(); // 欠席登録した生徒
                    var missingStudents = new List<string>(); // 名簿に存在しなかった生徒

                    int totalEntries = entries.Count;
                    int processedEntries = 0;

                    // プログレスバーを表示
                    progressBar.Visibility = Visibility.Visible;
                    progressBar.Minimum = 0;
                    progressBar.Maximum = totalEntries;
                    progressBar.Value = 0;

                    await Task.Run(() =>
                    {
                        foreach (var entry in entries)
                        {
                            var searchElement = entry.Element(SearchKey);
                            if (searchElement != null && absentList.Contains(searchElement.Value.Trim()))
                            {
                                entry.Element("Status").Value = "不参加";
                                registeredList.Add($"部屋番号: {entry.Element("RoomNumber")?.Value ?? "不明"}, 名前: {entry.Element("Name")?.Value ?? "不明"}, 学籍番号: {entry.Element("StudentNumber")?.Value ?? "不明"}");
                            }

                            processedEntries++;

                            // UI スレッドでプログレスバーを更新
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                progressBar.Value = processedEntries;
                            });
                        }
                    });

                    // 名簿に存在しない欠席者リストを取得
                    missingStudents = absentList
                        .Where(item => !entries.Any(entry => entry.Element(SearchKey)?.Value.Trim() == item))
                        .Select(item => $"検索キー: {item}")
                        .ToList();

                    // missingデータの処理
                    if (missingStudents.Any())
                    {
                        string missingMessage = "以下の生徒は名簿に存在しません:\n" + string.Join("\n", missingStudents);
                        var dialogResult = MessageBox.Show(
                            missingMessage,
                            "名簿に存在しない生徒",
                            MessageBoxButton.OKCancel,
                            MessageBoxImage.Question
                        );

                        if (dialogResult == MessageBoxResult.Cancel)
                        {
                            MessageBox.Show("インポートをキャンセルしました。", "キャンセル", MessageBoxButton.OK, MessageBoxImage.Information);
                            progressBar.Visibility = Visibility.Collapsed;
                            return;
                        }
                    }
                    xmlDoc.Save(currentEventFilePath);
                    MessageBox.Show("欠席リストの取り込みが完了しました。", "完了", MessageBoxButton.OK, MessageBoxImage.Information);

                    // ログに記録
                    using (StreamWriter logWriter = new StreamWriter(logFilePath, true, Encoding.UTF8))
                    {
                        logWriter.WriteLine($"[{currentTime}] 欠席リストのインポート開始");

                        if (registeredList.Count > 0)
                        {
                            logWriter.WriteLine("欠席登録された生徒:");
                            foreach (var student in registeredList)
                            {
                                logWriter.WriteLine($"  - {student}");
                            }
                        }

                        if (missingStudents.Count > 0)
                        {
                            logWriter.WriteLine("名簿に存在しなかった生徒:");
                            foreach (var student in missingStudents)
                            {
                                logWriter.WriteLine($"  - {student}");
                            }
                        }

                        logWriter.WriteLine($"[{currentTime}] 欠席リストのインポート終了");
                    }

                    // インポート完了後にプログレスバーを非表示
                    progressBar.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    fluid_general.App.LogError(ex);
                    MessageBox.Show($"エラーが発生しました: {ex.Message}\n\n※ファイルにパスワードがかかっているか、別のプログラムで開かれている可能性があります。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    progressBar.Visibility = Visibility.Collapsed;
                }
            }
        }
        private async void ImportAttendClick(object sender, RoutedEventArgs e)
        {
            string currentTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"); // 現在の時間
            string logFolderPath = System.IO.Path.Combine(fluid_general.App.AppDataPath, "log");
            string logFilePath = System.IO.Path.Combine(logFolderPath, $"{CurrentEvent}.txt");

            // ログフォルダが存在しない場合は作成
            if (!Directory.Exists(logFolderPath))
            {
                Directory.CreateDirectory(logFolderPath);
            }

            string searchCondition = SearchCondition.Text;
            string SearchKey = "";

            if (searchCondition == "学籍番号")
            {
                SearchKey = "StudentNumber";
            }
            else if (searchCondition == "部屋番号")
            {
                SearchKey = "RoomNumber";
            }
            else
            {
                MessageBox.Show("検索条件を選択してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return; // エラー時は処理を終了
            }

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx",
                Title = "出席リストファイルをインポート"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string selectedFilePath = openFileDialog.FileName;

                    // 現在のイベントファイルパスを取得
                    string dataFolder = System.IO.Path.Combine(fluid_general.App.AppDataPath, "data");
                    string currentEventFilePath = System.IO.Path.Combine(dataFolder, $"{CurrentEvent}.xml");

                    if (!Directory.Exists(dataFolder))
                    {
                        MessageBox.Show("データフォルダが見つかりません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (!File.Exists(currentEventFilePath))
                    {
                        MessageBox.Show("現在のイベントファイルが見つかりません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 出席者リストを取得
                    var attendList = new HashSet<string>();
                    using (var fs = new FileStream(selectedFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var workbook = new XLWorkbook(fs))
                    {
                        var worksheet = workbook.Worksheets.First();
                        foreach (var cell in worksheet.Column(1).CellsUsed())
                        {
                            string value = cell.GetValue<string>().Trim();
                            attendList.Add(value);
                        }
                    }

                    XDocument xmlDoc = XDocument.Load(currentEventFilePath);
                    var entries = xmlDoc.Descendants("Entry").ToList();
                    var registeredList = new HashSet<string>(); // 出席登録した生徒
                    var missingStudents = new List<string>(); // 名簿に存在しなかった生徒

                    int totalEntries = entries.Count;
                    int processedEntries = 0;

                    // プログレスバーを表示
                    progressBar.Visibility = Visibility.Visible;
                    progressBar.Minimum = 0;
                    progressBar.Maximum = totalEntries;
                    progressBar.Value = 0;

                    await Task.Run(() =>
                    {
                        foreach (var entry in entries)
                        {
                            var searchElement = entry.Element(SearchKey);
                            if (searchElement != null && attendList.Contains(searchElement.Value.Trim()))
                            {
                                entry.Element("Status").Value = "参加済み";
                                registeredList.Add($"部屋番号: {entry.Element("RoomNumber")?.Value ?? "不明"}, 名前: {entry.Element("Name")?.Value ?? "不明"}, 学籍番号: {entry.Element("StudentNumber")?.Value ?? "不明"}");
                            }

                            processedEntries++;

                            // UI スレッドでプログレスバーを更新
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                progressBar.Value = processedEntries;
                            });
                        }
                    });

                    // 名簿に存在しない欠席者リストを取得
                    missingStudents = attendList
                        .Where(item => !entries.Any(entry => entry.Element(SearchKey)?.Value.Trim() == item))
                        .Select(item => $"検索キー: {item}")
                        .ToList();

                    // missingデータの処理
                    if (missingStudents.Any())
                    {
                        string missingMessage = "以下の生徒は名簿に存在しません:\n" + string.Join("\n", missingStudents);
                        var dialogResult = MessageBox.Show(
                            missingMessage,
                            "名簿に存在しない生徒",
                            MessageBoxButton.OKCancel,
                            MessageBoxImage.Question
                        );

                        if (dialogResult == MessageBoxResult.Cancel)
                        {
                            MessageBox.Show("インポートをキャンセルしました。", "キャンセル", MessageBoxButton.OK, MessageBoxImage.Information);
                            progressBar.Visibility = Visibility.Collapsed;
                            return;
                        }
                    }
                    xmlDoc.Save(currentEventFilePath);
                    MessageBox.Show("出席リストの取り込みが完了しました。", "完了", MessageBoxButton.OK, MessageBoxImage.Information);

                    // ログに記録
                    using (StreamWriter logWriter = new StreamWriter(logFilePath, true, Encoding.UTF8))
                    {
                        logWriter.WriteLine($"[{currentTime}] 出席リストのインポート開始");

                        if (registeredList.Count > 0)
                        {
                            logWriter.WriteLine("出席登録された生徒:");
                            foreach (var student in registeredList)
                            {
                                logWriter.WriteLine($"  - {student}");
                            }
                        }

                        if (missingStudents.Count > 0)
                        {
                            logWriter.WriteLine("名簿に存在しなかった生徒:");
                            foreach (var student in missingStudents)
                            {
                                logWriter.WriteLine($"  - {student}");
                            }
                        }

                        logWriter.WriteLine($"[{currentTime}] 出席リストのインポート終了");
                    }

                    // インポート完了後にプログレスバーを非表示
                    progressBar.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    fluid_general.App.LogError(ex);
                    MessageBox.Show($"エラーが発生しました: {ex.Message}\n\n※ファイルにパスワードがかかっているか、別のプログラムで開かれている可能性があります。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    progressBar.Visibility = Visibility.Collapsed;
                }
            }
        }
        private async void ImportEventClick(object sender, RoutedEventArgs e)
        {
            string currentTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"); // 現在の時間
            string logFolderPath = System.IO.Path.Combine(fluid_general.App.AppDataPath, "log");
            string logFilePath = System.IO.Path.Combine(logFolderPath, $"{CurrentEvent}.txt");
            // ログフォルダが存在しない場合は作成
            if (!Directory.Exists(logFolderPath))
            {
                Directory.CreateDirectory(logFolderPath);
            }


            // 検索条件の取得
            string searchCondition = SearchCondition.Text;
            string SearchKey = "";

            if (searchCondition == "学籍番号")
            {
                SearchKey = "StudentNumber";
            }
            else if (searchCondition == "部屋番号")
            {
                SearchKey = "RoomNumber";
            }
            else
            {
                MessageBox.Show("検索条件を選択してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return; // エラー時は処理を終了
            }

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "XML Files (*.xml)|*.xml",
                Title = "イベントファイルをインポート"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string selectedFilePath = openFileDialog.FileName;

                    // dataフォルダの確認
                    string dataFolder = System.IO.Path.Combine(fluid_general.App.AppDataPath, "data");
                    if (!Directory.Exists(dataFolder))
                    {
                        MessageBox.Show("データフォルダが見つかりません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 現在のイベントファイルパスを取得
                    string currentEventFilePath = System.IO.Path.Combine(dataFolder, $"{CurrentEvent}.xml");
                    if (!File.Exists(currentEventFilePath))
                    {
                        MessageBox.Show("現在のイベントファイルが見つかりません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // XMLファイルを読み込み
                    var sourceDoc = System.Xml.Linq.XDocument.Load(selectedFilePath);
                    var currentDoc = System.Xml.Linq.XDocument.Load(currentEventFilePath);

                    var sourceEntries = sourceDoc.Descendants("Entry");
                    var currentEntries = currentDoc.Descendants("Entry").ToList();
                    var sameStudentList = new HashSet<string>(); // 不参加登録した生徒を格納する
                    var errorStudentList = new HashSet<string>(); // エラーが発生した生徒を格納する
                    var newStudentList = new HashSet<string>(); // 新たに登録された生徒を格納する
                    var importedStudentList = new List<string>(); // 正常にインポートされた生徒

                    int totalEntries = sourceEntries.Count();
                    int processedEntries = 0;

                    // プログレスバーを表示
                    progressBar.Visibility = Visibility.Visible;
                    progressBar.Minimum = 0;
                    progressBar.Maximum = totalEntries;
                    progressBar.Value = 0;

                    await Task.Run(() =>
                    {
                        foreach (var sourceEntry in sourceEntries)
                        {
                            var studentNumberElement = sourceEntry.Element("StudentNumber")?.Value ?? "不明";
                            var roomNumberElement = sourceEntry.Element("RoomNumber")?.Value ?? "不明";
                            var nameElement = sourceEntry.Element("Name")?.Value ?? "不明";
                            var sourceStatusElement = sourceEntry.Element("Status")?.Value ?? "不明";

                            var SearchElement = SearchKey == "StudentNumber" ? studentNumberElement : roomNumberElement;

                            if (SearchElement == null || sourceStatusElement == null)
                            {
                                errorStudentList.Add($"部屋番号: {roomNumberElement}, 名前: {nameElement}, 学籍番号: {studentNumberElement}"); //学籍番号や部屋番号が存在しない生徒を格納
                                continue;
                            }


                            if (string.IsNullOrWhiteSpace(SearchKey) || sourceStatusElement != "参加済み")
                                continue;

                            // 現在のイベントに同じ学籍番号または部屋番号があるか確認
                            var matchingEntry = currentEntries.FirstOrDefault(entry =>
                                entry.Element(SearchKey)?.Value == SearchElement);

                            if (matchingEntry != null)
                            {
                                var currentStatusElement = matchingEntry.Element("Status");

                                if (currentStatusElement != null && currentStatusElement.Value == "参加済み")
                                {
                                    // 両方で参加済みの生徒を格納
                                    sameStudentList.Add($"部屋番号: {roomNumberElement} , 名前:  {nameElement} , 学籍番号:  {studentNumberElement}");
                                }
                                else
                                {
                                    // 正常にインポートされた生徒をリストに追加
                                    importedStudentList.Add($"部屋番号: {roomNumberElement} , 名前:  {nameElement} , 学籍番号:  {studentNumberElement}");
                                }

                                // 上書き処理: インポート元のデータを使用
                                currentStatusElement?.SetValue(sourceStatusElement);
                            }
                            else
                            {
                                // 元のファイルにいなかった生徒を格納
                                newStudentList.Add($"部屋番号: {roomNumberElement}, 名前: {nameElement}, 学籍番号: {studentNumberElement}");
                                currentDoc.Root.Element("Entries")?.Add(sourceEntry);
                            }

                            processedEntries++;

                            // UI スレッドでプログレスバーを更新
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                progressBar.Value = processedEntries;
                            });
                        }
                    });

                    // 重複データの処理
                    if (sameStudentList.Count > 0)
                    {
                        string samestudentMessage = "以下の生徒は両方のファイルで「参加済み」です。:\n" + string.Join("\n", sameStudentList);
                        var dialogResult = MessageBox.Show(
                            samestudentMessage,
                            "データの重複",
                            MessageBoxButton.OKCancel,
                            MessageBoxImage.Question
                        );

                        if (dialogResult == MessageBoxResult.Cancel)
                        {
                            MessageBox.Show("インポートをキャンセルしました。", "キャンセル", MessageBoxButton.OK, MessageBoxImage.Information);
                            progressBar.Visibility = Visibility.Collapsed;
                            return;
                        }
                    }
                    //データ不足の生徒の処理
                    if (errorStudentList.Count > 0)
                    {
                        string errorstudentMessage = "以下の生徒は" + SearchKey + "のデータを持っていません。検索条件を変更して再度インポートしてください。:\n" + string.Join("\n", errorStudentList);
                        var dialogResult = MessageBox.Show(
                            errorstudentMessage,
                            "情報がない生徒",
                            MessageBoxButton.OKCancel,
                            MessageBoxImage.Question
                        );

                        if (dialogResult == MessageBoxResult.Cancel)
                        {
                            MessageBox.Show("インポートをキャンセルしました。", "キャンセル", MessageBoxButton.OK, MessageBoxImage.Information);
                            progressBar.Visibility = Visibility.Collapsed;
                            return;
                        }
                    }
                    // 新たに登録された生徒の処理
                    if (newStudentList.Count > 0)
                    {
                        string samestudentMessage = "以下の生徒はマージ先にいません。:\n" + string.Join("\n", newStudentList);
                        var dialogResult = MessageBox.Show(
                            samestudentMessage,
                            "マージ先に存在しない生徒",
                            MessageBoxButton.OKCancel,
                            MessageBoxImage.Question
                        );

                        if (dialogResult == MessageBoxResult.Cancel)
                        {
                            MessageBox.Show("インポートをキャンセルしました。", "キャンセル", MessageBoxButton.OK, MessageBoxImage.Information);
                            progressBar.Visibility = Visibility.Collapsed;
                            return;
                        }
                    }

                    // 重複データ、データ不足、新規生徒、正常にインポートされた生徒をログに記録
                    using (StreamWriter logWriter = new StreamWriter(logFilePath, true, Encoding.UTF8))
                    {
                        logWriter.WriteLine($"[{currentTime}] インポート処理開始");

                        if (sameStudentList.Count > 0)
                        {
                            logWriter.WriteLine("重複データ:");
                            foreach (var student in sameStudentList)
                            {
                                logWriter.WriteLine($"  - {student}");
                            }
                        }

                        if (errorStudentList.Count > 0)
                        {
                            logWriter.WriteLine("データ不足の生徒:");
                            foreach (var student in errorStudentList)
                            {
                                logWriter.WriteLine($"  - {student}");
                            }
                        }

                        if (newStudentList.Count > 0)
                        {
                            logWriter.WriteLine("新規生徒:");
                            foreach (var student in newStudentList)
                            {
                                logWriter.WriteLine($"  - {student}");
                            }
                        }

                        if (importedStudentList.Count > 0)
                        {
                            logWriter.WriteLine("正常にインポートされた生徒:");
                            foreach (var student in importedStudentList)
                            {
                                logWriter.WriteLine($"  - {student}");
                            }
                        }

                        logWriter.WriteLine($"[{currentTime}] インポート処理終了");
                    }
                    // 競合チェック
                    try
                    {
                        using (FileStream fs = new FileStream(currentEventFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        {
                            // ファイルが使用中でなければ処理を続行
                        }
                    }
                    catch (IOException)
                    {
                        MessageBox.Show("現在のイベントファイルが使用中です。アプリを閉じてから再試行してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                        progressBar.Visibility = Visibility.Collapsed;
                        return;
                    }

                    // マージ結果を保存
                    currentDoc.Save(currentEventFilePath);

                    // インポート完了後にプログレスバーを非表示
                    progressBar.Visibility = Visibility.Collapsed;

                    MessageBox.Show("インポートが完了しました。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);

                    // ダイアログを閉じる
                    this.Hide();
                }
                catch (Exception ex)
                {
                    fluid_general.App.LogError(ex);
                    MessageBox.Show($"エラーが発生しました: {ex.Message}\n\n※ファイルにパスワードがかかっているか、別のプログラムで開かれている可能性があります。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    progressBar.Visibility = Visibility.Collapsed;
                }
            }
        }


    }
}
