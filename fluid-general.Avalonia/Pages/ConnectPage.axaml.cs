using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using fluid_general.Services;
using fluid_general.Utils;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace fluid_general.Avalonia.Pages;

public class ParentInfo
{
    public string Name { get; set; } = "";
    public string Ip { get; set; } = "";
}

public partial class ConnectPage : UserControl
{

    public ConnectPage()
    {
        InitializeComponent();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        string? url = AppEnv.ServerBaseUrl;
        if (string.IsNullOrEmpty(url))
        {
            StatusDot.Fill = Brushes.Gray;
            StatusTextBlock.Text = "現在、親機モード（ローカル接続）で動作しています。";
            RemoteUrlInfo.IsVisible = false;
            DisconnectButton.IsEnabled = false;
        }
        else
        {
            StatusDot.Fill = Brushes.LimeGreen;
            StatusTextBlock.Text = "現在、子機モードで動作しています。";
            RemoteUrlInfo.IsVisible = true;
            RemoteUrlText.Text = url;
            DisconnectButton.IsEnabled = true;
            ServerAddressTextBox.Text = url;
        }
    }

    private async void OnConnectClick(object sender, RoutedEventArgs e)
    {
        string url = ServerAddressTextBox.Text?.Trim() ?? "";
        await ProcessConnectionAsync(url);
    }

    private async Task ProcessConnectionAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            SearchStatusTextBlock.Text = "URLを入力してください。";
            return;
        }

        if (!url.StartsWith("http")) url = "http://" + url;
        if (!url.EndsWith("/")) url += "/";

        StatusTextBlock.Text = "接続テスト中...";
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            client.DefaultRequestHeaders.Add("X-Fluid-MachineName", Environment.MachineName);
            
            // 親機のAPIを叩いて生存確認 (名簿取得APIなどで代用)
            var response = await client.GetAsync($"{url}api/members");

            if (response.IsSuccessStatusCode)
            {
                AppEnv.ServerBaseUrl = url;
                UpdateStatus();
                SearchStatusTextBlock.Text = "接続に成功しました。";
            }
            else
            {
                SearchStatusTextBlock.Text = $"接続に失敗しました (Status: {response.StatusCode})";
            }
        }
        catch (Exception ex)
        {
            SearchStatusTextBlock.Text = $"エラー: {ex.Message}";
        }
    }

    private void OnDisconnectClick(object sender, RoutedEventArgs e)
    {
        AppEnv.ServerBaseUrl = null;
        UpdateStatus();
        SearchStatusTextBlock.Text = "ローカルモードに戻りました。";
    }

    private async void OnSearchClick(object sender, RoutedEventArgs e)
    {
        SearchButton.IsEnabled = false;
        SearchProgressBar.IsVisible = true;
        SearchStatusTextBlock.Text = "親機を探索中...";
        ParentListBox.ItemsSource = null;

        try
        {
            var results = await DiscoveryService.DiscoverParentsAsync();
            var list = new List<ParentInfo>();
            foreach (var res in results)
            {
                var parts = res.Split('|');
                if (parts.Length >= 2)
                {
                    list.Add(new ParentInfo { Name = parts[0], Ip = parts[1] });
                }
            }

            ParentListBox.ItemsSource = list;
            SearchStatusTextBlock.Text = list.Count > 0 ? $"{list.Count} 台の親機が見つかりました。" : "親機は見つかりませんでした。";
        }
        catch (Exception ex)
        {
            SearchStatusTextBlock.Text = $"探索エラー: {ex.Message}";
        }
        finally
        {
            SearchButton.IsEnabled = true;
            SearchProgressBar.IsVisible = false;
        }
    }

    private async void OnParentSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ParentListBox.SelectedItem is ParentInfo parent)
        {
            string url = $"http://{parent.Ip}:51500/";
            await ProcessConnectionAsync(url);
        }
    }
}
