using ClosedXML.Excel;
using Microsoft.Win32;
using ModernWpf.Controls;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

namespace fluid_general.Pages
{
    /// <summary>
    /// Page2.xaml の相互作用ロジック
    /// </summary>
    public partial class RosterPage : System.Windows.Controls.Page
    {
        private string rosterFolderPath = System.IO.Path.Combine(fluid_general.App.AppDataPath, "roster");

        public RosterPage()
        {
            InitializeComponent();

            // Create roster folder if it doesn't exist
            if (!Directory.Exists(rosterFolderPath))
            {
                Directory.CreateDirectory(rosterFolderPath);
            }

            // Load existing roster files into DataGrid on initialization
            LoadRosterFiles();
        }
        private async void ImportRoster_Click(object sender, RoutedEventArgs e)
        {
            // Open file dialog to select Excel or XML file
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Excel or XML Files|*.xlsx;*.xml",
                Title = "名簿ファイルを開く"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string selectedFilePath = openFileDialog.FileName;
                    string fileExtension = System.IO.Path.GetExtension(selectedFilePath).ToLower();

                    // If XML is selected, skip conversion
                    if (fileExtension == ".xml")
                    {
                        // Simply load the XML into the DataGrid
                        string xmlFileName = System.IO.Path.GetFileName(selectedFilePath);
                        string xmlFilePath = System.IO.Path.Combine(rosterFolderPath, xmlFileName);

                        // Copy the selected XML file to the roster folder if not already exists
                        if (!File.Exists(xmlFilePath))
                        {
                            File.Copy(selectedFilePath, xmlFilePath);
                        }

                        // Show success message
                        ContentDialog ImportSuccessDialog = new ContentDialog
                        {
                            Title = "ファイルインポート",
                            Content = "XMLファイルのインポートが完了しました。",
                            CloseButtonText = "閉じる"
                        };

                        await ImportSuccessDialog.ShowAsync();
                    }
                    else if (fileExtension == ".xlsx")
                    {
                        string xmlFileName = System.IO.Path.GetFileNameWithoutExtension(selectedFilePath) + ".xml";
                        string xmlFilePath = System.IO.Path.Combine(rosterFolderPath, xmlFileName);

                        // Convert Excel to XML
                        using (var fs = new FileStream(selectedFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var workbook = new XLWorkbook(fs))
                        {
                            var worksheet = workbook.Worksheets.First();

                            // 名簿人数カウント
                            int rowCount = worksheet.RowsUsed().Count();
                            progressBar.Maximum = rowCount;
                            progressBar.Visibility = Visibility.Visible;

                            // 名簿情報を追加
                            XElement rosterInfo = new XElement("RosterInfo",
                                new XElement("TotalCount", rowCount),
                                new XElement("NameCol", 6),
                                new XElement("StudentNumberCol", 9),
                                new XElement("RoomNumberCol", 1),
                                new XElement("KanaCol", 7),
                                new XElement("GenderCol", 4),
                                new XElement("DepartCol", 10),
                                new XElement("YearCol", 5)
                            );

                            // Create XML document
                            XElement roster = new XElement("Roster");
                            await Task.Run(() =>
                            {
                                int progress = 0;
                                foreach (var row in worksheet.RowsUsed())
                                {
                                    XElement entry = new XElement("Entry",
                                        from cell in row.Cells()
                                        select new XElement("Cell", cell.Value.ToString())
                                    );
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        roster.Add(entry);
                                        progressBar.Value = ++progress;
                                    });
                                }
                            });
                            // Create XML document with RosterInfo first
                            XDocument xmlDocument = new XDocument(new XElement("Root", rosterInfo, roster));

                            // Save the XML document
                            xmlDocument.Save(xmlFilePath);

                            progressBar.Visibility = Visibility.Collapsed;
                            progressBar.Value = 0;
                        }

                        // Show success message
                        ContentDialog ConvertSuccessDialog = new ContentDialog
                        {
                            Title = "ファイル変換",
                            Content = "インポートされたファイルをXMLに変換しました。",
                            CloseButtonText = "閉じる"
                        };

                        await ConvertSuccessDialog.ShowAsync();
                        RosterOption(xmlFileName);
                    }

