using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using fluid_general.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace fluid_general.Avalonia.Pages;

public partial class RosterDetailWindow : Window
{
    private readonly string _rosterName;

    public RosterDetailWindow()
    {
        InitializeComponent();
        _rosterName = string.Empty;
    }

    public RosterDetailWindow(string rosterName)
    {
        InitializeComponent();
        _rosterName = rosterName;
        RosterNameTextBlock.Text = $"名簿: {rosterName}";
        
        _ = LoadMembersAsync();
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
                    Binding binding;
                    if (mapping.Label == "ID") binding = new Binding("ExcelId");
                    else if (mapping.Label == "学籍番号") binding = new Binding("StudentNumber");
                    else if (mapping.Label == "名前") binding = new Binding("Name");
                    else if (mapping.Label == "名前（かな）") binding = new Binding("Kana");
                    else
                    {
                        binding = new Binding($"CustomFields[{mapping.Label}]");
                    }

                    MembersDataGrid.Columns.Add(new DataGridTextColumn
                    {
                        Header = mapping.Label,
                        Binding = binding,
                        IsReadOnly = mapping.Label == "ID" || mapping.Label == "学籍番号",
                        Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                    });
                }
            }
            else
            {
                // デフォルト列
                MembersDataGrid.Columns.Add(new DataGridTextColumn { Header = "ID", Binding = new Binding("ExcelId"), IsReadOnly = true });
                MembersDataGrid.Columns.Add(new DataGridTextColumn { Header = "学籍番号", Binding = new Binding("StudentNumber"), IsReadOnly = true });
                MembersDataGrid.Columns.Add(new DataGridTextColumn { Header = "名前", Binding = new Binding("Name") });
                MembersDataGrid.Columns.Add(new DataGridTextColumn { Header = "名前（かな）", Binding = new Binding("Kana") });
            }

            MembersDataGrid.ItemsSource = members;
        }
        catch (Exception ex)
        {
            fluid_general.Utils.AppEnv.LogError(ex);
        }
    }

    private async void DeleteSelectedMember(object? sender, RoutedEventArgs e)
    {
        if (MembersDataGrid.SelectedItem is Member member)
        {
            try
            {
                var service = App.GetDataService();
                await service.DeleteMemberAsync(member.RosterName, member.ExcelId);
                await LoadMembersAsync();
            }
            catch (Exception ex)
            {
                fluid_general.Utils.AppEnv.LogError(ex);
            }
        }
    }
}
