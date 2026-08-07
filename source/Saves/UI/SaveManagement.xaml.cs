using Graviton.Models;
using Graviton.Models.Notifications;
using Graviton.Models.RomM.Rom;
using Graviton.Models.RomM.Saves;
using Graviton.Models.Saves;

using Playnite;

using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
        private SaveController SaveController => GravitonPlugin.Instance.SaveController!;

        EmulatorMapping? Mapping { get; set; }
        List<RomMRomLocal>? ROMs { get; set; }
        List<GravitonSave> Saves { get; set; } = new();

        MessageBoxResponse RestoreLocally;
        MessageBoxResponse FullRestore;
        MessageBoxResponse Cancel;

        MessageBoxResponse UntrackSave;
        MessageBoxResponse DeleteSaveLocal;
        MessageBoxResponse DeleteSaveTotally;

        private void Back_Click(object sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(BackRequestedEvent));


        public SaveManagementView()
        {
            InitializeComponent();

            RestoreLocally = new(Loc.GetString("RestoreLocallyOnly"));
            FullRestore = new(Loc.GetString("RestoreAndSync"), isDefault: true);
            Cancel = new(Loc.GetString("Cancel"), isCancel: true);

            UntrackSave = new(Loc.GetString("UntrackSaveButton"), isDefault: true);
            DeleteSaveLocal = new(Loc.GetString("DeleteSaveLocal"));
            DeleteSaveTotally = new(Loc.GetString("DeleteSaveBoth"));

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
                saves = await SaveController.Discover.Discover(ROMs);
            }
            else if (Mapping != null)
            {
                saves = await SaveController.Discover.Discover(Mapping);
            }
            else
            {
                saves = await SaveController.Discover.Discover();
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
                SavesItemControl.ItemsSource = Saves.Where(x => x.GameName.Contains(SaveFilterBox.Text, StringComparison.OrdinalIgnoreCase)).OrderBy(y => y.GameName);

                SyncedSavesCount.Text = saves.Where(x => x.Status == SaveStatus.Synced).Count().ToString();
                LocalNewerSavesCount.Text = saves.Where(x => x.Status == SaveStatus.LocalNewer).Count().ToString();
                RemoteNewerSavesCount.Text = saves.Where(x => x.Status == SaveStatus.RemoteNewer).Count().ToString();
                ConflictedSavesCount.Text = saves.Where(x => x.Status == SaveStatus.Conflicted).Count().ToString();
                ServerOnlySavesCount.Text = saves.Where(x => x.Status == SaveStatus.ServerOnly).Count().ToString();
                LocalOnlySavesCount.Text = saves.Where(x => x.Status == SaveStatus.UntrackedLocal).Count().ToString();

            }

        }

        #region TopBar Controls
        private void SaveFilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SavesItemControl.ItemsSource = Saves.Where(x => x.GameName.Contains(SaveFilterBox.Text, StringComparison.OrdinalIgnoreCase)).OrderBy(y => y.GameName);
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            _ = Load();
        }

        private async void CreateNewSave_Click(object sender, RoutedEventArgs e)
        {
            var window = GravitonPlugin.PlayniteApi.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = false,
                ShowMaximizeButton = false,
                ShowCloseButton = true,
                DefaultWidth = 1280,
                DefaultHeight = 720
            });

            CreateSaveSelector saveSelector;

            if (ROMs != null)
            {
                saveSelector = new CreateSaveSelector(ROMs);
            }
            else if (Mapping != null)
            {
                var roms = _plugin.ImportedGames.Where(x => x.Value.MappingID == Mapping.MappingId);
                if (roms == null)
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.roms.null", Loc.GetString("NoROMsForMapping"), GravitonSeverity.Error));
                    e.Handled = true;
                    return;
                }

                saveSelector = new CreateSaveSelector(roms.Select(x => x.Value).ToList());
            }
            else
            {
                saveSelector = new CreateSaveSelector();
            }

            window.Title = Loc.GetString("CreateNewSave");
            window.Content = saveSelector;
            window.Owner = GravitonPlugin.PlayniteApi.GetLastActiveWindow();
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();

            if (saveSelector.WasConfirmed)
            {
                GravitonSave newsave = new()
                {
                    ROMID = saveSelector.SelectedROM!.Id,
                    GameName = saveSelector.SelectedROM!.Name!,
                    Filename = saveSelector.SelectedSourcePaths!.Count > 1 || !File.Exists(saveSelector.SelectedSourcePaths[0]) ? $"{saveSelector.SelectedROM.Name}.zip" : Path.GetFileName(saveSelector.SelectedSourcePaths[0]),
                    SourceFilePaths = saveSelector.SelectedSourcePaths!.Select(x => x.Replace(saveSelector.SelectedMapping!.SavePath, EmulatorMapping.SavePathToken)).ToObservableCollection(),
                    Status = SaveStatus.LocalNewer
                };

                newsave = await SaveController.Manager.TrackNewLocalSave(newsave);
                Saves.Add(newsave);
            }

            e.Handled = true;
        }

        private void TrackArchivedSave_Click(object sender, RoutedEventArgs e)
        {
            var window = GravitonPlugin.PlayniteApi.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = false,
                ShowMaximizeButton = false,
                ShowCloseButton = true,
                DefaultWidth = 1280,
                DefaultHeight = 720
            });

            ArchiveSaveSelector trackArchiveSave;

            if (ROMs != null)
            {
                trackArchiveSave = new ArchiveSaveSelector(ROMs);
            }
            else if (Mapping != null)
            {
                trackArchiveSave = new ArchiveSaveSelector(Mapping);
            }
            else
            {
                trackArchiveSave = new ArchiveSaveSelector();
            }

            window.Title = Loc.GetString("TrackArchivedSave");
            window.Content = trackArchiveSave;
            window.Owner = GravitonPlugin.PlayniteApi.GetLastActiveWindow();
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();

            if (trackArchiveSave.NewSave != null)
            {
                var oldsave = Saves.FirstOrDefault(x => x.ROMID == trackArchiveSave.NewSave.ROMID);
                if (oldsave != null)
                    Saves.Remove(oldsave);

                Saves.Add(trackArchiveSave.NewSave);
                SavesItemControl.ItemsSource = Saves.Where(x => x.GameName.Contains(SaveFilterBox.Text, StringComparison.OrdinalIgnoreCase)).OrderBy(y => y.GameName);
            }

            e.Handled = true;
        }
        #endregion

        #region Per-save Controls
        private void SaveRow_Click(object sender, MouseButtonEventArgs e)
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
                GravitonNotify.Add(new GravitonNotification("graviton.save.null", Loc.GetString("SaveIsNull"), GravitonSeverity.Error));
                e.Handled = true;
                return;
            }

            if(save.Status == SaveStatus.Conflicted)
            {
                var result = SaveController.Negotiator.ResolveConflict(save);
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
                    GravitonNotify.Add(new GravitonNotification("graviton.save.alreadysynced", Loc.GetString("SaveAlreadySynced"), GravitonSeverity.Info));
                    break;

                case SaveStatus.LocalNewer:
                    save = await SaveController.Manager.Upload(save);
                    Saves[saveindex] = save;
                    SavesItemControl.ItemsSource = Saves.Where(x => x.GameName.Contains(SaveFilterBox.Text, StringComparison.OrdinalIgnoreCase)).OrderBy(y => y.GameName);
                    e.Handled = true;
                    return;

                case SaveStatus.RemoteNewer:
                    save = await SaveController.Manager.Download(save);
                    Saves[saveindex] = save;
                    SavesItemControl.ItemsSource = Saves.Where(x => x.GameName.Contains(SaveFilterBox.Text, StringComparison.OrdinalIgnoreCase)).OrderBy(y => y.GameName);
                    e.Handled = true;
                    return;

                case SaveStatus.Conflicted:
                    GravitonNotify.Add(new GravitonNotification("graviton.save.conflicted", Loc.GetString("SyncStillConflicted", ("GameName", save.GameName!)), GravitonSeverity.Info));
                    e.Handled = true;
                    return;

                case SaveStatus.ServerOnly:
                    save = await SaveController.Manager.TrackNewRemoteSave(save);
                    Saves[saveindex] = save;
                    SavesItemControl.ItemsSource = Saves.Where(x => x.GameName.Contains(SaveFilterBox.Text, StringComparison.OrdinalIgnoreCase)).OrderBy(y => y.GameName);
                    e.Handled = true;
                    return;

                case SaveStatus.UntrackedLocal:
                    save = await SaveController.Manager.TrackNewLocalSave(save);
                    Saves[saveindex] = save;
                    SavesItemControl.ItemsSource = Saves.Where(x => x.GameName.Contains(SaveFilterBox.Text, StringComparison.OrdinalIgnoreCase)).OrderBy(y => y.GameName);
                    e.Handled = true;
                    return;

                case SaveStatus.TempRestored:
                    var response = await GravitonPlugin.PlayniteApi.Dialogs.ShowMessageAsync(Loc.GetString("UploadRestoredSaveConfirm"), Loc.GetString("UploadRestoredSaveTitle"), MessageBoxButtons.YesNoCancel);

                    if (response == Playnite.MessageBoxResult.Yes)
                    {
                        save = await SaveController.Manager.Upload(save);
                    }
                    e.Handled = true;
                    return;

                case SaveStatus.MissingFiles:
                    string pathsString = "\n";
                    foreach (var path in save.MissingFiles)
                    {
                        pathsString += $"\t{path}\n";
                    }
                    var msresponse = await GravitonPlugin.PlayniteApi.Dialogs.ShowMessageAsync(Loc.GetString("MissingFilesConfirm", ("Paths", pathsString)), Loc.GetString("RestoreHistoricSaveTitle"), MessageBoxButtons.YesNoCancel);

                    if(msresponse == Playnite.MessageBoxResult.Yes)
                    {
                        foreach (var path in save.MissingFiles)
                            save.SourceFilePaths.Remove(path);

                        save.Status = SaveStatus.LocalNewer;
                        save = await SaveController.Manager.Upload(save);
                    }

                    e.Handled = true;
                    return;

                default:
                    GravitonNotify.Add(new GravitonNotification("graviton.save.unknown", Loc.GetString("SaveStatusUnknownWarning"), GravitonSeverity.Warn));
                    break;
            }
        }

        private async void DeleteSave_Click(object sender, RoutedEventArgs e)
        {
            var save = ((FrameworkElement)sender).DataContext as GravitonSave;
            if (save == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.save.null", Loc.GetString("SaveIsNull"), GravitonSeverity.Error));
                e.Handled = true;
                return;
            }

            var rom = _plugin.ImportedGames.FirstOrDefault(x => x.Value.LocalSave?.LocalID == save.LocalID).Value;
            if (rom == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.rom.notfound", Loc.GetString("ROMNotFoundForSave"), GravitonSeverity.Error));
                e.Handled = true;
                return;
            }


            var response = await GravitonPlugin.PlayniteApi.Dialogs.ShowMessageAsync(Loc.GetString("DeleteSaveMessage"), Loc.GetString("DeleteSaveTitle"), MessageBoxSeverity.Question, new List<MessageBoxResponse> { UntrackSave, DeleteSaveLocal, DeleteSaveTotally, Cancel }, new List<MessageBoxOption>());

            if (response == UntrackSave)
            {
                Saves.Remove(save);
                await SaveController.Manager.UntrackSave(save.SaveID);
                rom.LocalSave = null;
                rom.Save();
            }

            if (response == DeleteSaveLocal)
            {
                // TODO - Add Deleting
            }

            if (response == DeleteSaveTotally)
            {
                // TODO - Add Deleting
            }

        }
        #endregion

        #region Per-Save Expanded Controls
        private async void RevertToHistoricSave_Click(object sender, RoutedEventArgs e)
        {
            var historicSave = ((FrameworkElement)sender).DataContext as GravitonSave;
            if (historicSave == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.save.null", Loc.GetString("SaveIsNull"), GravitonSeverity.Error));
                e.Handled = true;
                return;
            }

            var parentROM = _plugin.ImportedGames.FirstOrDefault(x => x.Value.LocalSave?.HistoricSaves != null && x.Value.LocalSave.HistoricSaves.Any(y => y.LocalID == historicSave.LocalID)).Value;
            if(parentROM == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.parentsave.null", Loc.GetString("ParentSaveNotFound"), GravitonSeverity.Error));
                e.Handled = true;
                return;
            }

            var response = await GravitonPlugin.PlayniteApi.Dialogs.ShowMessageAsync(Loc.GetString("RestoreHistoricSaveConfirm"), Loc.GetString("RestoreHistoricSaveTitle"), MessageBoxSeverity.Question, new List<MessageBoxResponse> { RestoreLocally, FullRestore, Cancel}, new List<MessageBoxOption>());

            if(response == RestoreLocally || response == FullRestore)
            {
                Saves.Remove(parentROM.LocalSave!);
                var result = await SaveController.Manager.Download(historicSave, true);
                if(result.Status == SaveStatus.Synced)
                {
                    parentROM.LocalSave!.HistoricSaves?.Remove(historicSave);

                    if (parentROM.LocalSave.HistoricSaves == null)
                        parentROM.LocalSave.HistoricSaves = new();

                    var savecopy = JsonSerializer.Deserialize<GravitonSave>(JsonSerializer.Serialize(parentROM.LocalSave));
                    if (savecopy != null)
                    {
                        savecopy.IsCurrent = false;
                        savecopy.IsHistoric = true;
                        savecopy.HistoricSaves = null;
                        parentROM.LocalSave.HistoricSaves.Add(savecopy);
                    }

                    result.HistoricSaves = parentROM.LocalSave.HistoricSaves.OrderByDescending(x => x.LastSyncedAt).ToObservableCollection();
                    result.IsCurrent = true;

                    if(response == RestoreLocally)
                    {
                        result.IsTempRestored = true;
                        result.Status = SaveStatus.TempRestored;
                    }
                        
                    parentROM.LocalSave = result;
                    parentROM.Save();

                    if(response == FullRestore)
                        await SaveController.Manager.Upload(historicSave);
                }

                Saves.Add(parentROM.LocalSave!);
            }

            SavesItemControl.ItemsSource = Saves.Where(x => x.GameName.Contains(SaveFilterBox.Text, StringComparison.OrdinalIgnoreCase));
            e.Handled = true;
        }

        private async void AddFileToSave_Click(object sender, RoutedEventArgs e)
        {
            var save = ((FrameworkElement)sender).DataContext as GravitonSave;
            await AddFilesFoldersToSave(save);
            e.Handled = true;
            return;
        }

        private async void AddFolderToSave_Click(object sender, RoutedEventArgs e)
        {
            var save = ((FrameworkElement)sender).DataContext as GravitonSave;
            await AddFilesFoldersToSave(save, true);
            e.Handled = true;
            return;
        }

        private async Task AddFilesFoldersToSave(GravitonSave? save, bool folderSelect = false)
        {
            if (save == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.save.null", Loc.GetString("SaveIsNull"), GravitonSeverity.Error));
                return;
            }

            var rom = _plugin.ImportedGames.FirstOrDefault(x => x.Value.LocalSave?.LocalID == save.LocalID).Value;
            if (rom.LocalSave == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.rom.null", Loc.GetString("GameNotFoundForSave"), GravitonSeverity.Error));
                return;
            }

            var mapping = _plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == rom.MappingID);
            if (mapping == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.mapping.null", Loc.GetString("MappingNotFoundForGame"), GravitonSeverity.Error));
                return;
            }

            List<string>? response;

            if(folderSelect)
                response = await GravitonPlugin.PlayniteApi.Dialogs.SelectFolderAsync(mapping.SavePath, true);
            else
                response = await GravitonPlugin.PlayniteApi.Dialogs.SelectFileAsync(initialDir: mapping.SavePath, allowMultiple: true);

            if (response != null)
            {
                bool needsUpload = false;
                foreach (var path in response)
                {
                    if (path.StartsWith(mapping.SavePath))
                    {
                        rom.LocalSave.SourceFilePaths.Add(path.Replace(mapping.SavePath, EmulatorMapping.SavePathToken));
                        needsUpload = true;
                    }
                }

                if (needsUpload)
                    await SaveController.Negotiator.NegotiateSave(rom);


                if (response.Any(x => !x.StartsWith(mapping.SavePath)))
                    GravitonNotify.Add(new GravitonNotification("graviton.skipped.add", Loc.GetString("FilesOutsideMappingDir"), GravitonSeverity.Error));

                SavesItemControl.ItemsSource = Saves.Where(x => x.GameName.Contains(SaveFilterBox.Text, StringComparison.OrdinalIgnoreCase)).OrderBy(y => y.GameName);
            }
        }
        private void RemoveSourcePath_Click(object sender, RoutedEventArgs e)
        {

            if (sender is not FrameworkElement fe)
            {
                e.Handled = true;
                return;
            }

            var save = fe.Tag as GravitonSave;
            var path = fe.DataContext as string;

            if (save == null || path == null)
            {
                e.Handled = true;
                return;
            }

            save.SourceFilePaths.Remove(path);
            e.Handled = true;
        }
        #endregion
    }
}