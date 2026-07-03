using Graviton.Models;

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
        public ObservableCollection<SaveRow> Rows { get; } = new();

        public MappingSaveTab()
        {
            InitializeComponent();
            RowsList.ItemsSource = Rows;

            RefreshText.Text = $"\uf46a {Loc.GetString("Refresh")}";
            EnableAllText.Text = Loc.GetString("EnableAllMatched");
        }

        public async void Load(EmulatorMapping mapping)
        {
            Mapping = mapping;
            TitleText.Text = $"{Loc.GetString("SaveManagerTitle")} — {mapping.MappingName}";

            var rows = await _plugin.SaveController!.GetLocalSaves(mapping);
            if (rows != null)
                Rows.AddRangeIfNotNull(rows);



            EmptyStateText.Visibility = Rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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
            RowsList.ItemsSource = string.IsNullOrEmpty(filter) ? Rows : new ObservableCollection<SaveRow>(System.Linq.Enumerable.Where(Rows, r => r.GameName.ToLowerInvariant().Contains(filter)));
        }

        private void AddSave_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SyncSave_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}