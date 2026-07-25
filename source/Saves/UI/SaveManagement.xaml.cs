using Graviton.Models;
using Graviton.Models.RomM.Rom;
using Graviton.Models.Saves;

using Playnite;

using System.Windows;
using System.Windows.Controls;

namespace Graviton.Saves
{
    public partial class SaveManagementView : UserControl
    {
        private GravitonPlugin _plugin => GravitonPlugin.Instance;

        EmulatorMapping? Mapping { get; set; }
        List<RomMRomLocal>? ROMs { get; set; }
        List<GravitonSave> Saves { get; set; } = new();

        public static readonly RoutedEvent BackRequestedEvent = EventManager.RegisterRoutedEvent("BackRequested", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SaveManagementView));

        public event RoutedEventHandler BackRequested
        {
            add => AddHandler(BackRequestedEvent, value);
            remove => RemoveHandler(BackRequestedEvent, value);
        }

        private void Back_Click(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(BackRequestedEvent));


        public SaveManagementView()
        {
            InitializeComponent();

            RefreshText.Text = $"\uf46a {Loc.GetString("Refresh")}";
            RefreshText.FontFamily = Playnite.Fonts.NerdFont;
        }

        public async Task Load(List<RomMRomLocal> roms)
        {
            ROMs = roms;
            await Load();
        }
        public async Task Load(EmulatorMapping mapping)
        {
            Mapping = mapping;
            BackButton.Visibility = Visibility.Visible;
            await Load();
        }
        public async Task Load()
        {
            Saves.Clear();
            SavesItemControl.ItemsSource = null;

            LoadingBar.Visibility = Visibility.Visible;
            NoSavesText.Visibility = Visibility.Collapsed;

            SyncedSavesCount.Text = "0";
            LocalNewerSavesCount.Text = "0";
            RemoteNewerSavesCount.Text = "0";
            ConflictedSavesCount.Text = "0";
            ServerOnlySavesCount.Text = "0";
            LocalOnlySavesCount.Text = "0";

            List<GravitonSave>? saves;

            if (ROMs != null)
            {
                saves = await SaveDiscovery.Discover(ROMs);
            }
            else if (Mapping != null)
            {
                saves = await SaveDiscovery.Discover(Mapping);
            }
            else
            {
                saves = await SaveDiscovery.Discover();
            }

            if (saves == null || saves.Count < 1)
            {
                LoadingBar.Visibility = Visibility.Collapsed;
                NoSavesText.Visibility = Visibility.Visible;
            }
            else
            {
                LoadingBar.Visibility = Visibility.Collapsed;
                Saves = saves;
                SavesItemControl.ItemsSource = Saves;

                SyncedSavesCount.Text = saves.Where(x => x.Status == SaveStatus.Synced).Count().ToString();
                LocalNewerSavesCount.Text = saves.Where(x => x.Status == SaveStatus.LocalNewer).Count().ToString();
                RemoteNewerSavesCount.Text = saves.Where(x => x.Status == SaveStatus.RemoteNewer).Count().ToString();
                ConflictedSavesCount.Text = saves.Where(x => x.Status == SaveStatus.Conflicted).Count().ToString();
                ServerOnlySavesCount.Text = saves.Where(x => x.Status == SaveStatus.ServerOnly).Count().ToString();
                LocalOnlySavesCount.Text = saves.Where(x => x.Status == SaveStatus.UntrackedLocal).Count().ToString();

            }

        }

        private void SaveFilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SavesItemControl.ItemsSource = Saves.Where(x => x.GameName.Contains(SaveFilterBox.Text, StringComparison.OrdinalIgnoreCase));
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            _ = Load();
        }

        private void Click_ShowSaveDetails(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is GravitonSave save)
            {
                save.IsExpanded = !save.IsExpanded;
            }
        }
    }
}