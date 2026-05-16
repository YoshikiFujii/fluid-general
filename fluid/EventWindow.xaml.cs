using fluid_general.Pages;
using Microsoft.VisualBasic; // StrConv を使用するため
using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using WpfAnimatedGif;

namespace fluid_general
{
    /// <summary>
    /// Window1.xaml の相互作用ロジック
    /// </summary>

    public class RosterItem : INotifyPropertyChanged
    {
        public int ExcelId { get; set; }
        public string RoomNumber { get; set; }
        public string Name { get; set; }
        public string Kana { get; set; }
        public string StudentNumber { get; set; }
        public string Gender { get; set; }
        public string Department { get; set; }
        public string Year { get; set; }
        public List<string> DisplayValues { get; set; } = new List<string>();


        private bool isRegistered;
        public bool IsRegistered
        {
            get { return isRegistered; }
            set
            {
                isRegistered = value;
                OnPropertyChanged(nameof(IsRegistered));
            }
        }

        private bool isNotRegistered;
        public bool IsNotRegistered
        {
            get { return isNotRegistered; }
            set
            {
                isNotRegistered = value;
                OnPropertyChanged(nameof(IsNotRegistered));
            }
        }

        private bool isAbsent;
        public bool IsAbsent
        {
            get { return isAbsent; }
            set
            {
                isAbsent = value;
                OnPropertyChanged(nameof(IsAbsent));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    public class SettingItem
    {
        public string SoundSetting { get; set; }
        public bool SameStudentErrorSoundSetting { get; set; }
    }
    public partial class EventWindow : Window
    {
        public ObservableCollection<RosterItem> RosterItems { get; set; } = new ObservableCollection<RosterItem>();
        public ObservableCollection<string> CurrentDisplayColumns { get; set; } = new ObservableCollection<string>();
        private SettingItem Settings { get; set; } = new SettingItem();
        private string LogFolderPath = System.IO.Path.Combine(App.AppDataPath, "log");
        private string currentEvent;

        private int serialDataBits = 8; // デフォルト8ビット。必要に応じて変更可
        private string serialDelimiter = "\n"; // 区切り文字

        private int TotalParticipants;//progressnumberのそうす
        private int DoneParticipants;//progressnumberの値

        private bool isDialogOpen = false;

        private int ConnectTryCount = 0;
        private const string QueryMessage = "cntfluid";
        private const string ExpectedResponse = "hithere!";
        private const int TimeoutMilliseconds = 2000; // タイムアウト時間（ミリ秒）
        private BitmapImage image_waiting;
        private BitmapImage image_serching;
        private Dictionary<string, BitmapImage> preloadGifs = new Dictionary<string, BitmapImage>();
        private int RandomSoundCount = 0;
        private System.Media.SoundPlayer player = null;
        private System.Windows.Threading.DispatcherTimer _syncTimer;
        private bool _isSyncing = false;
        private int _syncFailureCount = 0;
        private const int MaxSyncFailures = 3;
        SerialPort connectedPort = null;
        private bool AbsentErrorSettings = true;//欠席登録者が認証したときの設定 true:拒否 false:認証
        private SoundPlayer Sound1 = null;
        private SoundPlayer Sound2 = null;
        private SoundPlayer Sound3 = null;
        private SoundPlayer Sound4 = null;
        private SoundPlayer Sound5 = null;

        private fluid_general.Models.EventConfig _currentEventConfig;

        public EventWindow(fluid_general.Models.EventConfig selectedEvent)
        {
            InitializeComponent();

            UpdateTitle();

            App.ConnectionModeChanged += OnConnectionModeChanged;

            _currentEventConfig = selectedEvent;
            currentEvent = selectedEvent.EventName;
            EventHeader.Text = currentEvent;
            EventInfoHeader.Text = selectedEvent.EventDate.ToString();
            DataContext = this;

            this.Loaded += async (s, e) => await LoadEventAsync();
            LoadGifAsync();
            if (!System.IO.Directory.Exists(LogFolderPath))
            {
                Directory.CreateDirectory(LogFolderPath);
            }

            // 同期タイマーの設定（3秒おきに最新の参加状況を取得）
            _syncTimer = new System.Windows.Threading.DispatcherTimer();
            _syncTimer.Interval = TimeSpan.FromSeconds(3);
            _syncTimer.Tick += async (s, e) => await SyncCheckInLogsAsync();
            _syncTimer.Start();
        }
        private async void LoadGifAsync()
        {
            await Task.Run(() =>
            {
                Dispatcher.Invoke(() =>
                {
                    var gifPaths = new Dictionary<string, string>
                    {
                        { "waiting", "Resources/waiting.gif" },
                        { "searching", "Resources/searching.gif" }
                    };
                    foreach (var gif in gifPaths)
                    {
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        string gifPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, gif.Value);
                        bitmap.UriSource = new Uri(gifPath, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze(); // スレッドセーフにするためにフリーズ
                        preloadGifs[gif.Key] = bitmap;
                    }
                });

            });
        }
        //音声ファイルの読み込み
        private void Window_ContentRendered(object sender, EventArgs e)
        {
            string sound1 = "j_1.wav";
            string sound2 = "j_2.wav";
            string sound3 = "j_3.wav";
            string sound4 = "Gate_BEEP.wav";
            string sound5 = "Gate_Alert.wav";

            string path1 = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Sound", sound1);
            string path2 = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Sound", sound2);
            string path3 = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Sound", sound3);
            string path4 = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Sound", sound4);
            string path5 = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Sound", sound5);

            Sound1 = new SoundPlayer(path1);
            Sound2 = new SoundPlayer(path2);
            Sound3 = new SoundPlayer(path3);
            Sound4 = new SoundPlayer(path4);
            Sound5 = new SoundPlayer(path5);
        }
        public async void AddTerminal(object sender, RoutedEventArgs e)
        {
            if (connectedPort != null && connectedPort.IsOpen)
            {
                UnconnectTerminal();
                return;
            }
            AddTerminalButton.IsEnabled = false; // ボタンを無効にする
            await GetCOMPort();     // 非同期でCOMポートの検索を行う
            AddTerminalButton.IsEnabled = true;  // 処理が終わったらボタンを再び有効にする
            if (connectedPort != null)
            {
                // 接続が完了したらデータを受信し続ける
                await ReceiveDataContinuously(connectedPort);
            }
        }
        public async void UnconnectTerminal()
        {
            if (isDialogOpen) return;
            if (connectedPort != null && connectedPort.IsOpen)
            {
                connectedPort.Write("111111111"); // 端末に終了コマンドを送信
                connectedPort.Close();
                connectedPort.Dispose();
                connectedPort = null;
                ShutdownButton.Visibility = Visibility.Collapsed;
                AddTerminalButtonIcon.Glyph = "\uE710";
                AddTerminalButton.ToolTip = "認証端末を接続する";
                StatusAnimation.Visibility = Visibility.Collapsed;
                SubStatusText.Text = "";
                CertificationLabel.Content = "";
                CertificationLabel2.Content = "";
                CertificationRectangle.Fill = new SolidColorBrush(Color.FromRgb(222, 222, 222));

                isDialogOpen = true;
                ContentDialog DisconnectDialog = new ContentDialog
                {
                    Title = "接続解除",
                    Content = "認証端末を接続解除しました。再接続するには接続ボタンを押してください。",
                    CloseButtonText = "閉じる"
                };

                await DisconnectDialog.ShowAsync();
            }
            else 
            {
                if (connectedPort != null && connectedPort.IsOpen)
                {
                    connectedPort.Close();
                    connectedPort.Dispose();
                    connectedPort = null;
                }
                ShutdownButton.Visibility = Visibility.Collapsed;
                AddTerminalButtonIcon.Glyph = "\uE710";
                AddTerminalButton.ToolTip = "認証端末を接続する";
                StatusAnimation.Visibility = Visibility.Collapsed;
                SubStatusText.Text = "";
                CertificationLabel.Content = "";
                CertificationLabel2.Content = "";
                CertificationRectangle.Fill = new SolidColorBrush(Color.FromRgb(222, 222, 222));

                isDialogOpen = true;
                ContentDialog ForceDisconnectDialog = new ContentDialog
                {
                    Title = "接続解除",
                    Content = "認証端末からの接続が切れました。再接続するには接続ボタンを押してください。",
                    CloseButtonText = "閉じる"
                };

                await ForceDisconnectDialog.ShowAsync();
            }
            isDialogOpen = false;
        }
        public async void ShutdownTerminal(object sender, RoutedEventArgs e)
        {
            if (connectedPort != null && connectedPort.IsOpen)
            {
                connectedPort.Write("222222222"); //shutdown signal
                connectedPort.Close();
                connectedPort.Dispose();
                connectedPort = null;
                ShutdownButton.Visibility = Visibility.Collapsed;
                AddTerminalButtonIcon.Glyph = "\uE710";
                AddTerminalButton.ToolTip = "認証端末を接続する";
                StatusAnimation.Visibility = Visibility.Collapsed;
                SubStatusText.Text = "";
                CertificationLabel.Content = "";
                CertificationLabel2.Content = "";
                CertificationRectangle.Fill = new SolidColorBrush(Color.FromRgb(222, 222, 222));
            }
            ContentDialog ResultDialog = new ContentDialog
            {
                Title = "シャットダウン",
                Content = "認証端末をシャットダウンしました。再接続する場合はUSBを刺しなおしてください。",
                CloseButtonText = "閉じる"
            };

            await ResultDialog.ShowAsync();
        }

        public async Task GetCOMPort()
        {
            ImageBehavior.SetAnimatedSource(StatusAnimation, null); // GIFをリセット
            await Task.Delay(100); // 短い遅延を入れて完全にリセット
            // アニメーションの設定
            Dispatcher.Invoke(() =>
            {
                if (preloadGifs.TryGetValue("searching", out var searchingGif))
                {
                    ImageBehavior.SetAnimatedSource(StatusAnimation, searchingGif);
                    StatusAnimation.Visibility = Visibility.Visible;
                }
            });
            MessageLabel.Content = "";
            SubStatusText.Text = "端末を検索中　USBタイプの認証端末を接続してください";

            int retire = 0;

            await Task.Run(async () =>
            {
                while (retire < 5)
                {
                    string[] ports = SerialPort.GetPortNames();
                    foreach (string portName in ports)
                    {
                        try
                        {
                            // SerialPort オブジェクトを作成
                            connectedPort = new SerialPort(portName)
                            {
                                // ポートの設定を行う（ボーレート、パリティ、データビットなど）
                                BaudRate = 115200,
                                Parity = Parity.None,
                                DataBits = serialDataBits,
                                StopBits = StopBits.One,
                                Handshake = Handshake.None,
                                ReadTimeout = TimeoutMilliseconds,
                                WriteTimeout = TimeoutMilliseconds,
                                NewLine = serialDelimiter // 区切り文字を設定
                            };
                            await Task.Delay(500);
                            // ポートをオープン
                            connectedPort.Open();

                            try
                            {
                                // メッセージを書き込む
                                string message = QueryMessage.TrimEnd('\n', '\r') + serialDelimiter;
                                connectedPort.DiscardOutBuffer();
                                connectedPort.Write(message);

                                string response = "";
                                int attempts = 0;
                                int maxAttempts = 10; // 最大試行回数を設定（例: 10回）

                                // データが来るまで待機しながらループ
                                while (attempts < maxAttempts)
                                {
                                    try
                                    {
                                        response = connectedPort.ReadLine(); // 区切り文字まで受信
                                        break;
                                    }
                                    catch (TimeoutException)
                                    {
                                        await Task.Delay(100);
                                        attempts++;
                                    }
                                }

                                if (response.EndsWith(ExpectedResponse.TrimEnd('\n', '\r')))
                                {
                                    retire = 5;

                                    // 成功時のアニメーション変更
                                    Dispatcher.Invoke(() =>
                                    {
                                        SubStatusText.Text = "端末接続完了";
                                        StatusAnimation.Visibility = Visibility.Visible;
                                        AddTerminalButtonIcon.Glyph = "\uE711";
                                        AddTerminalButton.ToolTip = "接続解除";
                                        ShutdownButton.Visibility = Visibility.Visible;
                                        Dispatcher.Invoke(() =>
                                        {
                                            if (preloadGifs.TryGetValue("waiting", out var waitingGif))
                                            {
                                                ImageBehavior.SetAnimatedSource(StatusAnimation, waitingGif);
                                            }
                                        });

                                    });

                                    return;
                                }
                                else
                                {
                                    Console.WriteLine("レスポンスが一致しません。");
                                }
                            }
                            catch (TimeoutException)
                            {
                                // タイムアウトが発生した場合は次のポートへ進む
                                Console.WriteLine($"ポート {portName} でタイムアウトが発生しました。次のポートに進みます。");
                            }

                        }
                        catch (Exception ex)
                        {
                            // 例外をキャッチしてログに出力
                            Console.WriteLine($"ポート {portName} でエラーが発生: {ex.Message}");
                            await Task.Delay(500);
                        }
                    }
                    retire++;
                }

                // 全てのポートを試した後、接続が失敗した場合の処理
                Dispatcher.Invoke(() =>
                {
                    FadeOutElement(StatusAnimation, 1.0); // 1秒でフェードアウト
                    MessageLabel.Content = "端末が見つかりませんでした。"; // メッセージを表示
                    SubStatusText.Text = "端末接続後起動に３０秒程度かかります。少し待ってから再度検索してください。";
                    ConnectTryCount++;
                    if (Properties.Settings.Default.IsFirstTimeConnectError == true & ConnectTryCount == 2)
                    {
                        MessageBox.Show("ドライバのインストールが完了していない可能性があります。端末を接続した状態でWindowsアップデートを実行してみてください。");
                        Properties.Settings.Default.IsFirstTimeConnectError = false;
                        Properties.Settings.Default.Save();
                    }
                });
            });
        }
        //#####################################サウンド#########################################
        //-------------タッチサウンド--------------------
        private void TouchSound()
        {
            int count = RandomSoundCount % 5;

            if (Settings.SoundSetting == "JR")
            {
                PlaySound(Sound4);
            }

            if (Settings.SoundSetting == "JUGGLER")
            {
                if (count == 0)
                {
                    PlaySound(Sound1);
                    RandomSoundCount++;
                }
                else if (count == 1 || count == 2 || count == 3)
                {
                    PlaySound(Sound2);
                    RandomSoundCount++;
                }
                else
                {   // Randomオブジェクトの作成
                    Random random = new Random();
                    int num = random.Next(1, 5);
                    Console.WriteLine($"RandomCount is {num}");
                    if (num == 1)
                    {
                        PlaySound(Sound3);
                        RandomSoundCount = 0;
                    }
                    else
                    {
                        PlaySound(Sound1);
                        RandomSoundCount = 1;
                    }
                }
            }

        }
        //---------------エラーサウンド---------------------
        private void ErrorSound()
        {
            PlaySound(Sound5);
        }
        //--------２回以上認証したときのサウンド------
        private void SameStudentErrorSound()
        {
            if (Settings.SameStudentErrorSoundSetting == true)
            {
                PlaySound(Sound5);
            }
            if (Settings.SameStudentErrorSoundSetting == false)
            {
                PlaySound(Sound4);
            }
        }
        private void AbsentErrorSound()
        {
            if (AbsentErrorSettings == true)
            {
                PlaySound(Sound5);
            }
            if (AbsentErrorSettings == false)
            {
                PlaySound(Sound4);
            }
        }
        private void StopSound()
        {
            if (player != null)
            {
                player.Stop();
                player.Dispose();
                player = null;
            }
        }
        private void PlaySound(SoundPlayer Sound)
        {
            try
            {
                Sound.Play();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"音声の再生に失敗しました: {ex.Message}");
            }
        }


        public async Task ReceiveDataContinuously(SerialPort serialPort)
        {
            try
            {
                while (serialPort.IsOpen)
                {
                    try
                    {
                        if (connectedPort.BytesToRead > 0)
                        {
                            int availableBytes = connectedPort.BytesToRead;
                            byte[] buffer = new byte[availableBytes];

                            // シリアルポートからデータを読み取る
                            int bytesRead = serialPort.Read(buffer, 0, buffer.Length);

                            // 読み取ったバイト列を文字列にデコード
                            string result = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            Console.WriteLine("Received data: " + result);

                            if (result == "000000000")
                            {
                                CertificationLabel.Content = "認証エラー：学生証以外が認識されました";
                                CertificationLabel2.Content = "再度試してください";
                                Console.WriteLine("学生証以外の検出");
                                CertificationBackChange(255, 80, 80);
                                ErrorSound();
                            }
                            else if (result == "E0001")
                            {
                                ContentDialog ScannerErrorDialog = new ContentDialog
                                {
                                    Title = "スキャナーエラー",
                                    Content = "認証端末に接続されているNFCスキャナが正常に接続されていません。スキャナを接続しなおしてください。",
                                    CloseButtonText = "閉じる"
                                };

                                await ScannerErrorDialog.ShowAsync();
                            }
                            else
                            {
                                AuthenticateByNFC(result);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"タッチ処理中にエラーが発生しました: {ex.Message}");
                    }

                    // 適宜、短い待機を挟んで無駄なCPU使用率を防ぐ
                    await Task.Delay(100);
                }
                Console.WriteLine("シリアルポートが閉じられました。");
                UnconnectTerminal();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"データ受信中にエラーが発生しました: {ex.Message}");
            }
        }
        private void AuthenticateByNFC(string studentNumber)
        {
            // 末尾2桁を削除
            if (studentNumber.Length > 2)
            {
                studentNumber = studentNumber.Substring(0, studentNumber.Length - 2);
            }
            // 受け取った学籍番号またはエクセルIDに一致する生徒をRosterItemsから検索
            var student = RosterItems.FirstOrDefault(item => 
                item.StudentNumber == studentNumber || 
                item.ExcelId.ToString() == studentNumber);

            if (student != null)
            {
                // すでに参加済みでない場合のみステータスを更新
                if (student.IsNotRegistered)
                {
                    student.IsRegistered = true;
                    student.IsNotRegistered = false;
                    student.IsAbsent = false;

                    // 状態が変更されたので、保存する
                    SaveStatus(student);
                    CertificationLabel.Content = $"{student.Name} さんが参加しました";
                    CertificationLabel2.Content = $"部屋番号:{student.RoomNumber} | 区分:{student.Year} | 学科:{student.Department}";
                    Console.WriteLine($"Student {studentNumber} marked as registered.");
                    TouchSound();
                    WriteLog("参加済み", student);
                    CertificationBackChange(151, 209, 255);
                    UpdateProgressBar(student, "参加済み");
                }
                else if (student.IsAbsent)
                {
                    AbsentErrorSound();
                    CertificationLabel.Content = $"{student.Name} さんは欠席登録者です";
                    CertificationLabel2.Content = $"部屋番号:{student.RoomNumber} | 区分:{student.Year} | 学科:{student.Department}";
                    Console.WriteLine($"Student {studentNumber} is Absent registered.");
                    CertificationBackChange(255, 255, 125);

                    if (AbsentErrorSettings == false)
                    {
                        student.IsRegistered = true;
                        student.IsNotRegistered = false;
                        student.IsAbsent = false;
                        UpdateProgressBar(student, "参加済み");
                    }
                }
                else
                {
                    CertificationLabel.Content = $"{student.Name} さんは参加済みです";
                    CertificationLabel2.Content = $"部屋番号:{student.RoomNumber} | 区分:{student.Year} | 学科:{student.Department}";
                    Console.WriteLine($"Student {studentNumber} is already registered.");
                    SameStudentErrorSound();
                    CertificationBackChange(255, 255, 125);
                }
            }
            else
            {
                // 該当する生徒が見つからない場合の処理
                CertificationLabel.Content = $"ERROR:{studentNumber} は名簿にありません";
                Console.WriteLine($"Student {studentNumber} not found in the roster.");
                CertificationBackChange(255, 80, 80);
            }
        }
        private async void CertificationBackChange(int goalR, int goalG, int goalB)
        {
            int defaultR = 222, defaultG = 222, defaultB = 222;
            double r, g, b;
            double R = goalR, G = goalG, B = goalB;

            SolidColorBrush brush = new SolidColorBrush(Color.FromRgb((byte)R, (byte)G, (byte)B));
            CertificationRectangle.Fill = brush;
            brush.Color = Color.FromRgb((byte)goalR, (byte)goalG, (byte)goalB);

            // 色の変化量を計算
            r = (defaultR - goalR) / 30.00;
            g = (defaultG - goalG) / 30.00;
            b = (defaultB - goalB) / 30.00;

            // 10ステップで色を変更
            for (int i = 0; i < 30; i++)
            {
                R += r;
                G += g;
                B += b;

                // 色を更新
                brush.Color = Color.FromRgb((byte)R, (byte)G, (byte)B);

                // 少し待ってから次のステップへ
                await Task.Delay(10);  // 100ミリ秒待つ
            }
        }

        private async Task LoadEventAsync()
        {
            try
            {
                var service = App.GetDataService();
                var ev = await service.GetEventAsync(_currentEventConfig.Id);
                if (ev == null)
                {
                    MessageBox.Show($"イベントが見つかりません。 (ID: {_currentEventConfig.Id})");
                    return;
                }

                Settings.SoundSetting = ev.TouchSound;
                Settings.SameStudentErrorSoundSetting = ev.SameStudentSetting;

                await RefreshRosterListAsync();
                UpdateProgressBar();
                
                RosterListView.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"イベントの読み込み中にエラーが発生しました:\n{ex.Message}");
                App.LogError(ex);
            }
        }

        public void UpdateProgressBar()
        {
            TotalParticipants = RosterItems.Count;
            DoneParticipants = RosterItems.Count(r => r.IsRegistered);

            WholeStatusText.Text = $"{DoneParticipants}人 / {TotalParticipants}人";
        }

        // ステータス詳細ボタンのハンドラーを削除
        // 更新ボタンのハンドラーを削除

        public async void aboutButtonClick(object sender, RoutedEventArgs e)
        {
            var dialog = new aboutDialog();
            var result = await dialog.ShowAsync();
        }

        public async void SettingsButtonClick(object sender, RoutedEventArgs e)
        {
            var dialog = new EventSettingsDialog(null, Settings);
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                Settings.SoundSetting = dialog.SelectedSoundSetting;
                Settings.SameStudentErrorSoundSetting = dialog.SameStudentErrorEnabled;
                SaveSettings(Settings);
            }
        }
        public void WriteLog(string status, RosterItem rosterItem)
        {

            string logFile = $"{currentEvent}.txt"; // ログファイルのパス
            string logFilePath = System.IO.Path.Combine(LogFolderPath, logFile);
            string currentTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"); // 現在の時間
            string logEntry = $"{currentTime}, {rosterItem.RoomNumber}, {rosterItem.Name}, {status}\n";

            // ログファイルに追記
            LogList.AppendText($"[{currentTime}]    {status}    {rosterItem.RoomNumber}     {rosterItem.Name}  \r\n");
            LogList.ScrollToEnd();
            File.AppendAllText(logFilePath, logEntry);
        }

