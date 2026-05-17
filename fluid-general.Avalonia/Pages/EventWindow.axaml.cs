using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Media;
using AnimatedImage.Avalonia;
using fluid_general.Models;
using fluid_general.Services;
using fluid_general.Utils;
using NetCoreAudio;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace fluid_general.Avalonia.Pages;

public class MemberVM : INotifyPropertyChanged
{
    private string _status = "未参加";
    public Member Member { get; set; } = null!;
    
    public string Status 
    { 
        get => _status; 
        set 
        { 
            if (_status == value) return;
            _status = value; 
            Notify(nameof(Status));
            Notify(nameof(IsRegistered));
            Notify(nameof(IsNotRegistered));
            Notify(nameof(IsAbsent));
        } 
    }

    public bool IsRegistered 
    { 
        get => Status == "参加済み"; 
        set { if (value) Status = "参加済み"; } 
    }
    public bool IsNotRegistered 
    { 
        get => Status == "未参加"; 
        set { if (value) Status = "未参加"; } 
    }
    public bool IsAbsent 
    { 
        get => Status == "不参加"; 
        set { if (value) Status = "不参加"; } 
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public void Notify(string prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}

public partial class EventWindow : Window
{
    private readonly EventConfig _event;
    private readonly ObservableCollection<MemberVM> _members = new();
    private readonly ObservableCollection<string> _logs = new();
    private List<string> _displayColumns = new();
    private SerialPort? _serialPort;
    private const string QueryMessage = "cntfluid";
    private const string ExpectedResponse = "hithere!";
    private readonly Player _player = new();
    private readonly Random _random = new();
    private readonly string LogFolderPath = Path.Combine(AppEnv.AppDataPath, "log");

    public EventWindow()
    {
        InitializeComponent();
        _event = new EventConfig();
    }

    public EventWindow(EventConfig ev)
    {
        InitializeComponent();
        _event = ev;

        if (!Directory.Exists(LogFolderPath))
        {
            try { Directory.CreateDirectory(LogFolderPath); } catch { }
        }
        
        EventTitleText.Text = ev.EventName;
        EventDateText.Text = ev.EventDate.ToString("yyyy/MM/dd");
        
        MemberGrid.ItemsSource = _members;
        LogListBox.ItemsSource = _logs;

        _ = LoadDataAsync();

        // 子機モードの場合、定期的に更新を確認する
        var timer = new global::Avalonia.Threading.DispatcherTimer
        {
            Interval = System.TimeSpan.FromSeconds(3)
        };
        timer.Tick += (s, e) => {
            if (!string.IsNullOrEmpty(fluid_general.Utils.AppEnv.ServerBaseUrl))
            {
                _ = LoadDataAsync();
            }
        };
        timer.Start();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            var service = App.GetDataService();
            var members = await service.GetMembersByRosterAsync(_event.RosterName);
            var logs = await service.GetCheckInLogsAsync(_event.Id);
            var config = await service.GetRosterConfigAsync(_event.RosterName);

            var newColumns = (config?.DisplayColumns != null && config.DisplayColumns.Count > 0)
                ? config.DisplayColumns
                : new List<string> { "名前", "学籍番号" };

            if (!_displayColumns.SequenceEqual(newColumns))
            {
                _displayColumns = newColumns;
                await RebuildColumnsAsync();
            }

            if (_members.Count == 0)
            {
                // 初回読み込み
                foreach (var m in members)
                {
                    var log = logs.FirstOrDefault(l => l.ExcelId == m.ExcelId);
                    var vm = new MemberVM { Member = m, Status = log?.Status ?? "未参加" };
                    vm.PropertyChanged += OnMemberStatusChanged;
                    _members.Add(vm);
                }
            }
            else
            {
                // 更新（既存のインスタンスの状態を書き換える）
                foreach (var vm in _members)
                {
                    var log = logs.FirstOrDefault(l => l.ExcelId == vm.Member.ExcelId);
                    string newStatus = log?.Status ?? "未参加";
                    if (vm.Status != newStatus)
                    {
                        // イベントハンドラによる無限ループを防ぐため、一時的に解除するか
                        // あるいは Status プロパティ内で変更がない場合は何もしないようにしているので、そのまま代入
                        vm.Status = newStatus;
                    }
                }
            }

            UpdateStats();
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex);
        }
    }

