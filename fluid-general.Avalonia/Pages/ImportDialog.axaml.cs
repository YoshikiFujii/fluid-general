using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ClosedXML.Excel;
using fluid_general.Models;
using fluid_general.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace fluid_general.Avalonia.Pages;

public partial class ImportDialog : Window
{
    private readonly EventConfig _event;
    public bool IsImportSuccessful { get; private set; }

    public ImportDialog()
    {
        InitializeComponent();
        _event = new EventConfig(); // Designer support
    }

    public ImportDialog(EventConfig eventConfig)
    {
        InitializeComponent();
        _event = eventConfig;
        _ = LoadMatchColumnsAsync();
    }

    private async Task LoadMatchColumnsAsync()
    {
        try
        {
            var service = App.GetDataService();
            var members = await service.GetMembersByRosterAsync(_event.RosterName);
            
            var allKeys = new List<string> { "名前", "かな", "学籍番号" };
            allKeys.AddRange(members.SelectMany(m => m.CustomFields.Keys).Distinct());
            allKeys = allKeys.Distinct().ToList();

            MatchColumnComboBox.ItemsSource = allKeys;
            if (allKeys.Contains("学籍番号")) MatchColumnComboBox.SelectedItem = "学籍番号";
            else if (allKeys.Count > 0) MatchColumnComboBox.SelectedIndex = 0;
        }
        catch (Exception ex) { AppEnv.LogError(ex); }
    }

    private async void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Excelファイルを選択",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("Excel Files") { Patterns = new[] { "*.xlsx" } } }
        });

        if (files.Count >= 1)
        {
            FilePathTextBox.Text = files[0].Path.LocalPath;
        }
    }

    private async void OnExecuteClick(object sender, RoutedEventArgs e)
    {
        string filePath = FilePathTextBox.Text ?? "";
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

        string? searchKey = MatchColumnComboBox.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(searchKey)) return;

        string? targetStatus = (TargetStatusComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (string.IsNullOrEmpty(targetStatus)) return;

        ExecuteButton.IsEnabled = false;
        ImportProgressBar.IsVisible = true;
        StatusMessage.Text = "処理中...";

        try
        {
            var dataList = new HashSet<string>();
            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook(filePath);
                var worksheet = workbook.Worksheets.First();
                foreach (var cell in worksheet.Column(1).CellsUsed())
                {
                    string val = cell.GetValue<string>()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(val)) dataList.Add(val);
                }
            });

            var service = App.GetDataService();
            var members = await service.GetMembersByRosterAsync(_event.RosterName);
            
            ImportProgressBar.IsIndeterminate = false;
            ImportProgressBar.Maximum = dataList.Count;
            ImportProgressBar.Value = 0;

            int updated = 0;
            foreach (var val in dataList)
            {
                var member = members.FirstOrDefault(m => 
                {
                    if (searchKey == "名前") return m.Name == val;
                    if (searchKey == "かな") return m.Kana == val;
                    if (searchKey == "学籍番号") return m.StudentNumber == val;
                    return m.CustomFields.GetValueOrDefault(searchKey) == val;
                });

                if (member != null)
                {
                    await service.UpdateCheckInStatusAsync(member.RosterName, member.ExcelId, _event.Id, targetStatus);
                    updated++;
                }
                ImportProgressBar.Value++;
            }

            IsImportSuccessful = true;
            StatusMessage.Text = $"完了: {updated}名のステータスを更新しました。";
            await Task.Delay(2000);
            Close();
        }
        catch (Exception ex)
        {
            AppEnv.LogError(ex);
            StatusMessage.Text = "エラーが発生しました。";
            ExecuteButton.IsEnabled = true;
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();
}