        // ラジオボタンが選択されたときの処理
        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            var radioButton = sender as RadioButton;
            var selectedRosterItem = radioButton.DataContext as RosterItem;

            if (selectedRosterItem == null) return;

            string NewStatus = "";

            // 状態に応じてステータスを更新
            if (radioButton.Content.ToString() == "参加済み")
            {
                selectedRosterItem.IsRegistered = true;
                selectedRosterItem.IsNotRegistered = false;
                selectedRosterItem.IsAbsent = false;
                NewStatus = "参加済み";
            }
            else if (radioButton.Content.ToString() == "未参加")
            {
                selectedRosterItem.IsRegistered = false;
                selectedRosterItem.IsNotRegistered = true;
                selectedRosterItem.IsAbsent = false;
                NewStatus = "未参加";
            }
            else if (radioButton.Content.ToString() == "不参加")
            {
                selectedRosterItem.IsRegistered = false;
                selectedRosterItem.IsNotRegistered = false;
                selectedRosterItem.IsAbsent = true;
                NewStatus = "不参加";
            }

            // 更新されたステータスを進捗バーに反映
            UpdateProgressBar(selectedRosterItem, NewStatus);

            // ログに書き込む
            WriteLog(NewStatus, selectedRosterItem);

            // 変更を保存するロジックをここに追加
            SaveStatus(selectedRosterItem);
        }
        private void UpdateProgressBar(RosterItem student, string currentStatus)
        {
            UpdateProgressBar();
        }
        // 選択された状態を保存する
        //設定をイベントファイルに保存する
        private async void SaveSettings(SettingItem settings)
        {
            var service = App.GetDataService();
            var ev = await service.GetEventAsync(_currentEventConfig.Id);
            if (ev != null)
            {
                ev.TouchSound = settings.SoundSetting;
                ev.SameStudentSetting = settings.SameStudentErrorSoundSetting;
                await service.UpdateEventAsync(ev);
            }
        }
        
