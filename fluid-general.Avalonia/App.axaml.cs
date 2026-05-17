using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using fluid_general.Services;
using fluid_general.Utils;
using fluid_general.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace fluid_general.Avalonia;

public partial class App : Application
{
    private static IHost? _host;
    private static DispatcherTimer? _connectionWatchdog;
    private static int _syncFailureCount = 0;
    private const int MaxSyncFailures = 3;

    public static IDataService GetDataService()
    {
        if (string.IsNullOrEmpty(AppEnv.ServerBaseUrl))
        {
            return new LocalDataService();
        }
        else
        {
            return new RemoteDataService();
        }
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        try
        {
            // データベースの初期化
            using (var db = new AppDbContext())
            {
                db.Database.EnsureCreated();
            }
            
            // APIサーバーを起動 (常に起動しておくことで親機モードへの切り替えに備える)
            await StartServerAsync();

            // サウンドファイルの書き出し
            ExtractSounds();
            
            // 探索用サーバーの起動
            DiscoveryService.StartServer();

            // 接続監視タイマーの開始
            StartConnectionWatchdog();
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex);
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            desktop.Exit += OnExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void StartConnectionWatchdog()
    {
        _connectionWatchdog = new DispatcherTimer();
        _connectionWatchdog.Interval = TimeSpan.FromSeconds(3);
        _connectionWatchdog.Tick += async (s, e) => await CheckConnectionAsync();
        _connectionWatchdog.Start();
    }

    private async Task CheckConnectionAsync()
    {
        if (string.IsNullOrEmpty(AppEnv.ServerBaseUrl))
        {
            _syncFailureCount = 0;
            return;
        }

        try
        {
            var service = new RemoteDataService();
            // 生存確認
            await service.GetEventsAsync();
            _syncFailureCount = 0;
        }
        catch
        {
            _syncFailureCount++;
            if (_syncFailureCount >= MaxSyncFailures)
            {
                await HandleConnectionLossAsync();
            }
        }
    }

    private async Task HandleConnectionLossAsync()
    {
        _syncFailureCount = 0;
        string? oldUrl = AppEnv.ServerBaseUrl;
        if (string.IsNullOrEmpty(oldUrl)) return;

        // 親機モード（ローカル接続）に切り替え
        AppEnv.ServerBaseUrl = null; 

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // イベントウィンドウが開いていれば閉じる
            var windowsToClose = desktop.Windows
                .Where(w => w.GetType().Name == "EventWindow")
                .ToList();
            
            foreach (var win in windowsToClose)
            {
                win.Close();
            }

            // 通知
            // Avalonia には標準の MessageBox がないので、MainWindow などを通じて通知するか、
            // 面倒なので Console またはデバッグ出力（ユーザーへのメッセージとしては別途実装が必要かもしれないが、WPF版に合わせる）
            // ここでは MainWindow があればダイアログを出してみる
            if (desktop.MainWindow is Window mainWin)
            {
                // ここでは簡易的な通知としてタイトル等に反映されるのを確認してもらう
                // 必要なら ContentDialog 等で通知するロジックを追加
            }
        }
    }

    private async Task StartServerAsync()
    {
        try
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
                    });
                    webBuilder.ConfigureServices(services =>
                    {
                        services.AddControllers();
                        services.AddDbContext<AppDbContext>();
                        services.AddTransient<IDataService, LocalDataService>();
                    });
                    webBuilder.UseUrls("http://0.0.0.0:5000");
                })
                .Build();

            await _host.StartAsync();
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex);
        }
    }

    private async void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        if (_host != null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }
        DiscoveryService.StopServer();
        _connectionWatchdog?.Stop();
    }

    private void ExtractSounds()
    {
        string soundDir = System.IO.Path.Combine(AppEnv.AppDataPath, "Sound");
        if (!System.IO.Directory.Exists(soundDir)) System.IO.Directory.CreateDirectory(soundDir);

        string[] sounds = { "Gate_Alert.wav", "Gate_BEEP.wav", "j_1.wav", "j_2.wav", "j_3.wav" };
        foreach (var sound in sounds)
        {
            string targetPath = System.IO.Path.Combine(soundDir, sound);
            if (!System.IO.File.Exists(targetPath))
            {
                try
                {
                    using var stream = global::Avalonia.Platform.AssetLoader.Open(new Uri($"avares://fluid-general.Avalonia/Assets/Sound/{sound}"));
                    using var fileStream = System.IO.File.Create(targetPath);
                    stream.CopyTo(fileStream);
                }
                catch (Exception ex) { AppEnv.LogError(ex); }
            }
        }
    }
}