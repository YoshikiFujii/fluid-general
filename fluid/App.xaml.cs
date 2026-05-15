using Microsoft.Win32;
using ModernWpf;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using ClosedXML.Graphics;
using System.Reflection;
using SixLabors.Fonts;
using System.Globalization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Net.Http;
using System.Threading.Tasks;
using fluid_general.Data;

namespace fluid_general
{
    /// <summary>
    /// App.xaml の相互作用ロジック
    /// </summary>
    public partial class App : Application
    {
        private IHost? _host;
        
        // 子機モード（他PCのセッションに接続）として動作する場合のベースURL
        public static string? ServerBaseUrl 
        { 
            get => fluid_general.Properties.Settings.Default.ServerBaseUrl;
            set 
            {
                fluid_general.Properties.Settings.Default.ServerBaseUrl = value;
                fluid_general.Properties.Settings.Default.Save();
            }
        }

        public static Services.IDataService GetDataService()
        {
            if (string.IsNullOrEmpty(ServerBaseUrl))
            {
                return new Services.LocalDataService();
            }
            else
            {
                return new Services.RemoteDataService();
            }
        }

        public App()
        {
            // 未処理の例外を捕捉するイベントを登録
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;

            // Web API サーバーの構成
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
                    });
                    // ポート5000で待ち受け（必要に応じて設定から読み込む形に変更可）
                    webBuilder.UseUrls("http://0.0.0.0:5000"); 
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            base.OnStartup(e);

            if (_host != null)
            {
                await _host.StartAsync();

                // データベースの初期化
                using (var scope = _host.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    db.Database.EnsureCreated();

                    // RosterName カラムが不足している場合の簡易マイグレーション
                    try
                    {
                        var conn = db.Database.GetDbConnection();
                        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "PRAGMA table_info(Members);";
                            bool exists = false;
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    if (reader["name"].ToString() == "RosterName") { exists = true; break; }
                                }
                            }
                            if (!exists)
                            {
                                cmd.CommandText = "ALTER TABLE Members ADD COLUMN RosterName TEXT DEFAULT '';";
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError(ex); // 失敗しても起動を優先
                    }
                }
            }

            try
            {
                string fontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "NotoSansJP-Regular.ttf");
                var customFonts = new FontCollection();
                var notoFamily = customFonts.Add(fontPath);

                // システムフォントの代わりとして使用（オプション）
                Font fallbackFont = notoFamily.CreateFont(12, SixLabors.Fonts.FontStyle.Regular);

                // 以降、明示的にこれを使う必要がある
                // 例: image.Mutate(ctx => ctx.DrawText("Hello", fallbackFont, ...));
            }
            catch (Exception ex)
            {
                File.WriteAllText("font_error.log", ex.ToString());
                MessageBox.Show("Notoフォントの読み込みに失敗しました");
            }

            // リモート接続の検証 (子機モードの場合)
            if (!string.IsNullOrEmpty(ServerBaseUrl))
            {
                _ = VerifyRemoteConnectionAsync();
            }
            else
            {
                // 親機モードとしてログに出力
                Console.WriteLine("Mode: Master (Local)");
            }

            ApplyTheme();
            // システムのテーマ変更イベントを登録
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            // ModernWPF のテーマ変更イベントも登録
            ThemeManager.Current.ActualApplicationThemeChanged += OnThemeChanged;

        }
        private void OnThemeChanged(ThemeManager sender, object args)
        {
            ApplyTheme(); // ModernWPFテーマが変更された場合
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (_host != null)
            {
                await _host.StopAsync(TimeSpan.FromSeconds(5));
                _host.Dispose();
            }
            base.OnExit(e);
        }


        //↓の関数でテーマ変更時自動で読み込むようにしたが、なぜかwindowをリロードしないと適応されない

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General)
            {
                ApplyTheme(); // システムのテーマが変更された場合
            }
        }

        private void ApplyTheme()
        {
            bool isDark = ThemeManager.Current.ActualApplicationTheme == ApplicationTheme.Dark;
            string themePath = isDark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml";

            // ModernWPF のリソースを維持しながらカスタムリソースのみ変更
            var dictionaries = Resources.MergedDictionaries;
            // 既存のカスタムリソースを削除 (ModernWPF のリソースは残す)
            dictionaries.Remove(dictionaries.FirstOrDefault(d => d.Source?.ToString().Contains("Themes/") == true));
            Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(themePath, UriKind.Relative) });

        }
        // AppData内のアプリ専用フォルダへのパスを取得
        public static string AppDataPath
        {
            get
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "fluid-general");
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                return path;
            }
        }

        // アプリケーション内での未処理例外をキャッチ
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            LogError(ex);
            MessageBox.Show("予期しないエラーが発生しました。詳細はログを確認してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        // UIスレッドでの未処理例外をキャッチ
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogError(e.Exception);
            MessageBox.Show("予期しないエラーが発生しました。詳細はログを確認してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;  // エラーが発生してもアプリケーションが終了しないようにする
        }

        // エラーログをファイルに書き出すメソッド
        public static void LogError(Exception ex)
        {
            try
            {
                // AppData内にログを出力するように変更
                string logFilePath = Path.Combine(AppDataPath, "error.log");

                // ログを追記モードで書き込む
                using (StreamWriter writer = new StreamWriter(logFilePath, true))
                {
                    writer.WriteLine("日時: " + DateTime.Now);
                    writer.WriteLine("メッセージ: " + ex.Message);
                    writer.WriteLine("スタックトレース: " + ex.StackTrace);
                    writer.WriteLine("------------------------------------------------------");
                }
            }
            catch (Exception loggingEx)
            {
                // ログ書き込み中にエラーが発生した場合、標準出力にエラーメッセージを出力
                Console.WriteLine("エラーログの書き込みに失敗しました: " + loggingEx.Message);
            }
        }

        public static async Task VerifyRemoteConnectionAsync()
        {
            if (string.IsNullOrEmpty(ServerBaseUrl)) return;

            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(3);
                var response = await client.GetAsync($"{ServerBaseUrl}api/members");
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Mode: Sub-machine (Remote) - Connected to {ServerBaseUrl}");
                }
                else
                {
                    Console.WriteLine($"Mode: Sub-machine (Remote) - Connection Failed to {ServerBaseUrl} (Status: {response.StatusCode})");
                    // 必要に応じてユーザーに通知
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Mode: Sub-machine (Remote) - Connection Error to {ServerBaseUrl}: {ex.Message}");
            }
        }
    }
}
