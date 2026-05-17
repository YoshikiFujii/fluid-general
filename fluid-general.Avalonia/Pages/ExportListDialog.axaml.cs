using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ClosedXML.Excel;
using fluid_general.Models;
using fluid_general.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace fluid_general.Avalonia.Pages;

public class ColumnItemVM : INotifyPropertyChanged
{
    private string _label = "";
    private bool _isChecked;

    public string Label { get => _label; set { _label = value; OnPropertyChanged(nameof(Label)); } }
    public bool IsChecked { get => _isChecked; set { _isChecked = value; OnPropertyChanged(nameof(IsChecked)); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class ExportListDialog : Window
{
    private readonly EventConfig _event;
    public ObservableCollection<ColumnItemVM> ExportColumns { get; } = new();

    public ExportListDialog()
    {
        InitializeComponent();
        _event = new EventConfig();
    }

    public ExportListDialog(EventConfig eventConfig)
    {
        InitializeComponent();
        _event = eventConfig;
        _ = LoadColumnsAsync();
    }

    private async Task LoadColumnsAsync()
    {
        try
        {
            var service = App.GetDataService();
            var config = await service.GetRosterConfigAsync(_event.RosterName);
            
            ExportColumns.Add(new ColumnItemVM { Label = "名前", IsChecked = true });
            ExportColumns.Add(new ColumnItemVM { Label = "かな", IsChecked = true });
            ExportColumns.Add(new ColumnItemVM { Label = "学籍番号", IsChecked = true });

            if (config?.Mappings != null)
            {
                foreach (var mapping in config.Mappings)
                {
                    if (new[] { "ID", "名前", "かな", "名前（かな）", "学籍番号" }.Contains(mapping.Label)) continue;
                    ExportColumns.Add(new ColumnItemVM { Label = mapping.Label, IsChecked = true });
                }
            }
            ColumnsListBox.ItemsSource = ExportColumns;
        }
        catch (Exception ex) { AppEnv.LogError(ex); }
    }

    private async void OnExportClick(object sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "エクスポート先を選択",
            SuggestedFileName = $"{_event.EventName}.xlsx",
            DefaultExtension = "xlsx",
            FileTypeChoices = new[] { new FilePickerFileType("Excel Files") { Patterns = new[] { "*.xlsx" } } }
        });

        if (file == null) return;

        try
        {
            string path = file.Path.LocalPath;
            var service = App.GetDataService();
            var members = await service.GetMembersByRosterAsync(_event.RosterName);
            var logs = await service.GetCheckInLogsAsync(_event.Id);

            bool showReg = CheckedCheckBox.IsChecked == true;
            bool showUnreg = UncheckedCheckBox.IsChecked == true;
            bool showAbs = AbsentCheckBox.IsChecked == true;

            var filtered = members.Where(m =>
            {
                var log = logs.FirstOrDefault(l => l.ExcelId == m.ExcelId);
                string status = log?.Status ?? "未参加";
                return (showReg && status == "参加済み") || (showUnreg && status == "未参加") || (showAbs && status == "不参加");
            }).ToList();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("参加者リスト");
            
            var selectedCols = ExportColumns.Where(c => c.IsChecked).ToList();
            int col = 1;
            foreach (var c in selectedCols) ws.Cell(1, col++).Value = c.Label;
            ws.Cell(1, col).Value = "状態";

            int row = 2;
            foreach (var m in filtered)
            {
                col = 1;
                var log = logs.FirstOrDefault(l => l.ExcelId == m.ExcelId);
                string status = log?.Status ?? "未参加";

                foreach (var c in selectedCols)
                {
                    string val = c.Label switch
                    {
                        "名前" => m.Name,
                        "かな" => m.Kana,
                        "学籍番号" => m.StudentNumber,
                        _ => m.CustomFields.GetValueOrDefault(c.Label, "")
                    };
                    ws.Cell(row, col++).Value = val;
                }
                ws.Cell(row, col).Value = status;
                row++;
            }

            if (DetailCheckBox.IsChecked == true)
            {
                var ws2 = workbook.Worksheets.Add("集計情報");
                ws2.Cell(1, 1).Value = "項目";
                ws2.Cell(1, 2).Value = "人数";
                
                ws2.Cell(2, 1).Value = "総数";
                ws2.Cell(2, 2).Value = members.Count;
                ws2.Cell(3, 1).Value = "参加済み";
                ws2.Cell(3, 2).Value = logs.Count(l => l.Status == "参加済み");
                ws2.Cell(4, 1).Value = "不参加";
                ws2.Cell(4, 2).Value = logs.Count(l => l.Status == "不参加");
            }

            ws.Columns().AdjustToContents();
            workbook.SaveAs(path);
            Close();
        }
        catch (Exception ex) { AppEnv.LogError(ex); }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();
}