                    // Reload roster files into DataGrid
                    LoadRosterFiles();
                }
                catch (Exception ex)
                {
                    fluid_general.App.LogError(ex);
                    MessageBox.Show($"エラーが発生しました: {ex.Message}\n\n※ファイルにパスワードがかかっているか、別のプログラムで開かれている可能性があります。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Method to load the list of XML files from the roster folder into the DataGrid
        private void LoadRosterFiles()
        {
            var xmlFiles = Directory.GetFiles(rosterFolderPath, "*.xml")
                                    .Select(f =>
                                    {
                                        var xmlDoc = XDocument.Load(f);
                                        var totalCount = xmlDoc.Root.Element("RosterInfo")?.Element("TotalCount")?.Value ?? "不明";
                                        return new
                                        {
                                            FileName = System.IO.Path.GetFileName(f),
                                            TotalCount = totalCount
                                        };
                                    })
                                    .ToList();
            RosterDataGrid.ItemsSource = xmlFiles;
        }
        private void RosterOptionClick(object sender, RoutedEventArgs e)
        {
            if (RosterDataGrid.SelectedItem != null)
            {
                dynamic selectedItem = RosterDataGrid.SelectedItem;
                string selectedFileName = selectedItem.FileName;

                RosterOption(selectedFileName);
            }
            else
            {
                MessageBox.Show("名簿ファイルを選択してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
        private async void RosterOption(string selectedFileName)
        {
            string xmlFilePath = System.IO.Path.Combine(rosterFolderPath, selectedFileName);

            if (File.Exists(xmlFilePath))
            {
                // XMLファイルを読み込み
                XDocument xmlDoc = XDocument.Load(xmlFilePath);
                XElement rosterInfo = xmlDoc.Root.Element("RosterInfo");

                if (rosterInfo != null)
                {
                    // XMLファイルの値を取得
                    int nameCol = (int?)rosterInfo.Element("NameCol") ?? 0;
                    int snCol = (int?)rosterInfo.Element("StudentNumberCol") ?? 0;
                    int rnCol = (int?)rosterInfo.Element("RoomNumberCol") ?? 0;
                    int kanaCol = (int?)rosterInfo.Element("KanaCol") ?? 0;
                    int genderCol = (int?)rosterInfo.Element("GenderCol") ?? 0;
                    int departCol = (int?)rosterInfo.Element("DepartCol") ?? 0;
                    int yearCol = (int?)rosterInfo.Element("YearCol") ?? 0;

                    // ダイアログに初期値を設定して表示
                    var dialog = new RosterDialog(System.IO.Path.GetFileNameWithoutExtension(selectedFileName), nameCol, snCol, rnCol, kanaCol, genderCol, departCol, yearCol);
                    var result = await dialog.ShowAsync();

                    if (result == ContentDialogResult.Primary)
                    {
                        try
                        {
                            // ダイアログから取得したデータ
                            string newRosterName = dialog.rostername;
                            int newNameCol = dialog.namenum;
                            int newSnCol = dialog.snnum;
                            int newRnCol = dialog.rnnum;
                            int newKnCol = dialog.kananum;
                            int newGdCol = dialog.gendernum;
                            int newDpCol = dialog.departnum;
                            int newYrCol = dialog.yearnum;

                            string newFileName = $"{newRosterName}.xml";
                            string newFilePath = System.IO.Path.Combine(rosterFolderPath, newFileName);


                            // 変更があった項目のみ更新
                            if (newNameCol != nameCol)
                            {
                                rosterInfo.SetElementValue("NameCol", newNameCol);
                            }
                            if (newSnCol != snCol)
                            {
                                rosterInfo.SetElementValue("StudentNumberCol", newSnCol);
                            }
                            if (newRnCol != rnCol)
                            {
                                rosterInfo.SetElementValue("RoomNumberCol", newRnCol);
                            }
                            if (newKnCol != kanaCol)
                            {
                                rosterInfo.SetElementValue("KanaCol", newKnCol);
                            }
                            if (newGdCol != genderCol)
                            {
                                rosterInfo.SetElementValue("GenderCol", newGdCol);
                            }
                            if (newDpCol != departCol)
                            {
                                rosterInfo.SetElementValue("DepartCol", newDpCol);
                            }
                            if (newYrCol != yearCol)
                            {
                                rosterInfo.SetElementValue("YearCol", newYrCol);
                            }
                            // ファイル名が変更された場合
                            if (selectedFileName != newFileName)
                            {
                                if (File.Exists(newFilePath))
                                {
                                    // ファイル名が重複している場合のエラーメッセージ
                                    ContentDialog DuplicationErrorDialog = new ContentDialog
                                    {
                                        Title = "エラー",
                                        Content = "同じ名前のファイルがすでに存在します。別の名前を指定してください。",
                                        CloseButtonText = "OK"
                                    };

                                    await DuplicationErrorDialog.ShowAsync();
                                    return;
                                }
                                else
                                {
                                    // ファイル名を変更
                                    File.Move(xmlFilePath, newFilePath);
                                }
                            }
                            // 更新されたXMLを保存
                            xmlDoc.Save(newFilePath);

                            // 成功メッセージ
                            ContentDialog ChangedDialog = new ContentDialog
                            {
                                Title = "成功",
                                Content = "変更内容を保存しました。",
                                CloseButtonText = "OK"
                            };

                            await ChangedDialog.ShowAsync();

                            // 名簿リストを再読み込み
                            LoadRosterFiles();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"エラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                else
                {
                    MessageBox.Show("RosterInfo が見つかりませんでした。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

        }
        private async void DeleteSelectedItem(object sender, RoutedEventArgs e)
        {
            if (RosterDataGrid.SelectedItem != null)
            {
                dynamic selectedItem = RosterDataGrid.SelectedItem;
                string name = selectedItem.FileName;
                string filePath = System.IO.Path.Combine(rosterFolderPath, name);

                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    ContentDialog deleteFileDialog = new ContentDialog
                    {
                        Title = "削除",
                        Content = "本当に名簿をを削除しますか？",
                        PrimaryButtonText = "削除",
                        CloseButtonText = "キャンセル"
                    };

                    ContentDialogResult dFDresult = await deleteFileDialog.ShowAsync();

                    if (dFDresult == ContentDialogResult.Primary)
                    {
                        try
                        {
                            File.Delete(filePath);
                            ContentDialog deleteFileResultDialog = new ContentDialog
                            {
                                Title = "削除",
                                Content = "名簿の削除に成功しました。",
                                CloseButtonText = "閉じる"
                            };

                            await deleteFileResultDialog.ShowAsync();
                            LoadRosterFiles();
                        }
                        catch (Exception)
                        {
                            ContentDialog deleteFileErrorDialog = new ContentDialog
                            {
                                Title = "エラー",
                                Content = "名簿の削除に失敗しました。",
                                CloseButtonText = "閉じる"
                            };

                            await deleteFileErrorDialog.ShowAsync();
                        }
                    }
                }
                else
                {
                    ContentDialog deleteFileError1Dialog = new ContentDialog
                    {
                        Title = "エラー",
                        Content = "選択されたファイルはありません。",
                        CloseButtonText = "閉じる"
                    };

                    await deleteFileError1Dialog.ShowAsync();
                }
            }
        }
    }
}
