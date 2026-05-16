using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ClosedXML.Excel;
using fluid_general.Models;
using fluid_general.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace fluid_general.Avalonia.Pages;

public partial class RosterPage : UserControl
{
    public record RosterInfo(string RosterName, string TotalCount);

    public RosterPage()
    {
        InitializeComponent();
        _ = LoadRosterFilesAsync();
        fluid_general.Utils.AppEnv.ConnectionModeChanged += (s, e) => _ = LoadRosterFilesAsync();

        // 子機モードの場合、定期的に更新を確認する
        var timer = new global::Avalonia.Threading.DispatcherTimer
        {
            Interval = System.TimeSpan.FromSeconds(5)
        };
        timer.Tick += (s, e) => {
            if (!string.IsNullOrEmpty(fluid_general.Utils.AppEnv.ServerBaseUrl))
            {
                _ = LoadRosterFilesAsync();
            }
        };
        timer.Start();
    }

    private async Task LoadRosterFilesAsync()
    {
        try
        {
            var service = App.GetDataService();
            var allMembers = await service.GetMembersAsync();
            
            var rosters = allMembers
                .Where(m => !string.IsNullOrEmpty(m.RosterName))
                .GroupBy(m => m.RosterName)
                .Select(g => new RosterInfo(g.Key, g.Count().ToString()))
                .ToList();

            RosterGrid.ItemsSource = rosters;
        }
        catch (Exception ex)
        {
            fluid_general.Utils.AppEnv.LogError(ex);
        }
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await LoadRosterFilesAsync();
    }

    private async void ImportRoster_Click(object sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "名簿ファイルを開く",
            FileTypeFilter = new[] { new FilePickerFileType("Excel Files") { Patterns = new[] { "*.xlsx" } } }
        });

        if (files.Count > 0)
        {
            try
            {
                var file = files[0];
                string selectedFilePath = file.Path.LocalPath;
                string initialRosterName = Path.GetFileNameWithoutExtension(selectedFilePath);

                var service = App.GetDataService();
                var existingConfig = await service.GetRosterConfigAsync(initialRosterName);

                var mainWindow = topLevel as Window;
                var dialog = new RosterDialog(initialRosterName, existingConfig?.Mappings);
                await dialog.ShowDialog(mainWindow!);

                if (!dialog.IsSaved) return;

                string rosterName = dialog.RosterName;
                var mappings = dialog.Mappings.ToList();

                await service.UpdateRosterConfigAsync(new RosterConfig 
                { 
                    RosterName = rosterName, 
                    Mappings = mappings 
                });

                ImportProgressBar.IsVisible = true;
                ImportProgressBar.Value = 0;

                await Task.Run(async () => {
                    using (var fs = new FileStream(selectedFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var workbook = new XLWorkbook(fs))
                    {
                        var worksheet = workbook.Worksheets.First();
                        var rows = worksheet.RowsUsed().Skip(1).ToList();

                        var idMapping = mappings.FirstOrDefault(m => m.Label == "ID");
                        var snMapping = mappings.FirstOrDefault(m => m.Label == "学籍番号");
                        var nameMapping = mappings.FirstOrDefault(m => m.Label == "名前");
                        var kanaMapping = mappings.FirstOrDefault(m => m.Label == "名前（かな）");
                        var customMappings = mappings.Where(m => m.Label != "ID" && m.Label != "学籍番号" && m.Label != "名前" && m.Label != "名前（かな）").ToList();

                        int count = 0;
                        foreach (var row in rows)
                        {
                            var member = new Member { RosterName = rosterName };
                            if (idMapping != null)
                            {
                                var cell = row.Cell(idMapping.ColumnIndex);
                                if (!cell.IsEmpty())
                                {
                                    if (cell.DataType == XLDataType.Number) member.ExcelId = (int)cell.GetDouble();
                                    else
                                    {
                                        string idStr = cell.GetValue<string>();
                                        if (int.TryParse(idStr, out int id)) member.ExcelId = id;
                                        else if (double.TryParse(idStr, out double dId)) member.ExcelId = (int)dId;
                                    }
                                }
                            }
                            if (snMapping != null) member.StudentNumber = row.Cell(snMapping.ColumnIndex).GetValue<string>();
                            if (nameMapping != null) member.Name = row.Cell(nameMapping.ColumnIndex).GetValue<string>();
                            if (kanaMapping != null) member.Kana = row.Cell(kanaMapping.ColumnIndex).GetValue<string>();
                            foreach (var cm in customMappings)
                            {
                                member.CustomFields[cm.Label] = row.Cell(cm.ColumnIndex).GetValue<string>();
                            }
                            await service.CreateMemberAsync(member);
                            count++;
                            _ = Dispatcher.UIThread.InvokeAsync(() => {
                                ImportProgressBar.Value = (double)count / rows.Count * 100;
                            });
                        }
                    }
                });

                ImportProgressBar.IsVisible = false;
                await LoadRosterFilesAsync();
            }
            catch (Exception ex)
            {
                fluid_general.Utils.AppEnv.LogError(ex);
            }
        }
    }

    private async void RosterOptionClick(object? sender, RoutedEventArgs e)
    {
        if (RosterGrid.SelectedItem is RosterInfo selected)
        {
            var service = App.GetDataService();
            var config = await service.GetRosterConfigAsync(selected.RosterName);
            
            var topLevel = TopLevel.GetTopLevel(this);
            var dialog = new RosterDialog(selected.RosterName, config?.Mappings);
            await dialog.ShowDialog(topLevel as Window ?? throw new InvalidOperationException());

            if (dialog.IsSaved)
            {
                if (selected.RosterName != dialog.RosterName)
                {
                    try
                    {
                        var members = await service.GetMembersByRosterAsync(selected.RosterName);
                        foreach (var member in members)
                        {
                            member.RosterName = dialog.RosterName;
                            await service.UpdateMemberAsync(member);
                        }
                        await LoadRosterFilesAsync();
                    }
                    catch (Exception ex)
                    {
                        fluid_general.Utils.AppEnv.LogError(ex);
                    }
                }
            }
        }
    }

    private async void DeleteSelectedItem(object? sender, RoutedEventArgs e)
    {
        if (RosterGrid.SelectedItem is RosterInfo selected)
        {
            try
            {
                var service = App.GetDataService();
                var members = await service.GetMembersByRosterAsync(selected.RosterName);
                foreach (var member in members)
                {
                    await service.DeleteMemberAsync(member.RosterName, member.ExcelId);
                }
                await LoadRosterFilesAsync();
            }
            catch (Exception ex)
            {
                fluid_general.Utils.AppEnv.LogError(ex);
            }
        }
    }

    private void RosterGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (RosterGrid.SelectedItem is RosterInfo selected)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            var detailWindow = new RosterDetailWindow(selected.RosterName);
            detailWindow.ShowDialog(topLevel as Window ?? throw new InvalidOperationException());
        }
    }
}
