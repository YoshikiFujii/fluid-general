using Avalonia.Controls;
using Avalonia.Interactivity;
using fluid_general.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace fluid_general.Avalonia.Pages
{
    public partial class UpdateDialog : Window
    {
        private readonly UpdateCheckResult _updateInfo;
        private readonly HttpClient _httpClient = new HttpClient();

        public UpdateDialog()
        {
            InitializeComponent();
            _updateInfo = new UpdateCheckResult();
        }

        public UpdateDialog(UpdateCheckResult updateInfo)
        {
            InitializeComponent();
            _updateInfo = updateInfo;

            // UIの初期化
            VersionText.Text = $"新しいバージョン v{updateInfo.LatestVersion} が利用可能です！";
            ReleaseNotesText.Text = string.IsNullOrWhiteSpace(updateInfo.ReleaseNotes)
                ? "・パフォーマンス改善とバグの修正が行われました。"
                : updateInfo.ReleaseNotes;
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void OnUpdateClick(object? sender, RoutedEventArgs e)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await HandleWindowsUpdateAsync();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                HandleMacUpdate();
            }
            else
            {
                // Linux 等のその他プラットフォームはブラウザでリリースを開く
                OpenBrowser(_updateInfo.HtmlUrl);
                Close();
            }
        }

        private async Task HandleWindowsUpdateAsync()
        {
            var downloadUrl = _updateInfo.WindowsDownloadUrl;
            if (string.IsNullOrEmpty(downloadUrl))
            {
                // 万が一URLが取れなかった場合はブラウザで開く
                OpenBrowser(_updateInfo.HtmlUrl);
                Close();
                return;
            }

            // UI切り替え (ダウンロード中)
            ButtonArea.IsVisible = false;
            ProgressArea.IsVisible = true;

            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), $"FluidGeneral_Setup_{Guid.NewGuid():N}.exe");
                
                await DownloadFileWithProgressAsync(downloadUrl, tempPath);

                // インストーラーの起動（管理者権限への昇格要求を含む）
                var startInfo = new ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                Process.Start(startInfo);

                // アプリケーションを即座に終了してインストーラーによる上書きを可能にする
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                fluid_general.Utils.AppEnv.LogError(ex);
                // エラー時はブラウザに逃がす
                OpenBrowser(downloadUrl);
                Close();
            }
        }

        private void HandleMacUpdate()
        {
            // Apple Silicon か Intel かの判定
            bool isAppleSilicon = RuntimeInformation.OSArchitecture == Architecture.Arm64;
            string downloadUrl = isAppleSilicon 
                ? _updateInfo.MacAppleSiliconDownloadUrl 
                : _updateInfo.MacIntelDownloadUrl;

            // 片方のURLが空の場合はもう片方、あるいはリリース全体のURLをフォールバックとして使用
            if (string.IsNullOrEmpty(downloadUrl))
            {
                downloadUrl = !string.IsNullOrEmpty(_updateInfo.MacAppleSiliconDownloadUrl)
                    ? _updateInfo.MacAppleSiliconDownloadUrl
                    : (!string.IsNullOrEmpty(_updateInfo.MacIntelDownloadUrl) ? _updateInfo.MacIntelDownloadUrl : _updateInfo.HtmlUrl);
            }

            // ブラウザで直接ダウンロード（またはリリース一覧）を開く
            OpenBrowser(downloadUrl);
            Close();
        }

        private async Task DownloadFileWithProgressAsync(string url, string destinationPath)
        {
            using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var canReportProgress = totalBytes != -1L;

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    var totalRead = 0L;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;

                        if (canReportProgress)
                        {
                            var percentage = (double)totalRead / totalBytes * 100;
                            await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                DownloadProgressBar.Value = percentage;
                                StatusText.Text = $"アップデートをダウンロード中 ({percentage:0}%)...";
                            });
                        }
                    }
                }
            }
        }

        private static void OpenBrowser(string url)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    Process.Start("xdg-open", url);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open browser: {ex.Message}");
            }
        }
    }
}