    private async void OnMemberStatusChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is MemberVM vm && (e.PropertyName == nameof(MemberVM.Status)))
        {
            UpdateStats();
            WriteLog(vm.Status, vm.Member);
            
            // DB保存
            try
            {
                var service = App.GetDataService();
                await service.UpdateCheckInStatusAsync(_event.RosterName, vm.Member.ExcelId, _event.Id, vm.Status);
            }
            catch (Exception ex)
            {
                AppEnv.LogError(ex);
            }
        }
    }

    private void UpdateStats()
    {
        int total = _members.Count;
        int checkedIn = _members.Count(m => m.Status == "参加済み");
        
        WholeStatusText.Text = $"{checkedIn} / {total}";
        WholeProgressBar.Value = total > 0 ? (double)checkedIn / total * 100 : 0;
    }

    private void WriteLog(string status, Member member)
    {
        string time = DateTime.Now.ToString("HH:mm:ss");
        _logs.Insert(0, $"[{time}] {status}: {member.Name}");
        if (_logs.Count > 100) _logs.RemoveAt(100);

        try
        {
            string logFile = $"{_event.EventName}.txt";
            string logFilePath = Path.Combine(LogFolderPath, logFile);
            string currentTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            
            string room = "";
            if (member.CustomFields.TryGetValue("部屋", out var r1)) room = r1;
            else if (member.CustomFields.TryGetValue("部屋番号", out var r2)) room = r2;
            else if (member.CustomFields.TryGetValue("RoomNumber", out var r3)) room = r3;

            string logEntry = $"{currentTime}, {room}, {member.Name}, {status}\n";
            File.AppendAllText(logFilePath, logEntry);
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex);
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        _serialPort?.Close();
        Close();
    }

    private async void OnDetailedSettingsClick(object sender, RoutedEventArgs e)
    {
        var dialog = new EventSettingsDialog(_event);
        await dialog.ShowDialog(this);
        if (dialog.IsApplied)
        {
            try
            {
                var service = App.GetDataService();
                await service.UpdateEventAsync(_event);
            }
            catch (Exception ex) { AppEnv.LogError(ex); }
        }
    }

    private async void OnAddTerminalClick(object sender, RoutedEventArgs e)
    {
        if (_serialPort != null && _serialPort.IsOpen)
        {
            DisconnectTerminal();
            return;
        }

        AddTerminalButton.IsEnabled = false;
        CertificationLabel.Text = "端末を検索中...";
        ImageBehavior.SetAnimatedSource(StatusAnimation, new Uri("avares://fluid-general.Avalonia/Assets/searching.gif"));

        bool success = false;
        await Task.Run(async () =>
        {
            int retryCount = 0;
            while (retryCount < 5 && !success)
            {
                string[] ports = SerialPort.GetPortNames();
                foreach (string portName in ports)
                {
                    try
                    {
                        var port = new SerialPort(portName)
                        {
                            BaudRate = 115200,
                            Parity = Parity.None,
                            DataBits = 8,
                            StopBits = StopBits.One,
                            Handshake = Handshake.None,
                            ReadTimeout = 2000,
                            WriteTimeout = 2000,
                            NewLine = "\n"
                        };

                        await Task.Delay(200);
                        port.Open();

                        port.DiscardInBuffer();
                        port.DiscardOutBuffer();
                        port.Write(QueryMessage + "\n");
                        
                        string response = "";
                        int attempts = 0;
                        while (attempts < 10)
                        {
                            try
                            {
                                response = port.ReadLine().Trim();
                                break;
                            }
                            catch (TimeoutException)
                            {
                                await Task.Delay(100);
                                attempts++;
                            }
                        }

                        if (response.Contains(ExpectedResponse))
                        {
                            _serialPort = port;
                            success = true;
                            break;
                        }
                        
                        port.Close();
                    }
                    catch { }
                }

                if (!success)
                {
                    retryCount++;
                    await Task.Delay(1000);
                }
            }
        });

        if (success && _serialPort != null && _serialPort.IsOpen)
        {
            CertificationLabel.Text = "接続完了";
            CertificationLabel2.Text = "学生証をタッチしてください";
            ImageBehavior.SetAnimatedSource(StatusAnimation, new Uri("avares://fluid-general.Avalonia/Assets/waiting.gif"));
            AddTerminalButton.Content = "切断";
            ShutdownButton.IsVisible = true;
            _ = ReceiveDataLoop();
        }
        else
        {
            CertificationLabel.Text = "端末が見つかりません";
            CertificationLabel2.Text = "再試行してください";
            ImageBehavior.SetAnimatedSource(StatusAnimation, null!);
        }
        AddTerminalButton.IsEnabled = true;
    }

    private async Task ReceiveDataLoop()
    {
        if (_serialPort == null) return;
        
        await Task.Run(async () =>
        {
            while (_serialPort != null && _serialPort.IsOpen)
            {
                try
                {
                    if (_serialPort.BytesToRead > 0)
                    {
                        int availableBytes = _serialPort.BytesToRead;
                        byte[] buffer = new byte[availableBytes];
                        int bytesRead = _serialPort.Read(buffer, 0, buffer.Length);
                        
                        string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        if (!string.IsNullOrEmpty(data))
                        {
                            data = data.Trim();
                            await Dispatcher.UIThread.InvokeAsync(() => HandleNfcData(data));
                        }
                    }
                }
                catch (Exception)
                {
                    break;
                }
                await Task.Delay(100);
            }
            
            await Dispatcher.UIThread.InvokeAsync(() => DisconnectTerminal());
        });
    }

    private string _lastStudentNumber = "";

    private void HandleNfcData(string data)
    {
        if (string.IsNullOrEmpty(data)) return;

        if (data == "000000000")
        {
            CertificationLabel.Text = "認証エラー";
            CertificationLabel2.Text = "学生証以外が認識されました。再度試してください。";
            CertificationBorder.Background = Brushes.Crimson;
            PlaySound("Gate_Alert.wav");
            
            _ = Task.Delay(3000).ContinueWith(_ => Dispatcher.UIThread.InvokeAsync(() => CertificationBorder.Background = new SolidColorBrush(Color.FromArgb(21, 255, 255, 255))));
            return;
        }

        if (data == "E0001")
        {
            CertificationLabel.Text = "スキャナーエラー";
            CertificationLabel2.Text = "NFCスキャナを再接続してください。";
            CertificationBorder.Background = Brushes.Gold;
            PlaySound("Gate_Alert.wav");
            
            _ = Task.Delay(3000).ContinueWith(_ => Dispatcher.UIThread.InvokeAsync(() => CertificationBorder.Background = new SolidColorBrush(Color.FromArgb(21, 255, 255, 255))));
            return;
        }

        string studentNumber = data;
        if (studentNumber.Length > 2) studentNumber = studentNumber.Substring(0, studentNumber.Length - 2);

        var vm = _members.FirstOrDefault(m => m.Member.StudentNumber == studentNumber || m.Member.ExcelId.ToString() == studentNumber);
        if (vm != null)
        {
            if (vm.Status == "参加済み")
            {
                CertificationLabel.Text = $"{vm.Member.Name} さんは参加済みです";
                CertificationBorder.Background = Brushes.Gold;
                if (_event.SameStudentSetting) PlaySound("Gate_Alert.wav");
            }
            else
            {
                vm.Status = "参加済み";
                CertificationLabel.Text = $"{vm.Member.Name} さんが参加しました";
                CertificationBorder.Background = Brushes.DodgerBlue;
                PlaySuccessSound();
            }
            CertificationLabel2.Text = $"{vm.Member.StudentNumber}";
        }
        else
        {
            CertificationLabel.Text = "未登録のカードです";
            CertificationLabel2.Text = data;
            CertificationBorder.Background = Brushes.Crimson;
            PlaySound("Gate_Alert.wav");
        }

        _lastStudentNumber = studentNumber;

        _ = Task.Delay(3000).ContinueWith(_ => Dispatcher.UIThread.InvokeAsync(() => CertificationBorder.Background = new SolidColorBrush(Color.FromArgb(21, 255, 255, 255))));
    }

    private void PlaySuccessSound()
    {
        if (_event.TouchSound == "JUGGLER")
        {
            int r = _random.Next(1, 4);
            PlaySound($"j_{r}.wav");
        }
        else
        {
            PlaySound("Gate_BEEP.wav");
        }
    }

    private void PlaySound(string fileName)
    {
        try
        {
            string path = Path.Combine(AppEnv.AppDataPath, "Sound", fileName);
            if (File.Exists(path))
            {
                _player.Play(path);
            }
        }
        catch (Exception ex) { AppEnv.LogError(ex); }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        DisconnectTerminal();
        base.OnClosing(e);
    }

    private void DisconnectTerminal()
    {
        if (_serialPort != null && _serialPort.IsOpen)
        {
            try
            {
                _serialPort.Write("111111111\n");
            }
            catch { }
            try
            {
                _serialPort.Close();
            }
            catch { }
        }
        _serialPort = null;
        AddTerminalButton.Content = "端末接続";
        ShutdownButton.IsVisible = false;
        CertificationLabel.Text = "";
        CertificationLabel2.Text = "";
        ImageBehavior.SetAnimatedSource(StatusAnimation, null!);
    }

    private void OnShutdownTerminalClick(object sender, RoutedEventArgs e)
    {
        if (_serialPort != null && _serialPort.IsOpen)
        {
            try
            {
                _serialPort.Write("222222222\n");
            }
            catch { }
            try
            {
                _serialPort.Close();
            }
            catch { }
        }
        _serialPort = null;
        AddTerminalButton.Content = "端末接続";
        ShutdownButton.IsVisible = false;
        CertificationLabel.Text = "";
        CertificationLabel2.Text = "";
        ImageBehavior.SetAnimatedSource(StatusAnimation, null!);
    }

    private async Task RebuildColumnsAsync()
    {
        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            MemberGrid.Columns.Clear();
            foreach (var col in _displayColumns)
            {
                if (col == "名前")
                    MemberGrid.Columns.Add(new DataGridTextColumn { Header = "名前", Binding = new global::Avalonia.Data.Binding("Member.Name"), IsReadOnly = true, Width = new DataGridLength(200) });
                else if (col == "学籍番号")
                    MemberGrid.Columns.Add(new DataGridTextColumn { Header = "学籍番号", Binding = new global::Avalonia.Data.Binding("Member.StudentNumber"), IsReadOnly = true, Width = new DataGridLength(150) });
                else if (col == "かな")
                    MemberGrid.Columns.Add(new DataGridTextColumn { Header = "かな", Binding = new global::Avalonia.Data.Binding("Member.Kana"), IsReadOnly = true, Width = new DataGridLength(150) });
                else
                    MemberGrid.Columns.Add(new DataGridTextColumn { Header = col, Binding = new global::Avalonia.Data.Binding($"Member.CustomFields[{col}]"), IsReadOnly = true, Width = new DataGridLength(150) });
            }

            // ステータス列（固定）
            var statusColumn = new DataGridTemplateColumn
            {
                Header = "状態",
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                CellTemplate = (global::Avalonia.Markup.Xaml.Templates.DataTemplate)this.Resources["StatusColumnTemplate"]!
            };
            MemberGrid.Columns.Add(statusColumn);
        });
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void OnFilterChanged(object? sender, RoutedEventArgs e)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        string query = SearchBox.Text?.ToLower() ?? "";
        bool showRegistered = ShowRegisteredCheckBox.IsChecked == true;
        bool showNotRegistered = ShowNotRegisteredCheckBox.IsChecked == true;
        bool showAbsent = ShowAbsentCheckBox.IsChecked == true;

        var filtered = _members.Where(vm =>
        {
            // Status filter
            bool statusMatch = (vm.Status == "参加済み" && showRegistered) ||
                             (vm.Status == "未参加" && showNotRegistered) ||
                             (vm.Status == "不参加" && showAbsent);
            
            if (!statusMatch) return false;

            // Search query filter
            if (string.IsNullOrWhiteSpace(query)) return true;

            return (vm.Member.Name?.ToLower().Contains(query) == true) ||
                   (vm.Member.StudentNumber?.ToLower().Contains(query) == true) ||
                   (vm.Member.Kana?.ToLower().Contains(query) == true) ||
                   (vm.Member.CustomFields.Values.Any(v => v?.ToLower().Contains(query) == true));
        }).ToList();

        MemberGrid.ItemsSource = filtered;
    }

    private async void OnDisplaySettingsClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var service = App.GetDataService();
            var members = await service.GetMembersByRosterAsync(_event.RosterName);
            var allKeys = new List<string> { "名前", "かな", "学籍番号" };
            allKeys.AddRange(members.SelectMany(m => m.CustomFields.Keys).Distinct());
            allKeys = allKeys.Distinct().ToList();

            var dialog = new DisplaySettingsDialog(allKeys, _displayColumns);
            await dialog.ShowDialog(this);

            if (dialog.IsApplied)
            {
                _displayColumns = dialog.GetSelectedColumns();
                var config = await service.GetRosterConfigAsync(_event.RosterName) ?? new RosterConfig { RosterName = _event.RosterName };
                config.DisplayColumns = _displayColumns;
                await service.UpdateRosterConfigAsync(config);
                await RebuildColumnsAsync();
            }
        }
        catch (Exception ex) { AppEnv.LogError(ex); }
    }

    private async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new ImportDialog(_event);
        await dialog.ShowDialog(this);
        if (dialog.IsImportSuccessful)
        {
            await LoadDataAsync();
        }
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new ExportListDialog(_event);
        await dialog.ShowDialog(this);
    }

    private void OnOpenLogFolderClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (Directory.Exists(LogFolderPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", LogFolderPath);
            }
        }
        catch (Exception ex) { AppEnv.LogError(ex); }
    }

    private void OnOpenLogFileClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            string logFile = $"{_event.EventName}.txt";
            string logFilePath = Path.Combine(LogFolderPath, logFile);

            if (File.Exists(logFilePath))
            {
                System.Diagnostics.Process.Start("notepad.exe", logFilePath);
            }
        }
        catch (Exception ex) { AppEnv.LogError(ex); }
    }

    private void OnDeleteLogFileClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            string logFile = $"{_event.EventName}.txt";
            string logFilePath = Path.Combine(LogFolderPath, logFile);

            if (File.Exists(logFilePath))
            {
                File.Delete(logFilePath);
                _logs.Clear();
            }
        }
        catch (Exception ex) { AppEnv.LogError(ex); }
    }

    private void OnClearLogClick(object? sender, RoutedEventArgs e)
    {
        _logs.Clear();
    }
}
