using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace fluid_general.Services
{
    public class UpdateCheckResult
    {
        public bool IsNewVersionAvailable { get; set; }
        public string LatestVersion { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public string WindowsDownloadUrl { get; set; } = string.Empty;
        public string MacAppleSiliconDownloadUrl { get; set; } = string.Empty;
        public string MacIntelDownloadUrl { get; set; } = string.Empty;
        public string HtmlUrl { get; set; } = string.Empty;
    }

    public static class UpdateCheckerService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string Owner = "YoshikiFujii";
        private const string Repo = "fluid-general";

        public static async Task<UpdateCheckResult> CheckForUpdatesAsync(string currentVersionString)
        {
            var result = new UpdateCheckResult();
            try
            {
                // GitHub API requires a User-Agent header to prevent 403 Forbidden
                _httpClient.DefaultRequestHeaders.UserAgent.Clear();
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("FluidGeneralUpdateChecker/1.0");

                var url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
                var responseString = await _httpClient.GetStringAsync(url);

                using (var doc = JsonDocument.Parse(responseString))
                {
                    var root = doc.RootElement;
                    var tagName = root.GetProperty("tag_name").GetString() ?? string.Empty;
                    var body = root.GetProperty("body").GetString() ?? string.Empty;
                    var htmlUrl = root.GetProperty("html_url").GetString() ?? string.Empty;

                    result.LatestVersion = tagName.TrimStart('v');
                    result.ReleaseNotes = body;
                    result.HtmlUrl = htmlUrl;

                    // Compare versions
                    if (Version.TryParse(currentVersionString.TrimStart('v'), out var currentVer) &&
                        Version.TryParse(result.LatestVersion, out var latestVer))
                    {
                        if (latestVer > currentVer)
                        {
                            result.IsNewVersionAvailable = true;
                        }
                    }

                    // Extract download URLs from assets array
                    if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var asset in assets.EnumerateArray())
                        {
                            var name = asset.GetProperty("name").GetString() ?? string.Empty;
                            var downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? string.Empty;

                            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                result.WindowsDownloadUrl = downloadUrl;
                            }
                            else if (name.EndsWith(".dmg", StringComparison.OrdinalIgnoreCase))
                            {
                                if (name.Contains("AppleSilicon", StringComparison.OrdinalIgnoreCase) || 
                                    name.Contains("arm64", StringComparison.OrdinalIgnoreCase))
                                {
                                    result.MacAppleSiliconDownloadUrl = downloadUrl;
                                }
                                else if (name.Contains("Intel", StringComparison.OrdinalIgnoreCase) || 
                                         name.Contains("x64", StringComparison.OrdinalIgnoreCase))
                                {
                                    result.MacIntelDownloadUrl = downloadUrl;
                                }
                                else
                                {
                                    // Fallback in case of a single unannotated DMG
                                    if (string.IsNullOrEmpty(result.MacAppleSiliconDownloadUrl))
                                    {
                                        result.MacAppleSiliconDownloadUrl = downloadUrl;
                                    }
                                    if (string.IsNullOrEmpty(result.MacIntelDownloadUrl))
                                    {
                                        result.MacIntelDownloadUrl = downloadUrl;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Fail silently, just log error via diagnostics
                System.Diagnostics.Debug.WriteLine($"Update check failed: {ex.Message}");
            }

            return result;
        }
    }
}
