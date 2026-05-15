using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace fluid_general.Pages
{
    public partial class EventPage : System.Windows.Controls.Page
    {
        private ObservableCollection<Event> Events = new ObservableCollection<Event>();
        private string dataFolder = System.IO.Path.Combine(fluid_general.App.AppDataPath, "data");


        public EventPage()
        {
            InitializeComponent();

            // dataフォルダが存在しない場合は作成
            if (!Directory.Exists(dataFolder))
            {
                Directory.CreateDirectory(dataFolder);
            }
            LoadEventsFromXml();
            // DataGridにイベントを表示
            EventList.ItemsSource = Events;
            Events.CollectionChanged += Events_CollectionChanged;
            UpdateEventListVisibility();

        }
        private void Page_DragOver(object sender, DragEventArgs e)
        {
            // ファイルがドラッグされている場合のみ許可
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }
        private void Page_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string file in files)
                {
                    // XMLファイルを読み込む
                    if (Path.GetExtension(file).ToLower() == ".xml")
                    {
                        try
                        {
                            string fileName = System.IO.Path.GetFileName(file);
                            string destinationPath = System.IO.Path.Combine(dataFolder, fileName);

                            // 同名ファイルが存在する場合、ファイル名に番号を付加
                            int count = 1;
                            while (File.Exists(destinationPath))
                            {
                                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
                                string extension = Path.GetExtension(file);
                                string newFileName = $"{fileNameWithoutExtension}({count}){extension}";
                                destinationPath = Path.Combine(dataFolder, newFileName);
                                count++;
                            }
                            File.Copy(file, destinationPath, overwrite: false);
                            LoadEventsFromXml();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error loading file {file}: {ex.Message}");
                        }
                    }
                    else
                    {
                        MessageBox.Show("イベントファイルをドロップしてください。");
                    }
                }
            }
        }

        private void TextBlock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (EventList.SelectedItem != null)
            {
                var selectedEvent = (Event)EventList.SelectedItem;
                OpenEventWindow(selectedEvent);
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            // dataフォルダを開く
            System.Diagnostics.Process.Start("explorer.exe", dataFolder);
        }
        private void Events_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateEventListVisibility();
        }

        private void Event_Open(object sender, RoutedEventArgs e)
        {
            if (EventList.SelectedItem != null)
            {
                var selectedEvent = (Event)EventList.SelectedItem;
                OpenEventWindow(selectedEvent);
            }
        }
        private void OpenEventWindow(Event selectedEvent)
        {
            // 選択されたイベントを渡してEventWindowを開く
            EventWindow eventWindow = new EventWindow(selectedEvent);
            eventWindow.Show();
            // 現在のMainWindowを閉じる
            var mainWindow = Application.Current.MainWindow;
            mainWindow.Close();
        }

        // イベントリストの内容に応じて表示を切り替える
        private void UpdateEventListVisibility()
        {
            if (Events.Count == 0)
            {
                // イベントがない場合、メッセージを表示し、DataGrid を隠す
                EmptyMessageTextBlock.Visibility = Visibility.Visible;
                EventList.Visibility = Visibility.Collapsed;
            }
            else
            {
                // イベントがある場合、DataGrid を表示し、メッセージを隠す
                EmptyMessageTextBlock.Visibility = Visibility.Collapsed;
                EventList.Visibility = Visibility.Visible;
            }
        }
        private void LoadEventsFromXml()
        {
            // dataフォルダ内のすべてのXMLファイルを取得
            var files = Directory.GetFiles(dataFolder, "*.xml");

            // ObservableCollectionをクリア
            Events.Clear();

            foreach (var file in files)
            {
                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(Event));
                    using (FileStream fs = new FileStream(file, FileMode.Open))
                    {
                        // XMLからイベントを読み込み
                        Event ev = (Event)serializer.Deserialize(fs);
                        // ファイル名から拡張子を除いた名前を EventName に設定
                        ev.EventName = Path.GetFileNameWithoutExtension(file);
                        Events.Add(ev);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading file {file}: {ex.Message}");
                }
            }
        }
        private void SaveEventToXml(Event ev)
        {
            try
            {
                string fileName = System.IO.Path.Combine(dataFolder, $"{ev.EventName}.xml");
                XmlSerializer serializer = new XmlSerializer(typeof(Event));
                using (FileStream fs = new FileStream(fileName, FileMode.Create))
                {
                    // イベントをXMLファイルに保存
                    serializer.Serialize(fs, ev);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving event {ev.EventName}: {ex.Message}");
            }
        }
        private async void NewEvent_Click(object sender, RoutedEventArgs e)
        {
            LoadEventsFromXml();
            var dialog = new EventDialog();
            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                string eventName = dialog.EventName;
                string eventDate = dialog.EventDate.Date.ToLongDateString();
                string rosterFilePath = System.IO.Path.Combine(fluid_general.App.AppDataPath, "roster", $"{dialog.Roster}.xml");

                // 新しいイベントを作成
                Event newEvent = new Event
                {
                    EventName = eventName,
                    EventDate = eventDate,
                    Participants = 0,
                    Status = "予定",
                    TouchSound = "JR",
                    SameStudentSetting = true,
                    RosterName = dialog.Roster,
                };
                RosterInfo EventColInfo = new RosterInfo
                {
                    RoomNumberCol = 1,
                    GenderCol = 2,
                    NameCol = 3,
                    KanaCol = 4,
                    StudentNumberCol = 5,
                    DepartCol = 6,
                    YearCol = 7
                };

                try
                {
                    // 名簿ファイルを読み込む
                    var rosterDoc = XDocument.Load(rosterFilePath);
                    // RosterInfo を解析
                    var rosterInfoElement = rosterDoc.Descendants("RosterInfo").FirstOrDefault();
                    var rosterInfo = RosterInfo.FromXml(rosterInfoElement);
                    newEvent.RosterInfo = EventColInfo;

                    // Roster のデータを読み込む
                    var rosterEntries = rosterDoc.Descendants("Entry")
                        .Select(entry => RosterEntry.FromCells(
                            entry.Elements("Cell").Select(cell => cell.Value).ToList(),
                            rosterInfo))
                        .ToList();

                    // 一人目の参加者のステータスを "参加状況" に設定
                    if (rosterEntries.Count > 0)
                    {
                        rosterEntries[0].Status = "参加状況";
                    }

                    newEvent.Roster = rosterEntries;

                    // 参加人数を設定
                    newEvent.Participants = rosterEntries.Count;
                }
                catch (InvalidOperationException ex)
                {
                    MessageBox.Show($"列番号が設定されていません: {ex.Message}");
                    return;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Rosterの読み込み中にエラーが発生しました: {ex.Message}");
                    return;
                }

                // イベントをXMLファイルに保存
                SaveEventToXml(newEvent);

                // ObservableCollectionに追加してListViewを更新
                Events.Add(newEvent);
            }
        }


        private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            // Only filter when it is user input, not programmatic changes
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                var queryText = sender.Text;

                // Filter the events based on the input
                var filteredEvents = Events
                    .Where(ev => ev.EventName.IndexOf(queryText, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                // Update the DataGrid with the filtered events
                EventList.ItemsSource = filteredEvents;
            }
        }
        private async void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (EventList.SelectedItem != null)
            {
                dynamic selectedItem = EventList.SelectedItem;
                string name = selectedItem.EventName + ".xml";
                string filePath = System.IO.Path.Combine(dataFolder, name);

                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    ContentDialog deleteFileDialog = new ContentDialog
                    {
                        Title = "削除",
                        Content = "本当にイベントを削除しますか？",
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
                                Content = "イベントの削除に成功しました。",
                                CloseButtonText = "閉じる"
                            };

                            await deleteFileResultDialog.ShowAsync();
                            LoadEventsFromXml();
                            // DataGridにイベントを表示
                            EventList.ItemsSource = Events;
                        }
                        catch (Exception)
                        {
                            ContentDialog deleteFileErrorDialog = new ContentDialog
                            {
                                Title = "エラー",
                                Content = "イベントの削除に失敗しました。",
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
    public class Event
    {
        //イベント名＝xmlファイル名にしてるけど、イベント名もファイルに格納したほうがいい。今度やる。
        public string EventName { get; set; }
        public string EventDate { get; set; }
        public int Participants { get; set; }
        public string Status { get; set; }
        public string RosterName { get; set; }
        public string TouchSound { get; set; }
        public bool SameStudentSetting { get; set; }
        public List<RosterEntry> Roster { get; set; } = new List<RosterEntry>();
        public RosterInfo RosterInfo { get; set; }

    }
    [XmlType("Entry")]
    public class RosterEntry
    {
        // 各項目を明確にプロパティとして定義
        [XmlElement("RoomNumber")]
        public string RoomNumber { get; set; }

        [XmlElement("Gender")]
        public string Gender { get; set; }

        [XmlElement("Name")]
        public string Name { get; set; }

        [XmlElement("Kana")]
        public string Kana { get; set; }

        [XmlElement("StudentNumber")]
        public string StudentNumber { get; set; }

        [XmlElement("Department")]
        public string Department { get; set; }
        [XmlElement("Year")]
        public string Year { get; set; }

        // ステータスを保持 (初期値は "未参加")
        [XmlElement("Status")]
        public string Status { get; set; } = "未参加";

        // ファクトリメソッドで RosterInfo に基づいてインスタンスを作成
        public static RosterEntry FromCells(List<string> cells, RosterInfo info)
        {
            info.Validate(); // 列番号が設定されているか検証

            return new RosterEntry
            {
                RoomNumber = GetCellValue(cells, info.RoomNumberCol.Value),
                Gender = GetCellValue(cells, info.GenderCol.Value),
                Name = GetCellValue(cells, info.NameCol.Value),
                Kana = GetCellValue(cells, info.KanaCol.Value),
                StudentNumber = GetCellValue(cells, info.StudentNumberCol.Value),
                Department = GetCellValue(cells, info.DepartCol.Value),
                Year = GetCellValue(cells, info.YearCol.Value)
            };
        }

        // セル値を安全に取得するヘルパーメソッド
        private static string GetCellValue(List<string> cells, int columnIndex)
        {
            return columnIndex > 0 && columnIndex <= cells.Count ? cells[columnIndex - 1] : string.Empty;
        }
    }
    public class RosterInfo
    {
        public int? RoomNumberCol { get; set; }
        public int? GenderCol { get; set; }
        public int? NameCol { get; set; }
        public int? KanaCol { get; set; }
        public int? StudentNumberCol { get; set; }
        public int? DepartCol { get; set; }
        public int? YearCol { get; set; }

        // RosterInfo ノードから列情報をロード
        public static RosterInfo FromXml(XElement rosterInfoElement)
        {
            return new RosterInfo
            {
                RoomNumberCol = (int?)rosterInfoElement.Element("RoomNumberCol"),
                GenderCol = (int?)rosterInfoElement.Element("GenderCol"),
                NameCol = (int?)rosterInfoElement.Element("NameCol"),
                KanaCol = (int?)rosterInfoElement.Element("KanaCol"),
                StudentNumberCol = (int?)rosterInfoElement.Element("StudentNumberCol"),
                DepartCol = (int?)rosterInfoElement.Element("DepartCol"),
                YearCol = (int?)rosterInfoElement.Element("YearCol")
            };
        }
        public void Validate()
        {
            if (!RoomNumberCol.HasValue || !GenderCol.HasValue || !NameCol.HasValue ||
                !KanaCol.HasValue || !StudentNumberCol.HasValue || !DepartCol.HasValue || !YearCol.HasValue)
            {
                throw new InvalidOperationException("必要な列番号が設定されていません。RosterInfo を確認してください。");
            }
        }
    }



}
