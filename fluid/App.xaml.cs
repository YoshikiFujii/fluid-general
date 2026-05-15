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

namespace fluid_general
{
    /// <summary>
    /// App.xaml の相互作用ロジック
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            // 未処理の例外を捕捉するイベントを登録
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            base.OnStartup(e);

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
    }
}
