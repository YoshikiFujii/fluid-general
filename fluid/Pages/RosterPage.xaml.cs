using ClosedXML.Excel;
using Microsoft.Win32;
using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using fluid_general.Models;

namespace fluid_general.Pages
{
    public partial class RosterPage : System.Windows.Controls.Page
    {
        public RosterPage()
        {
            InitializeComponent();
            LoadRosterFiles();
        }

        private async void ImportRoster_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx",
                Title = "名簿ファイルを開く"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string selectedFilePath = openFileDialog.FileName;
                    string initialRosterName = System.IO.Path.GetFileNameWithoutExtension(selectedFilePath);
                    
                    var service = App.GetDataService();
                    var existingConfig = await service.GetRosterConfigAsync(initialRosterName);
                    
                    var dialog = new RosterDialog(initialRosterName, existingConfig?.Mappings);
                    var result = await dialog.ShowAsync();

                    if (result != ContentDialogResult.Primary) return;

                    string rosterName = dialog.rostername;
                    var mappings = dialog.Mappings.ToList();

                    // 名簿構成を保存
                    await service.UpdateRosterConfigAsync(new Models.RosterConfig 
                    { 
                        RosterName = rosterName, 
                        Mappings = mappings 
                    });

                    using (var fs = new FileStream(selectedFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var workbook = new XLWorkbook(fs))
                    {
                        var worksheet = workbook.Worksheets.First();
                        var rows = worksheet.RowsUsed().Skip(1).ToList();

                        progressBar.Maximum = rows.Count;
                        progressBar.Visibility = Visibility.Visible;
                        int progress = 0;

                        var idMapping = mappings.FirstOrDefault(m => m.Label == "ID");
                        var snMapping = mappings.FirstOrDefault(m => m.Label == "学籍番号");
                        var nameMapping = mappings.FirstOrDefault(m => m.Label == "名前");
                        var kanaMapping = mappings.FirstOrDefault(m => m.Label == "名前（かな）");
                        var customMappings = mappings.Where(m => m.Label != "ID" && m.Label != "学籍番号" && m.Label != "名前" && m.Label != "名前（かな）").ToList();

                        foreach (var row in rows)
                        {
                            var member = new Models.Member { RosterName = rosterName };
                            
                            if (idMapping != null)
                            {
                                var cell = row.Cell(idMapping.ColumnIndex);
                                if (!cell.IsEmpty())
                                {
                                    // 数値として取得を試みる
                                    if (cell.DataType == XLDataType.Number)
                                    {
                                        member.ExcelId = (int)cell.GetDouble();
                                    }
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
                            progress++;
                            progressBar.Value = progress;
                        }
                    }

                    progressBar.Visibility = Visibility.Collapsed;
                    MessageBox.Show("名簿のインポートが完了しました。");
                    LoadRosterFiles();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"エラーが発生しました: {ex.Message}");
                }
            }
        }

        private async void LoadRosterFiles()
        {
            try
            {
                var service = App.GetDataService();
                var allMembers = await service.GetMembersAsync();
                
                var rosters = allMembers
                    .Where(m => !string.IsNullOrEmpty(m.RosterName))
                    .GroupBy(m => m.RosterName)
                    .Select(g => new
                    {
                        RosterName = g.Key,
                        TotalCount = g.Count().ToString()
                    })
                    .ToList();

                RosterDataGrid.ItemsSource = rosters;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"名簿の読み込みに失敗しました: {ex.Message}");
            }
        }

        private void RosterOptionClick(object sender, RoutedEventArgs e)
        {
            if (RosterDataGrid.SelectedItem != null)
            {
                dynamic selectedItem = RosterDataGrid.SelectedItem;
                string selectedRosterName = selectedItem.RosterName;
                RosterOption(selectedRosterName);
            }
        }

        private async void RosterOption(string selectedRosterName)
        {
            var service = App.GetDataService();
            var config = await service.GetRosterConfigAsync(selectedRosterName);
            var dialog = new RosterDialog(selectedRosterName, config?.Mappings);
            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                string newRosterName = dialog.rostername;
                if (selectedRosterName != newRosterName)
                {
                    try
                    {
                        var members = await service.GetMembersByRosterAsync(selectedRosterName);
                        foreach (var member in members)
                        {
                            member.RosterName = newRosterName;
                            await service.UpdateMemberAsync(member);
                        }
                        MessageBox.Show("名簿名を更新しました。");
                        LoadRosterFiles();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"更新に失敗しました: {ex.Message}");
                    }
                }
            }
        }

        private async void DeleteSelectedItem(object sender, RoutedEventArgs e)
        {
            if (RosterDataGrid.SelectedItem != null)
            {
                dynamic selectedItem = RosterDataGrid.SelectedItem;
                string rosterName = selectedItem.RosterName;

                ContentDialog deleteDialog = new ContentDialog
                {
                    Title = "削除",
                    Content = $"本当に名簿 '{rosterName}' を削除しますか？\n(紐付いているメンバー全員が削除されます)",
                    PrimaryButtonText = "削除",
                    CloseButtonText = "キャンセル"
                };

                if (await deleteDialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    try
                    {
                        var service = App.GetDataService();
                        var members = await service.GetMembersByRosterAsync(rosterName);
                        foreach (var member in members)
                        {
                            await service.DeleteMemberAsync(member.RosterName, member.ExcelId);
                        }
                        LoadRosterFiles();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"削除失敗: {ex.Message}");
                    }
                }
            }
        }

        private void RosterDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (RosterDataGrid.SelectedItem != null)
            {
                dynamic selectedItem = RosterDataGrid.SelectedItem;
                string rosterName = selectedItem.RosterName;

                var detailWindow = new RosterDetailWindow(rosterName);
                detailWindow.Owner = Window.GetWindow(this);
                detailWindow.ShowDialog();

                // メンバー数などが変わった可能性があるためリロードする
                LoadRosterFiles();
            }
        }
    }
}
