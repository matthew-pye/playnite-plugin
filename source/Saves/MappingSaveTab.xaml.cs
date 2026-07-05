using Graviton.Models;
using Graviton.Models.Notifications;

using Playnite;

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Graviton.Saves
{
    public partial class MappingSaveTab : UserControl
    {
        private GravitonPlugin _plugin => GravitonPlugin.Instance;

        public EmulatorMapping? Mapping { get; private set; }
        public ObservableCollection<SaveRow> LocalSaveRow { get; } = new();
        public ObservableCollection<SaveRow> RemoteSaveRow { get; } = new();

        public MappingSaveTab()
        {
            InitializeComponent();
            RowsList.ItemsSource = LocalSaveRow;
            RemoteRowsList.ItemsSource = RemoteSaveRow;

            RefreshText.Text = $"\uf46a {Loc.GetString("Refresh")}";
            RefreshText.FontFamily = Playnite.Fonts.NerdFont;
            EnableAllText.Text = Loc.GetString("EnableAllSaves");

            LocalTitleText.Text = Loc.GetString("LocalSaves");
            RemoteTitleText.Text = Loc.GetString("RemoteSaves");

            if (Mapping != null)
                Load(Mapping);
        }

        public async void Load(EmulatorMapping mapping)
        {
            Mapping = mapping;
            TitleText.Text = $"{Loc.GetString("SaveManagerTitle")} — {mapping.MappingName}";

            var rows = await _plugin.SaveController!.GetLocalSaves(mapping);
            if (rows != null)
                LocalSaveRow.AddRangeIfNotNull(rows);

            rows = await _plugin.SaveController!.GetRemoteSaves(mapping);
            if (rows != null)
            {
                RemoteSaveRow.AddRangeIfNotNull(rows.Where(x => !LocalSaveRow.Any(y => y.SaveID == x.SaveID)));
            }
                
            EmptyStateText.Visibility = LocalSaveRow.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyRemoteText.Visibility = RemoteSaveRow.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (Mapping != null) 
                Load(Mapping);
        }

        private void EnableAllMatched_Click(object sender, RoutedEventArgs e)
        {
            // TODO: persist the enabled set via SaveController
        }

        private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var filter = FilterBox.Text?.Trim().ToLowerInvariant() ?? "";
            RowsList.ItemsSource = string.IsNullOrEmpty(filter) ? LocalSaveRow : new ObservableCollection<SaveRow>(System.Linq.Enumerable.Where(LocalSaveRow, r => r.GameName.ToLowerInvariant().Contains(filter)));
        }

        private void AddSave_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SyncSave_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void SaveSyncEnabled_Checked(object sender, RoutedEventArgs e)
        {
            if(string.IsNullOrEmpty(Mapping!.SavePath))
            {
                GravitonNotify.Add(new GravitonNotification("graviton.mapping.save.failed", Loc.GetString("NoSavePathSet"), GravitonSeverity.Error));
                ((CheckBox)sender).IsChecked = false;
            }
            else
            {
                var row = ((FrameworkElement)sender).DataContext as SaveRow;
                if (row != null && Mapping != null)
                {
                    var result = await GravitonPlugin.PlayniteApi.Dialogs.ShowMessageAsync($"Do you want to save '{row.SaveDirectoryView[0].Name}' in '{Mapping.SavePath}'?", "Download Save?", MessageBoxButtons.YesNo, MessageBoxSeverity.Question);
                    if (result == Playnite.MessageBoxResult.Yes)
                    {
                        // Download Save to location
                    }
                    else
                    {
                        ((CheckBox)sender).IsChecked = false;
                    }
                }
            }

            e.Handled = true;
        }
    }
}