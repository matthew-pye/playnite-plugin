using Graviton.Models;
using Graviton.Models.Notifications;

using Playnite;

using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
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
        public ObservableCollection<SaveRow> UnmatchedAutoDetectSaveRow { get; } = new();

        public bool IsLoadingLocal = false;
        public bool IsLoadingRemote = false;
        public bool IsLoadingAutoDetected = false;

        List <MessageBoxResponse> DeleteSaveMessageBoxResponses = new List<MessageBoxResponse>
        {
            new MessageBoxResponse("Remove entry",  isDefault: true),
            new MessageBoxResponse("Delete Save Local Only"),
            new MessageBoxResponse("Delete Save Completly"),
            new MessageBoxResponse("Cancel", isCancel: true)
        };

        private void RefreshFilteredItemSources()
        {
            var filter = FilterBox.Text?.Trim().ToLowerInvariant() ?? "";
            LocalRowsList.ItemsSource = string.IsNullOrEmpty(filter) ? LocalSaveRow : new ObservableCollection<SaveRow>(System.Linq.Enumerable.Where(LocalSaveRow, r => r.GameName.ToLowerInvariant().Contains(filter)));
            RemoteRowsList.ItemsSource = string.IsNullOrEmpty(filter) ? RemoteSaveRow : new ObservableCollection<SaveRow>(System.Linq.Enumerable.Where(RemoteSaveRow, r => r.GameName.ToLowerInvariant().Contains(filter)));
            AutoDetectRowsList.ItemsSource = string.IsNullOrEmpty(filter) ? UnmatchedAutoDetectSaveRow : new ObservableCollection<SaveRow>(System.Linq.Enumerable.Where(UnmatchedAutoDetectSaveRow, r => r.GameName.ToLowerInvariant().Contains(filter)));
        }

        public MappingSaveTab()
        {
            InitializeComponent();
            LocalRowsList.ItemsSource = LocalSaveRow;
            RemoteRowsList.ItemsSource = RemoteSaveRow;
            AutoDetectRowsList.ItemsSource = UnmatchedAutoDetectSaveRow;

            RefreshText.Text = $"\uf46a {Loc.GetString("Refresh")}";
            RefreshText.FontFamily = Playnite.Fonts.NerdFont;
            EnableAllText.Text = Loc.GetString("EnableAllSaves");

            LocalTitleText.Text = Loc.GetString("LocalSaves");
            RemoteTitleText.Text = Loc.GetString("RemoteSaves");
            AutoDetectTitleText.Text = Loc.GetString("UnsyncedAutoDetectSaves");

            if (Mapping != null)
                Load(Mapping);
        }

        public async Task Load(EmulatorMapping mapping)
        {
            LocalSaveRow.Clear();
            RemoteSaveRow.Clear();
            UnmatchedAutoDetectSaveRow.Clear();

            Mapping = mapping;
            TitleText.Text = $"{Loc.GetString("SaveManagerTitle")} — {mapping.MappingName}";

            LoadingLocalBar.Visibility = Visibility.Visible;
            LoadingRemoteBar.Visibility = Visibility.Visible;
            LoadingAutoBar.Visibility = Visibility.Visible;

            await Task.Delay(1000);

            var rows = await _plugin.SaveController!.GetLocalSaves(mapping);
            if (rows != null && rows.Count > 0)
                LocalSaveRow.AddRangeIfNotNull(rows.OrderBy(x => x.GameName));
            LoadingLocalBar.Visibility = Visibility.Collapsed;
            EmptyStateText.Visibility = LocalSaveRow.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            await Task.Delay(500);

            rows = await _plugin.SaveController!.GetRemoteSaves(mapping);
            if (rows != null && rows.Count > 0)
                RemoteSaveRow.AddRangeIfNotNull(rows.Where(x => !LocalSaveRow.Any(y => y.SaveID == x.SaveID)).OrderBy(x => x.GameName));
            LoadingRemoteBar.Visibility = Visibility.Collapsed;
            EmptyRemoteText.Visibility = RemoteSaveRow.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            await Task.Delay(800);

            rows = await _plugin.SaveController!.FindUntrackedAutoDetectSaves(mapping, LocalSaveRow.ToList());
            if (rows != null && rows.Count > 0)
                UnmatchedAutoDetectSaveRow.AddRangeIfNotNull(rows.OrderBy(x => x.GameName));
            LoadingAutoBar.Visibility = Visibility.Collapsed;
            EmptyUnmatchedText.Visibility = UnmatchedAutoDetectSaveRow.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            RefreshFilteredItemSources();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshButton.IsEnabled = false;

            if (Mapping != null) 
                await Load(Mapping);

            RefreshButton.IsEnabled = true;
        }

        private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshFilteredItemSources();
        }

        private async void AddNewSave_Click(object sender, RoutedEventArgs e)
        {
            if (Mapping != null)
            {
                var picker = new NewManualSaveView();
                picker.LoadForPath(Mapping.SavePath);
                picker.ROMs = _plugin.ImportedGames!.Where(x => x.Value.MappingID == Mapping.MappingId).Select(y => y.Value).ToList();

                SaveManagerWindow.Show("Add Manual Save", picker);

                if (!picker.WasConfirmed)
                {
                    e.Handled = true;
                    return;
                }

                if (picker.ROM == null)
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.selectsave.nogameselected", "No game was selected cannot create save backup", GravitonSeverity.Warn));
                    e.Handled = true;
                    return;
                }

                if (picker.SelectedSourcePaths.Count <= 0)
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.selectsave.nonselected", "No save files/folders were selected cannot create save backup", GravitonSeverity.Warn));
                    e.Handled = true;
                    return;
                }

                var result = await _plugin.SaveController!.UploadNewSave(Mapping, picker.ROM, picker.SelectedSourcePaths);
                if (result == null)
                {
                    e.Handled = true;
                    return;
                }
                result.SourcePaths = picker.SelectedSourcePaths;
                LocalSaveRow.Add(result);

                EmptyStateText.Visibility = LocalSaveRow.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                RefreshFilteredItemSources();
            }    

            e.Handled = true;
        }

        private async void SyncSave_Click(object sender, RoutedEventArgs e)
        {
            var row = ((FrameworkElement)sender).DataContext as SaveRow;
            if (row == null)
            {
                e.Handled = true;
                return;
            }

            var rom = _plugin.ImportedGames!.FirstOrDefault(x => x.Value.Id == row.GameID);
            if (rom.Value == null)
            {
                e.Handled = true;
                return;
            }

            var localsave = rom.Value.Saves.FirstOrDefault(y => y.LocalID == row.LocalSaveGuid);

            if (localsave == null)
            {
                e.Handled = true;
                return;
            }

            if (localsave.SaveID == -1)
            {
                var newrow = await _plugin.SaveController!.UploadNewSave(Mapping!, rom.Value, localsave.SourceFilePaths);
                if (newrow != null)
                {
                    LocalSaveRow.Remove(row);
                    LocalSaveRow.Add(newrow);
                }
            }
            else
            {
                var newrow = await _plugin.SaveController!.NegotiateSave(Mapping!, localsave, rom.Value);
                if (newrow != null)
                {
                    LocalSaveRow.Remove(row);
                    LocalSaveRow.Add(newrow);
                }
            }

            RefreshFilteredItemSources();
            e.Handled = true;
        }

        private async void DeleteSave_Click(object sender, RoutedEventArgs e)
        {
            var row = ((FrameworkElement)sender).DataContext as SaveRow;
            if (row == null)
            {
                e.Handled = true;
                return;
            }

            var result = await GravitonPlugin.PlayniteApi.Dialogs.ShowMessageAsync($"How do you want to delete the save?", "Delete Save?", MessageBoxSeverity.Question, DeleteSaveMessageBoxResponses, new List<MessageBoxOption>());

            if(result != null && result.Title != "Cancel")
            {
                var doublecheckresult = await GravitonPlugin.PlayniteApi.Dialogs.ShowMessageAsync($"Are you sure you want to '{result.Title}' ?", "Delete Save?", MessageBoxButtons.YesNo, MessageBoxSeverity.Question);
                if (doublecheckresult == Playnite.MessageBoxResult.Yes)
                {
                    var rom = _plugin.ImportedGames!.First(x => x.Value.Id == row.GameID).Value;
                    var save = rom.Saves.First(x => x.LocalID == row.LocalSaveGuid);
                    rom.Saves.Remove(save);

                    if (result.Title == "Delete Save Local Only")
                    {
                        foreach (var path in save.SourceFilePaths)
                        {
                            var fullpath = path.Replace("{MappingSavePath}", Mapping!.SavePath);

                            if (Path.HasExtension(fullpath) && File.Exists(fullpath))
                            {
                                File.Delete(fullpath);
                            }
                            else if (Directory.Exists(fullpath))
                            {
                                Directory.Delete(fullpath, true);
                            }
                        }

                    }
                    else if (result.Title == "Delete Save Completly")
                    {
                        foreach (var path in save.SourceFilePaths)
                        {
                            var fullpath = path.Replace("{MappingSavePath}", Mapping!.SavePath);

                            if (Path.HasExtension(fullpath) && File.Exists(fullpath))
                            {
                                File.Delete(fullpath);
                            }
                            else if (Directory.Exists(fullpath))
                            {
                                Directory.Delete(fullpath, true);
                            }
                        }
                        var deleteSave = new { saves = new { save.SaveID } };
                        var response = await HttpClientSingleton.RomMPostJsonAsync("/api/saves/delete", deleteSave);
                    }

                    await _plugin.SaveController!.UntrackSave(save.SaveID);

                    string json = JsonSerializer.Serialize(rom);
                    File.WriteAllText($"{_plugin.PluginDataPath}/Games/{rom.SHA1}.json", json);

                    LocalSaveRow.Remove(row);
                }
            }

            EmptyStateText.Visibility = LocalSaveRow.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyRemoteText.Visibility = RemoteSaveRow.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            RefreshFilteredItemSources();
            e.Handled = true;
        }

        private async void SyncRemoteSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(Mapping!.SavePath))
            {
                GravitonNotify.Add(new GravitonNotification("graviton.mapping.save.failed", Loc.GetString("NoSavePathSet"), GravitonSeverity.Error));
            }
            else
            {
                var row = ((FrameworkElement)sender).DataContext as SaveRow;
                if (row != null && Mapping != null)
                {
                    var result = await GravitonPlugin.PlayniteApi.Dialogs.ShowMessageAsync($"Do you want to save download save?\nSave: {row.SaveDirectoryView[0].Name}\nSave Path: {Mapping.SavePath}'?", "Download Save?", MessageBoxButtons.YesNo, MessageBoxSeverity.Question);
                    if (result == Playnite.MessageBoxResult.Yes)
                    {
                        var saverow = await _plugin.SaveController!.DownloadNewSave(Mapping, row);
                        if (saverow == null)
                        {
                            e.Handled = true;
                            return;
                        }

                        RemoteSaveRow.Remove(row);
                        LocalSaveRow.Add(saverow);

                    }
                }
            }

            EmptyStateText.Visibility = LocalSaveRow.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyRemoteText.Visibility = RemoteSaveRow.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            RefreshFilteredItemSources();

            e.Handled = true;
        }

        private async void UploadNewMatchedSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(Mapping!.SavePath))
            {
                GravitonNotify.Add(new GravitonNotification("graviton.mapping.save.failed", Loc.GetString("NoSavePathSet"), GravitonSeverity.Error));
            }
            else
            {
                var row = ((FrameworkElement)sender).DataContext as SaveRow;
                if (row != null && Mapping != null)
                {
                    var result = await GravitonPlugin.PlayniteApi.Dialogs.ShowMessageAsync($"Do you want to upload this save?", "Upload Save?", MessageBoxButtons.YesNo, MessageBoxSeverity.Question);
                    if (result == Playnite.MessageBoxResult.Yes)
                    {
                        var rom = _plugin.ImportedGames!.FirstOrDefault(x => x.Value.Id == row.GameID).Value;

                        var saverow = await _plugin.SaveController!.UploadNewSave(Mapping, rom, row.SourcePaths!);
                        if (saverow == null)
                        {
                            e.Handled = true;
                            return;
                        }

                        LocalSaveRow.Add(saverow);
                        UnmatchedAutoDetectSaveRow.Remove(row);
                        var remoterow = RemoteSaveRow.FirstOrDefault(x => x.SaveID == saverow.SaveID);
                        if (remoterow != null)
                            RemoteSaveRow.Remove(remoterow);
                    }
                }
            }

            EmptyStateText.Visibility = LocalSaveRow.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyRemoteText.Visibility = RemoteSaveRow.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyUnmatchedText.Visibility = UnmatchedAutoDetectSaveRow.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            RefreshFilteredItemSources();

            e.Handled = true;
        }
    }
}