using Graviton.Models;
using Graviton.Models.Notifications;
using Graviton.Models.RomM.Rom;
using Graviton.Models.RomM.Saves;
using Graviton.Models.Saves;

using Playnite;

using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace Graviton.Saves
{
    public partial class SaveManagementView : UserControl
    {
        public static readonly RoutedEvent BackRequestedEvent = EventManager.RegisterRoutedEvent("BackRequested", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SaveManagementView));

        public event RoutedEventHandler BackRequested
        {
            add => AddHandler(BackRequestedEvent, value);
            remove => RemoveHandler(BackRequestedEvent, value);
        }

        private GravitonPlugin _plugin => GravitonPlugin.Instance;

        EmulatorMapping? Mapping { get; set; }
        List<RomMRomLocal>? ROMs { get; set; }
        List<GravitonSave> Saves { get; set; } = new();

        MessageBoxResponse RestoreLocally;
        MessageBoxResponse FullRestore;
        MessageBoxResponse Cancel;

        private void Back_Click(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(BackRequestedEvent));


        public SaveManagementView()
        {
            InitializeComponent();

            RestoreLocally = new("Restore Locally Only");
            FullRestore = new("Restore & Sync", isDefault: true);
            Cancel = new("Cancel", isCancel: true);

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

            List<GravitonSave>? saves;// = TestSaves();

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

        private async void SyncSave_Click(object sender, RoutedEventArgs e)
        {
            var save = ((FrameworkElement)sender).DataContext as GravitonSave;
            if(save == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.save.null", "Save is null, skipping", GravitonSeverity.Error));
                e.Handled = true;
                return;
            }

            if(save.Status == SaveStatus.Conflicted)
            {
                var result = SaveManager.ResolveConflict(save);
                switch (result)
                {
                    case SaveSyncStatus.upload:
                        save.Status = SaveStatus.LocalNewer;
                        break;

                    case SaveSyncStatus.download:
                        save.Status = SaveStatus.RemoteNewer;
                        break;

                    case SaveSyncStatus.conflict:
                        save.Status = SaveStatus.Conflicted;
                        break;

                    case SaveSyncStatus.no_op:
                        save.Status = SaveStatus.Synced;
                        break;

                }
            }

            int saveindex = Saves.IndexOf(save);

            switch (save.Status)
            {
                case SaveStatus.Synced:
                    GravitonNotify.Add(new GravitonNotification("graviton.save.alreadysynced", "Save is already in sync!", GravitonSeverity.Info));
                    break;

                case SaveStatus.LocalNewer:
                    save = await SaveManager.Upload(save);
                    Saves[saveindex] = save;
                    SavesItemControl.ItemsSource = Saves.Where(x => x.GameName.Contains(SaveFilterBox.Text, StringComparison.OrdinalIgnoreCase));
                    e.Handled = true;
                    return;

                case SaveStatus.RemoteNewer:
                    save = await SaveManager.Download(save);
                    Saves[saveindex] = save;
                    SavesItemControl.ItemsSource = Saves.Where(x => x.GameName.Contains(SaveFilterBox.Text, StringComparison.OrdinalIgnoreCase));
                    e.Handled = true;
                    return;

                case SaveStatus.ServerOnly:
                    save = await SaveManager.TrackNewRemoteSave(save);
                    Saves[saveindex] = save;
                    SavesItemControl.ItemsSource = Saves.Where(x => x.GameName.Contains(SaveFilterBox.Text, StringComparison.OrdinalIgnoreCase));
                    e.Handled = true;
                    return;

                case SaveStatus.UntrackedLocal:
                    save = await SaveManager.TrackNewLocalSave(save);
                    Saves[saveindex] = save;
                    SavesItemControl.ItemsSource = Saves.Where(x => x.GameName.Contains(SaveFilterBox.Text, StringComparison.OrdinalIgnoreCase));
                    e.Handled = true;
                    return;

                default:
                    GravitonNotify.Add(new GravitonNotification("graviton.save.unknown", "Save status is unknown!, skipping", GravitonSeverity.Warn));
                    break;
            }
        }

        private async void RevertToHistoricSave_Click(object sender, RoutedEventArgs e)
        {
            var save = ((FrameworkElement)sender).DataContext as GravitonSave;
            if (save == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.save.null", "Save is null, skipping", GravitonSeverity.Error));
                e.Handled = true;
                return;
            }

            var parentROM = _plugin.ImportedGames!.FirstOrDefault(x => x.Value.LocalSave.HistoricSaves != null && x.Value.LocalSave.HistoricSaves.Any(y => y.LocalID == save.LocalID));
            if(parentROM.Value == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.parentsave.null", "Failed to find parent save, skipping", GravitonSeverity.Error));
                e.Handled = true;
                return;
            }

            var response = await GravitonPlugin.PlayniteApi.Dialogs.ShowMessageAsync("How do you want to restore the historic save?", "Restore Historic Save", MessageBoxSeverity.Question, new List<MessageBoxResponse> { RestoreLocally, FullRestore, Cancel}, new List<MessageBoxOption>());

            if(response == FullRestore)
            {
                Saves.Remove(parentROM.Value.LocalSave);
                var result = await SaveManager.Download(save, true);
                result = await SaveManager.Upload(save);
                Saves.Add(parentROM.Value.LocalSave);
            }

            if(response == RestoreLocally)
            {
                Saves.Remove(parentROM.Value.LocalSave);
                var result = await SaveManager.Download(save, true);

                if (parentROM.Value.LocalSave.HistoricSaves == null)
                    parentROM.Value.LocalSave.HistoricSaves = new();

                var savecopy = JsonSerializer.Deserialize<GravitonSave>(JsonSerializer.Serialize(parentROM.Value.LocalSave));
                if (savecopy != null)
                {
                    savecopy.IsCurrent = false;
                    savecopy.HistoricSaves = null;
                    parentROM.Value.LocalSave.HistoricSaves.Add(savecopy);
                }

                result.HistoricSaves = parentROM.Value.LocalSave.HistoricSaves.OrderByDescending(x => x.LastSyncedAt).ToObservableCollection();
                result.IsCurrent = true;
                result.IsTempRestored = true;
                parentROM.Value.LocalSave = result;
                parentROM.Value.Save();

                Saves.Add(parentROM.Value.LocalSave);
            }

        }

        List<GravitonSave> TestSaves()
        {
            return new()
            {
                new()
                {
                    GameName = "Super Mario Bros.",
                    Slot = "Autosave",
                    Status = SaveStatus.ServerOnly,
                    FileSize = 10424
                },
                new()
                {
                    GameName = "Xenoblade Chronicles",
                    Slot = "Autosave",
                    Status = SaveStatus.LocalNewer,
                    FileSize = 32464354,
                    LastSyncedAt = (DateTime.Now).AddSeconds(-46),
                    HistoricSaves = new()
                    {
                        new()
                        {
                             GameName = "Xenoblade Chronicles",
                             Slot = "Autosave",
                             LastSyncedAt = ((DateTime.Now).AddHours(-10)).AddMinutes(-11),
                             FileSize = 32424254,
                        },
                        new()
                        {
                             GameName = "Xenoblade Chronicles",
                             Slot = "Autosave",
                             LastSyncedAt = ((DateTime.Now).AddHours(-15)).AddMinutes(-50),
                             FileSize = 32361354,
                        },
                        new()
                        {
                             GameName = "Xenoblade Chronicles",
                             Slot = "Autosave",
                             LastSyncedAt = ((DateTime.Now).AddHours(-25)).AddMinutes(-23),
                             FileSize = 32228354,
                        },
                        new()
                        {
                             GameName = "Xenoblade Chronicles",
                             Slot = "Autosave",
                             LastSyncedAt = ((DateTime.Now).AddHours(-40)).AddMinutes(-2),
                             FileSize = 32121354,
                        },
                        new()
                        {
                             GameName = "Xenoblade Chronicles",
                             Slot = "Autosave",
                             LastSyncedAt = ((DateTime.Now).AddHours(-86)).AddMinutes(-44),
                             FileSize = 28881354,
                        }
                    }

                },
                new()
                {
                    GameName = "Pokemon Red.",
                    Slot = "Auto",
                    Status = SaveStatus.RemoteNewer,
                    LastSyncedAt = ((DateTime.Now).AddHours(-2)).AddMinutes(-31),
                    FileSize = 64354,
                },
                new()
                {
                    GameName = "Metroid",
                    Slot = "Test",
                    Status = SaveStatus.Conflicted,
                    LastSyncedAt = (DateTime.Now).AddMinutes(-52),
                    FileSize = 324,
                },
                new()
                {
                    GameName = "Kingdom Hearts: Final Mix",
                    Slot = "Autosave",
                    Status = SaveStatus.ServerOnly,
                    FileSize = 9146574,
                },
                new()
                {
                    GameName = "The Legend Of Zelda: Link to the Past",
                    Slot = "Autosave",
                    Status = SaveStatus.Synced,
                    LastSyncedAt = (DateTime.Now).AddHours(-26),
                    FileSize = 43633,
                },
            };
        }

        
    }
}