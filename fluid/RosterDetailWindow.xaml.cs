using fluid_general.Models;
using ModernWpf.Controls;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace fluid_general
{
    public partial class RosterDetailWindow : Window
    {
        private string _rosterName;

        public RosterDetailWindow(string rosterName)
        {
            InitializeComponent();
            _rosterName = rosterName;
            RosterNameTextBlock.Text = $"名簿: {rosterName}";
            Loaded += RosterDetailWindow_Loaded;
        }

        private async void RosterDetailWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadMembersAsync();
        }

        private async Task LoadMembersAsync()
        {
            try
            {
                var service = App.GetDataService();
                var config = await service.GetRosterConfigAsync(_rosterName);
                var members = await service.GetMembersByRosterAsync(_rosterName);

                MembersDataGrid.Columns.Clear();

                if (config != null && config.Mappings != null)
                {
                    foreach (var mapping in config.Mappings)
                    {
                        System.Windows.Data.Binding binding;
                        if (mapping.Label == "ID") binding = new System.Windows.Data.Binding("ExcelId");
                        else if (mapping.Label == "学籍番号") binding = new System.Windows.Data.Binding("StudentNumber");
                        else if (mapping.Label == "名前") binding = new System.Windows.Data.Binding("Name");
                        else if (mapping.Label == "名前（かな）") binding = new System.Windows.Data.Binding("Kana");
                        else
                        {
                            binding = new System.Windows.Data.Binding($"CustomFields[{mapping.Label}]");
                        }

                        MembersDataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
                        {
                            Header = mapping.Label,
                            Binding = binding,
                            IsReadOnly = mapping.Label == "ID" || mapping.Label == "学籍番号"
                        });
                    }
                }
                else
                {
                    MembersDataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "ID", Binding = new System.Windows.Data.Binding("ExcelId"), IsReadOnly = true });
                    MembersDataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "学籍番号", Binding = new System.Windows.Data.Binding("StudentNumber"), IsReadOnly = true });
                    MembersDataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "名前", Binding = new System.Windows.Data.Binding("Name") });
                    MembersDataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "名前（かな）", Binding = new System.Windows.Data.Binding("Kana") });
                }

                MembersDataGrid.ItemsSource = members;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"名簿の読み込みに失敗しました: {ex.Message}");
            }
        }

        private async void MembersDataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                if (e.Row.Item is Member member)
                {
                    await Task.Delay(100);
                    try
                    {
                        var service = App.GetDataService();
                        await service.UpdateMemberAsync(member);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"更新に失敗しました: {ex.Message}");
                    }
                }
            }
        }

        private async void DeleteSelectedMember(object sender, RoutedEventArgs e)
        {
            if (MembersDataGrid.SelectedItem is Member member)
            {
                ContentDialog deleteDialog = new ContentDialog
                {
                    Title = "削除",
                    Content = $"本当にメンバー '{member.Name}' を削除しますか？",
                    PrimaryButtonText = "削除",
                    CloseButtonText = "キャンセル"
                };

                // Because this is a separate window, we need to set the XamlRoot if using WinUI's ContentDialog inside a WPF Window
                // Or we can use MessageBox for simplicity here, since ContentDialog might have issues in a non-MainWindow without XamlRoot set.
                // Let's use MessageBox since it's safer in a generic secondary WPF Window with ModernWPF.
                var result = MessageBox.Show($"本当にメンバー '{member.Name}' を削除しますか？", "削除", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var service = App.GetDataService();
                        await service.DeleteMemberAsync(member.RosterName, member.ExcelId);
                        await LoadMembersAsync();
                        
                        // We also need a way to tell the parent RosterPage that roster size changed,
                        // but since the parent page reloads it from DB when navigated, it's fine.
                        // Or we can just set DialogResult if we open it as dialog.
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"削除失敗: {ex.Message}");
                    }
                }
            }
        }
    }
}
