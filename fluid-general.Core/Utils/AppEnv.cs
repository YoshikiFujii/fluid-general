using System;
using System.IO;

namespace fluid_general.Utils
{
    public static class AppEnv
    {
        // 接続先URLの変更通知
        public static event EventHandler? ConnectionModeChanged;

        private static string ConfigFilePath => Path.Combine(AppDataPath, "server_url.txt");

        // 子機モード（他PCのセッションに接続）として動作する場合のベースURL
        public static string? ServerBaseUrl 
        { 
            get 
            {
                try
                {
                    if (File.Exists(ConfigFilePath))
                    {
                        var url = File.ReadAllText(ConfigFilePath).Trim();
                        return string.IsNullOrEmpty(url) ? null : url;
                    }
                }
                catch { }
                return null;
            }
            set 
            {
                try
                {
                    File.WriteAllText(ConfigFilePath, value ?? string.Empty);
                }
                catch { }
                ConnectionModeChanged?.Invoke(null, EventArgs.Empty);
            }
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

        // エラーログをファイルに書き出すメソッド
        public static void LogError(Exception ex)
        {
            try
            {
                string logFilePath = Path.Combine(AppDataPath, "error.log");

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
                Console.WriteLine("エラーログの書き込みに失敗しました: " + loggingEx.Message);
            }
        }
    }
}
