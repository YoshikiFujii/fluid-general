using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
            // データベースの初期化 (親機モードの場合のみ)
            if (string.IsNullOrEmpty(AppEnv.ServerBaseUrl))
            {
                using var db = new AppDbContext();
                db.Database.EnsureCreated();
                
                // 親機モードならAPIサーバーを起動
                await StartServerAsync();
            }

            // サウンドファイルの書き出し
            ExtractSounds();
            
            // 探索用サーバーの起動
            DiscoveryService.StartServer();
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