        private async void SaveStatus(RosterItem rosterItem)
        {
            try
            {
                var service = App.GetDataService();
                string status = "未参加";
                if (rosterItem.IsRegistered) status = "参加済み";
                else if (rosterItem.IsAbsent) status = "不参加";

                await service.UpdateCheckInStatusAsync(_currentEventConfig.RosterName, rosterItem.ExcelId, _currentEventConfig.Id, status);
            }
            catch (Exception ex)
            {
                App.LogError(ex);
                // 通信エラー（HttpRequestException等）の場合は、Appのウォッチドッグが切断処理を行うため
                // ここではユーザーにメッセージボックスを出さないようにする。
                if (!(ex is System.Net.Http.HttpRequestException || ex is TaskCanceledException))
                {
                    MessageBox.Show($"名簿の取得に失敗しました: {ex.Message}");
                }
            }
        }
        // テキストボックスでの入力に基づいてリストをフィルタリング
        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            FilterRosterList();
        }
        private void FilterList(object sender, RoutedEventArgs e)
        {
            // 選択されているフィルター条件を取得
            bool showRegistered = Show_Registerd.IsChecked == true;
            bool showNotRegistered = Show_NotRegisterd.IsChecked == true;
            bool showAbsent = Show_Absent.IsChecked == true;

            // 絞り込んだ結果を作成
            var filteredItems = RosterItems.Where(item =>
                (showRegistered && item.IsRegistered) ||
                (showNotRegistered && item.IsNotRegistered) ||
                (showAbsent && item.IsAbsent)).ToList();

            // 絞り込んだリストを `ListView` に適用
            RosterListView.ItemsSource = filteredItems;

            // 結果が空の場合は非表示
            RosterListView.Visibility = filteredItems.Any() ? Visibility.Visible : Visibility.Collapsed;
        }


        private void FilterRosterList()
        {
            string query = GeneralSearchBox.Text.ToLower();

            if (string.IsNullOrWhiteSpace(query))
            {
                RosterListView.ItemsSource = RosterItems;
                return;
            }

            var filteredList = RosterItems.Where(item =>
                (item.RoomNumber?.ToLower().Contains(query) == true) ||
                (item.Name?.ToLower().Contains(query) == true) ||
                (item.Kana?.ToLower().Contains(query) == true) ||
                (ConvertToHiragana(item.Kana?.ToLower() ?? "").Contains(query)) ||
                (item.StudentNumber?.ToLower().Contains(query) == true) ||
                (item.Department?.ToLower().Contains(query) == true) ||
                (item.Year?.ToLower().Contains(query) == true) ||
                (item.DisplayValues.Any(v => v?.ToLower().Contains(query) == true))
            ).ToList();

            RosterListView.ItemsSource = filteredList;
        }
        //半角カナを全角かなに変換する関数-----------------------------------------------------------
        static string ConvertToHiragana(string input)
        {
            // (1) 半角カタカナを全角カタカナに変換
            string fullWidthKatakana = Strings.StrConv(input, VbStrConv.Wide, 0x0411);

            // (2) 全角カタカナをひらがなに変換
            return KatakanaToHiragana(fullWidthKatakana);
        }

        static string KatakanaToHiragana(string input)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in input)
            {
                if (c >= 0x30A0 && c <= 0x30FF) // カタカナ範囲
                {
                    sb.Append((char)(c - 0x60)); // ひらがなへ変換
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
        //--------------------------------------------------------------------------------------------

        // フェードアウトの処理を関数化
        private void FadeOutElement(UIElement element, double durationInSeconds)
        {
            var fadeOutAnimation = new DoubleAnimation
            {
                From = 1.0, // 現在の不透明度（完全表示）
                To = 0.0, // 最終的な不透明度（完全に非表示）
                Duration = TimeSpan.FromSeconds(durationInSeconds), // フェードアウトにかかる時間
                FillBehavior = FillBehavior.Stop // アニメーション終了後に状態を保持しない
            };

            fadeOutAnimation.Completed += (s, e) =>
            {
                // フェードアウト完了後に要素を非表示
                element.Visibility = Visibility.Hidden;
            };

            // アニメーションを開始
            element.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
        }
        private void CloseWindowButtonClick(object sender, RoutedEventArgs e)
        {
            ClosingProcess();
        }
        private void ClosingProcess()
        {
            try
            {
                if (connectedPort != null && connectedPort.IsOpen)
                {
                    connectedPort.Write("11111111"); // 端末に終了コマンドを送信
                    connectedPort.Close(); // ポートを閉じる
                }
            }
            catch (Exception ex)
            {
                // エラーが発生した場合、ログを表示または処理
                MessageBox.Show($"エラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (Application.Current.MainWindow != null)
                {
                    // メインウィンドウが非表示の場合は再表示
                    Application.Current.MainWindow.Show();
                }
                else
                {
                    // メインウィンドウが存在しない場合は新規作成
                    MainWindow mainWindow = new MainWindow();
                    Application.Current.MainWindow = mainWindow;
                    mainWindow.Show();

                }
                this.Close();
            }
        }
        void EventWindow_Closing(object sender, CancelEventArgs e)
        {
            App.ConnectionModeChanged -= OnConnectionModeChanged;
            _syncTimer?.Stop();
            try
            {
                if (connectedPort != null && connectedPort.IsOpen)
                {
                    connectedPort.Write("11111111"); // 端末に終了コマンドを送信
                    connectedPort.Close(); // ポートを閉じる
                }
            }
            catch (Exception ex)
            {
                // エラーが発生した場合、ログを表示または処理
                MessageBox.Show($"エラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (Application.Current.MainWindow != null)
                {
                    // メインウィンドウが非表示の場合は再表示
                    Application.Current.MainWindow.Show();
                }
                else
                {
                    // メインウィンドウが存在しない場合は新規作成
                    MainWindow mainWindow = new MainWindow();
                    Application.Current.MainWindow = mainWindow;
                    mainWindow.Show();
                }
            }
        }
        private void ClearLog(object sender, RoutedEventArgs e)
        {
            LogList.Text = "";
        }
        private async void DeleteLogFile(object sender, RoutedEventArgs e)
        {
            ContentDialog deleteLogDialog = new ContentDialog
            {
                Title = "ログの削除",
                Content = "本当にログを削除しますか？この操作は取り消せません。",
                PrimaryButtonText = "削除",
                CloseButtonText = "キャンセル"
            };

            // ユーザーの選択を待つ
            ContentDialogResult result = await deleteLogDialog.ShowAsync();

            // ユーザーが「削除」を選択した場合にのみログをクリア
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    // ログファイルのパスを取得
                    string logFile = $"{currentEvent}.txt"; // ログファイルの名前
                    string logFilePath = System.IO.Path.Combine(LogFolderPath, logFile);

                    // ファイルが存在する場合、内容をクリア
                    if (File.Exists(logFilePath))
                    {
                        File.WriteAllText(logFilePath, string.Empty); // ファイルの中身を空にする
                    }
                    else
                    {
                        MessageBox.Show($"ログファイルが存在しません: {logFilePath}", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    // エラーが発生した場合、ユーザーに通知
                    MessageBox.Show($"ログのクリア中にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void OpenLogFile(object sender, RoutedEventArgs e)
        {
            try
            {
                string logFile = $"{currentEvent}.txt";
                string logFilePath = System.IO.Path.Combine(LogFolderPath, logFile);

                if (File.Exists(logFilePath))
                {
                    System.Diagnostics.Process.Start("notepad.exe", logFilePath);
                }
                else
                {
                    MessageBox.Show($"ログファイルが存在しません: {logFilePath}", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ログファイルの展開時にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void OpenLogFolder(object sender, RoutedEventArgs e)
        {
            try
            {
                // ログフォルダのパスを取得
                string logFolderPath = LogFolderPath;

                // フォルダが存在するか確認
                if (Directory.Exists(logFolderPath))
                {
                    // エクスプローラーでログフォルダを開く
                    System.Diagnostics.Process.Start("explorer.exe", logFolderPath);
                }
                else
                {
                    // フォルダが存在しない場合はエラーメッセージを表示
                    MessageBox.Show($"ログフォルダが存在しません: {logFolderPath}", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                // 例外が発生した場合、エラーメッセージを表示
                MessageBox.Show($"ログフォルダを開く際にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async void ImportButtonClick(object sender, RoutedEventArgs e)
        {
            var dialog = new ImportDialog(currentEvent);
            var result = await dialog.ShowAsync();
            await LoadEventAsync();
        }
        private async void ExportButtonClick(object sender, RoutedEventArgs e)
        {
            var dialog = new ExportDialog(_currentEventConfig);
            var result = await dialog.ShowAsync();
        }
        private async Task SyncCheckInLogsAsync()
        {
            if (_isSyncing) return;
            _isSyncing = true;

            try
            {
                var service = App.GetDataService();
                var logs = await service.GetCheckInLogsAsync(_currentEventConfig.Id);

                // 取得したログを元にRosterItemsの状態を更新
                foreach (var log in logs)
                {
                    var item = RosterItems.FirstOrDefault(r => r.ExcelId == log.ExcelId);
                    if (item != null)
                    {
                        bool isRegistered = log.Status == "参加済み";
                        bool isAbsent = log.Status == "不参加";
                        bool isNotRegistered = log.Status == "未参加";

                        // 変更がある場合のみプロパティを更新（UI通知を最小限にするため）
                        if (item.IsRegistered != isRegistered || item.IsAbsent != isAbsent || item.IsNotRegistered != isNotRegistered)
                        {
                            item.IsRegistered = isRegistered;
                            item.IsAbsent = isAbsent;
                            item.IsNotRegistered = isNotRegistered;
                        }
                    }
                }
                UpdateProgressBar();

                // 接続端末数の更新
                UpdateConnectionStatus();
            }
            catch
            {
                // 通信エラーは App クラスのウォッチドッグが処理するため、ここでは何もしない
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private void OnConnectionModeChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (string.IsNullOrEmpty(App.ServerBaseUrl))
                {
                    // 子機モードから切断（親機モードに移行）した場合は、
                    // データの整合性と使い勝手を考慮してイベント画面を閉じてメインに戻る
                    this.Close();
                }
                else
                {
                    UpdateTitle();
                    _ = RefreshRosterListAsync();
                }
            });
        }

        private void UpdateTitle()
        {
            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string baseTitle = $"fluid-general - {version} - EventWindow";

            if (string.IsNullOrEmpty(App.ServerBaseUrl))
            {
                string localIp = Utils.NetworkUtils.GetLocalIPAddress();
                int connectionCount = App.GetActiveConnectionCount();
                string connectionText = connectionCount > 0 ? $" (接続数: {connectionCount})" : "";
                this.Title = string.IsNullOrEmpty(localIp) ? $"{baseTitle} - 親機モード{connectionText}" : $"{baseTitle} - 親機モード (IP: {localIp}){connectionText}";
                
                // 親機モードは薄い赤色にする
                var parentBrush = new SolidColorBrush(Color.FromRgb(255, 235, 235));
                ModernWpf.Controls.TitleBar.SetBackground(this, parentBrush);
            }
            else
            {
                this.Title = $"{baseTitle} - 子機モード (接続先: {App.ServerBaseUrl})";
                
                // 子機モードは薄い青色
                var childBrush = new SolidColorBrush(Color.FromRgb(230, 242, 255));
                ModernWpf.Controls.TitleBar.SetBackground(this, childBrush);
            }

            // タイトルバー以外（メニューバー右端）のUIも更新
            UpdateConnectionStatus();
        }

        private void UpdateConnectionStatus()
        {
            if (string.IsNullOrEmpty(App.ServerBaseUrl))
            {
                // 親機モードの場合のみ接続数を表示
                int count = App.GetActiveConnectionCount();
                ConnectionCountTextBlock.Text = count.ToString();
                ConnectionStatusButton.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;

                if (count > 0)
                {
                    ActiveTerminalsListBox.ItemsSource = App.GetActiveTerminalList();
                }
            }
            else
            {
                ConnectionStatusButton.Visibility = Visibility.Collapsed;
            }
        }

        public async Task RefreshRosterListAsync()
        {
            try
            {
                RosterErrorMessageTextBlock.Visibility = Visibility.Collapsed;
                var service = App.GetDataService();
                var members = await service.GetMembersByRosterAsync(_currentEventConfig.RosterName);
                var logs = await service.GetCheckInLogsAsync(_currentEventConfig.Id);
 
                RosterItems.Clear();
                if (members.Count == 0)
                {
                    LogList.AppendText($"[{DateTime.Now:HH:mm:ss}] [警告] 名簿 '{_currentEventConfig.RosterName}' にメンバーが登録されていません。\n");
                }
 
                var rosterConfig = await service.GetRosterConfigAsync(_currentEventConfig.RosterName);
                var displayColumns = (rosterConfig?.DisplayColumns != null && rosterConfig.DisplayColumns.Count > 0) 
                    ? rosterConfig.DisplayColumns 
                    : new List<string> { "名前", "かな", "学籍番号" };

                CurrentDisplayColumns.Clear();
                foreach (var col in displayColumns) CurrentDisplayColumns.Add(col);

                foreach (var member in members)
                {
                    var log = logs.FirstOrDefault(l => l.RosterName == member.RosterName && l.ExcelId == member.ExcelId);
                    string status = log?.Status ?? "未参加";
 
                    var item = new RosterItem
                    {
                        ExcelId = member.ExcelId,
                        RoomNumber = member.CustomFields.GetValueOrDefault("RoomNumber", ""),
                        Name = member.Name,
                        Kana = member.Kana,
                        StudentNumber = member.StudentNumber,
                        Gender = member.CustomFields.GetValueOrDefault("Gender", ""),
                        Department = member.CustomFields.GetValueOrDefault("Department", ""),
                        Year = member.CustomFields.GetValueOrDefault("Year", ""),
                        IsRegistered = status == "参加済み",
                        IsNotRegistered = status == "未参加",
                        IsAbsent = status == "不参加"
                    };

                    foreach (var col in displayColumns)
                    {
                        string val = col switch
                        {
                            "名前" => member.Name,
                            "かな" => member.Kana,
                            "学籍番号" => member.StudentNumber,
                            _ => member.CustomFields.GetValueOrDefault(col, "")
                        };
                        item.DisplayValues.Add(val);
                    }

                    RosterItems.Add(item);
                }
                RosterListView.ItemsSource = null;
                RosterListView.ItemsSource = RosterItems;
                
                // 検索などのために ListView を表示
                if (RosterItems.Count > 0)
                {
                    RosterListView.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                App.LogError(ex);
                // 通信エラー（HttpRequestException等）の場合は、Appのウォッチドッグが切断処理を行うため
                // ここではユーザーにメッセージボックスを出さないようにする。
                // 代わりに名簿エリアにエラーメッセージを表示する。
                RosterListView.Visibility = Visibility.Collapsed;
                RosterErrorMessageTextBlock.Text = $"名簿の読み込みに失敗しました: {ex.Message}";
                RosterErrorMessageTextBlock.Visibility = Visibility.Visible;
                
                if (!(ex is System.Net.Http.HttpRequestException || ex is System.Threading.Tasks.TaskCanceledException))
                {
                    // 致命的なエラー（ネットワーク以外）の場合はログ出力なども検討
                }
            }
        }

        private async void DisplaySettingsClick(object sender, RoutedEventArgs e)
        {
            var service = App.GetDataService();
            var members = await service.GetMembersByRosterAsync(_currentEventConfig.RosterName);
            
            // 全てのメンバーからユニークなカスタムフィールドキーを抽出
            var allKeys = new List<string> { "名前", "かな", "学籍番号" };
            allKeys.AddRange(members.SelectMany(m => m.CustomFields.Keys).Distinct());
            allKeys = allKeys.Distinct().ToList();
            
            var rosterConfig = await service.GetRosterConfigAsync(_currentEventConfig.RosterName);
            if (rosterConfig == null)
            {
                rosterConfig = new Models.RosterConfig { RosterName = _currentEventConfig.RosterName };
                // デフォルトの表示順を設定
                rosterConfig.DisplayColumns = new List<string> { "名前", "かな", "学籍番号" };
            }

            var dialog = new DisplaySettingsDialog(allKeys, rosterConfig.DisplayColumns);
            var result = await dialog.ShowAsync();
            
            if (result == ContentDialogResult.Primary)
            {
                rosterConfig.DisplayColumns = dialog.SelectedColumns;
                await service.UpdateRosterConfigAsync(rosterConfig);
                await RefreshRosterListAsync();
            }
        }
    }
}