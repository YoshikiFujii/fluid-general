using ClosedXML.Excel;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml.Linq;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Runtime.InteropServices;
using Spire.Doc;
using Spire.Doc.Documents;
using PdfSharp.Pdf.Content.Objects;
using System.Threading.Tasks;
using System.Threading;

namespace fluid_general.Pages
{
    /// <summary>
    /// ExportListWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class ExportPDFWindow : Window
    {
        private string CurrentEvent;
        private string eventFilePath;
        private CancellationTokenSource cancellationTokenSource;
        private RosterInfo rosterInfo;
        public ExportPDFWindow(string EventFilePath, string currentEvent)
        {
            eventFilePath = EventFilePath;
            CurrentEvent = currentEvent;

            InitializeComponent();
            GetDefault();
            
        }
        private void GetDefault()
        {
            DateTime dt = DateTime.Now;
            MonthTextBox.Text = dt.Month.ToString();
            DayTextBox.Text = dt.Day.ToString();

            HeadTextBox.Text = Properties.Settings.Default.DormHead;
            ChairPersonTextBox.Text = Properties.Settings.Default.ChairPerson;
            YearTextBox.Text = Properties.Settings.Default.Wareki;
            PointTextBox.Text = Properties.Settings.Default.Point;
            ReasonTextBox.Text = Properties.Settings.Default.Reason;

        }
        private async void ExportPDF_click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.DormHead = HeadTextBox.Text;
            Properties.Settings.Default.ChairPerson = ChairPersonTextBox.Text;
            Properties.Settings.Default.Wareki = YearTextBox.Text;
            Properties.Settings.Default.Point = PointTextBox.Text;
            Properties.Settings.Default.Reason = ReasonTextBox.Text;
            Properties.Settings.Default.Save(); // 設定を保存

            ProgressWindow progressWindow = new ProgressWindow();
            progressWindow.Show();

            cancellationTokenSource = new CancellationTokenSource();
            progressWindow.Closed += (s, args) => cancellationTokenSource.Cancel(); // ProgressWindowが閉じられたらキャンセル

            try
            {
                // PDF生成処理を非同期で実行
                await Task.Run(() => GeneratePDF(progressWindow, cancellationTokenSource.Token));
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("処理がキャンセルされました。", "キャンセル", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"エラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // プログレスウィンドウを閉じる
                progressWindow.Close();
                cancellationTokenSource.Dispose();
            }
        }

        private void GeneratePDF(ProgressWindow progressWindow, CancellationToken cancellationToken)
        {
            string wordFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "DemeritNoticeBase.docx");

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "PDFファイル(*.pdf)|*.pdf",
                Title = "PDFファイルの保存先を選択してください",
                FileName = $"{CurrentEvent}_減点通知書.pdf"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                string selectedPath = saveFileDialog.FileName;
                // 一時ファイルのパスを生成
                string tempFilePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Temp_{Guid.NewGuid()}.docx");

                try
                {
                    // 元のファイルを一時ファイルにコピー
                    File.Copy(wordFilePath, tempFilePath, true);

                    // Spire.Doc を使用して Word ドキュメントをロード
                    using (Spire.Doc.Document document = new Spire.Doc.Document())
                    {
                        document.LoadFromFile(tempFilePath);

                        // UI要素の存在確認と値の取得をUIスレッドで行う
                        string dormHead = string.Empty;
                        string chairPerson = string.Empty;
                        string year = string.Empty;
                        string month = string.Empty;
                        string day = string.Empty;
                        string point = string.Empty;
                        string reason = string.Empty;

                        Dispatcher.Invoke(() =>
                        {
                            dormHead = HeadTextBox.Text;
                            chairPerson = ChairPersonTextBox.Text;
                            year = YearTextBox.Text;
                            month = MonthTextBox.Text;
                            day = DayTextBox.Text;
                            point = PointTextBox.Text;
                            reason = ReasonTextBox.Text;
                        });

                        document.Replace("{dormitoryhead}", dormHead, true, true);
                        document.Replace("{chairperson}", chairPerson, true, true);
                        document.Replace("{year}", year, true, true);
                        document.Replace("{month}", month, true, true);
                        document.Replace("{day}", day, true, true);
                        document.Replace("{point}", point, true, true);
                        document.Replace("{reason}", reason, true, true);

                        document.SaveToFile(tempFilePath, FileFormat.Docx);
                    }
                    // XML ファイルを読み込み
                    XDocument eventDoc = XDocument.Load(eventFilePath);
                    var entries = eventDoc.Descendants("Entry").Where(x => x.Element("Status")?.Value == "未参加").ToList();

                    if (entries.Count == 0)
                    {
                        MessageBox.Show("対象となるエントリが見つかりませんでした。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // 個別のPDFを生成して後で結合する方法
                    string tempDir = Path.Combine(Path.GetTempPath(), $"PdfExport_{Guid.NewGuid()}");
                    Directory.CreateDirectory(tempDir);

                    try
                    {
                        List<string> pdfFiles = new List<string>();

                        // 各エントリに対して個別のPDFファイルを生成
                        for (int i = 0; i<entries.Count; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested(); // キャンセル要求を確認

                            var entry = entries[i];
                            string name = entry.Element("Name")?.Value ?? "";
                            string roomNumber = entry.Element("RoomNumber")?.Value ?? "";
                            string tempPdfPath = Path.Combine(tempDir, $"temp_{i}.pdf");

                            using (Spire.Doc.Document document = new Spire.Doc.Document())
                            {
                                document.LoadFromFile(tempFilePath);

                                document.Replace("{roomnumber}", roomNumber, true, true);
                                document.Replace("{name}", name, true, true);

                                document.SaveToFile(tempPdfPath, FileFormat.PDF);
                            }

                            pdfFiles.Add(tempPdfPath);

                            // プログレスバーを更新
                            int progress = (int)((i + 1) / (double)entries.Count * 100);
                            progressWindow.Dispatcher.Invoke(() => progressWindow.UpdateProgress(progress));
                        }

                        // PDFSharpを使用して複数のPDFを結合
                        if (pdfFiles.Count > 0)
                        {
                            using (PdfDocument outputDocument = new PdfDocument())
                            {
                                foreach (string file in pdfFiles)
                                {
                                    cancellationToken.ThrowIfCancellationRequested(); // キャンセル要求を確認

                                    using (PdfDocument inputDocument = PdfReader.Open(file, PdfDocumentOpenMode.Import))
                                    {
                                        for (int i = 0; i < inputDocument.PageCount; i++)
                                        {
                                            outputDocument.AddPage(inputDocument.Pages[i]);
                                        }
                                    }
                                }

                                outputDocument.Save(selectedPath);
                            }
                            Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"PDFが正常に保存されました: {selectedPath}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                            });
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show("処理がキャンセルされました。", "キャンセル", MessageBoxButton.OK, MessageBoxImage.Information);
                        });
                    }
                    finally
                    {
                        // 一時ファイルとディレクトリの削除
                        try
                        {
                            if (Directory.Exists(tempDir))
                            {
                                Directory.Delete(tempDir, true);
                            }
                        }
                        catch
                        {
                            // 削除に失敗しても処理を継続
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"エラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
