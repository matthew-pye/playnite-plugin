using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using RomM.Games;
using RomM.Models.RomM.Rom;
using RomM.Settings;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Windows;

namespace RomM.Save
{
   
    public class SaveController : ObservableObject
    {
        private readonly RomM Plugin;
        private SettingsViewModel Settings;
        private RomMRomLocal LaunchedGame;
        private List<FileInfo> PossibleSaveFiles;

        private EmulatorMapping _currentMapping;
        private ObservableCollection<RomMSave> _remoteSaves = new ObservableCollection<RomMSave>();
        private ObservableCollection<RomMSave> _localSaves = new ObservableCollection<RomMSave>();
        private ObservableCollection<PossibleSave> _possibleSaves = new ObservableCollection<PossibleSave>();

        public List<RomMRomLocal> ROMs { get; set; }

        public ObservableCollection<EmulatorMapping> Mappings
        {
            get => Settings.Mappings;
            set
            {
                OnPropertyChanged();
            }
        }
        public EmulatorMapping CurrentMapping
        {
            get => _currentMapping;
            set
            {
                SettingsViewModel.Instance.Notify = false;

                // Save save data to file
                if (_currentMapping != null)
                    SaveROMRevisions(_currentMapping.MappingId);

                _currentMapping = value;       
                OnPropertyChanged(nameof(FilteredROMs));
                OnPropertyChanged();

                // Pull mapping saves
                if (_currentMapping != null)
                {
                    SyncLocalSaves();
                    SyncPotentialSaves();
                    SyncRemoteSaves();
                    Settings.UpdateNotifcationBar($"Synced saves for {CurrentMapping.RomMPlatform.Name} with {RemoteSaves.Count + LocalSaves.Count} saves found!");
                }
                  
            }
        }

        public ObservableCollection<RomMSave> RemoteSaves
        {
            get => _remoteSaves;
            set
            {
                _remoteSaves = value;
                OnPropertyChanged();
            }
        }
        public ObservableCollection<RomMSave> LocalSaves
        {
            get => _localSaves;
            set
            {
                _localSaves = value;
                OnPropertyChanged();
            }
        }
        public ObservableCollection<PossibleSave> PossibleSaves
        {
            get => _possibleSaves;
            set
            {
                _possibleSaves = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<RomMRevision> FilteredROMs
        {
            get
            {
                if(ROMs != null && CurrentMapping != null)
                {
                    return ROMs.Where(x => x.MappingID == CurrentMapping.MappingId).SelectMany(y => y.ROMVersions).OrderBy(z => z.FileName).ToObservable();
                }
                return new ObservableCollection<RomMRevision>();
            }
            set
            {
                OnPropertyChanged();
            }
        }

        public SaveController(RomM plugin)
        {
            Plugin = plugin;
            Settings = SettingsViewModel.Instance;
            Mappings = Settings.Mappings;
            ROMs = new List<RomMRomLocal>();

            foreach (var game in Directory.EnumerateFiles(Plugin.ROMDataPath, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    string json = File.ReadAllText(game);
                    var gamedata = JsonConvert.DeserializeObject<RomMRomLocal>(json);
                    ROMs.Add(gamedata);
                }
                catch (Exception ex)
                {
                    Plugin.Logger.Error($"[Save Controller] Failed to read json file! - {game}\n\t{ex}");
                }
            }

        }

        public void ReloadROMs()
        {
            ROMs = new List<RomMRomLocal>();

            foreach (var game in Directory.EnumerateFiles(Plugin.ROMDataPath, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    string json = File.ReadAllText(game);
                    ROMs.Add(JsonConvert.DeserializeObject<RomMRomLocal>(json));
                }
                catch (Exception ex)
                {
                    Plugin.Logger.Error($"[Save Controller] Failed to read json file! - {game}\n\t{ex}");
                }
            }
        }

        public void SyncRemoteSaves(bool manual = false)
        {
            try
            {
                if (!Uri.IsWellFormedUriString(Settings.RomMHost, UriKind.RelativeOrAbsolute))
                    throw new ArgumentException("Host is not a valid URL!");

                if(CurrentMapping == null)
                    throw new ArgumentException("No mapping selected cannot sync save for mapping!");

                var response = HttpClientSingleton.Instance.GetAsync($"{Settings.RomMHost}/api/saves?platform_id={CurrentMapping.RomMPlatformId}").GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();

                Stream body = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                using (StreamReader reader = new StreamReader(body))
                {
                    var data = reader.ReadToEnd();
                    if (data == "[]")
                    {
                        RemoteSaves = new ObservableCollection<RomMSave>();
                        return;
                    }

                    var jsonResponse = JArray.Parse(data);
                    RemoteSaves = jsonResponse.ToObject<ObservableCollection<RomMSave>>();
                }

                var toremove = new List<RomMSave>();

                foreach (var rs in RemoteSaves)
                {
                    // Remove remote save if stored locally
                    if (LocalSaves.Any(x => x.ID == rs.ID))
                    {
                        toremove.Add(rs);
                        continue;
                    }
                        
                    RomMRevision rom = ROMs.Where(x => x.MappingID == CurrentMapping.MappingId).SelectMany(y => y.ROMVersions).First(z => z.Id == rs.ROMID);
                    if(rom != null)
                    {
                        if(rom.Save != null && rs.FileName == rom.Save.FileName)
                        {
                            rs.SyncEnabled = rom.Save.SyncEnabled;
                            if (rs.SyncEnabled)
                            {
                                rs.IsInSync = rs.LastUpdated > rom.Save.LastUpdated ? SaveSyncStatus.RemoteNewer :
                                (rs.LastUpdated < rom.Save.LastUpdated ? SaveSyncStatus.LocalNewer :
                                (rs.LastUpdated == rom.Save.LastUpdated ? SaveSyncStatus.InSync : SaveSyncStatus.NotEnabled));
                            }

                            rs.SaveFolder = rom.Save.SaveFolder;                
                        }

                        rs.GameName = Path.GetFileNameWithoutExtension(rom.FileName);

                    }
                }

                foreach (var remove in toremove)
                {
                    RemoteSaves.Remove(remove);
                }   
                
                if(manual)
                    Settings.UpdateNotifcationBar($"Synced saves for {CurrentMapping.RomMPlatform.Name} with {RemoteSaves.Count} saves found!");

            }
            catch (Exception ex)
            {
                Plugin.Logger.Error($"[SaveController] {ex}");
                Settings.UpdateNotifcationBar(ex.Message, true);
            }
        }
        public void RemoteSaveEnabled(RomMSave save)
        {
            if(save.SyncEnabled)
            {
                RomMRevision rom = ROMs.Where(x => x.MappingID == CurrentMapping.MappingId).SelectMany(y => y.ROMVersions).First(z => z.Id == save.ROMID);
                if (rom != null)
                {
                    if (rom.Save != null)
                    {
                        RemoteSaves.First(x => x.ID == save.ID).IsInSync =
                                        save.LastUpdated > rom.Save.LastUpdated ? SaveSyncStatus.RemoteNewer :
                                       (save.LastUpdated < rom.Save.LastUpdated ? SaveSyncStatus.LocalNewer :
                                       SaveSyncStatus.InSync);
                    }
                    else
                    {

                        RemoteSaves.First(x => x.ID == save.ID).IsInSync = SaveSyncStatus.RemoteNewer;
                    }

                    foreach (var saves in RemoteSaves.Where(x => x.ROMID == save.ROMID && x.ID != save.ID))
                    {
                        saves.SyncEnabled = false;
                    }
                }

            }
            else
            {
                RemoteSaves.First(x => x.ID == save.ID).IsInSync = SaveSyncStatus.NotEnabled;
            }   
        }

        public void SyncLocalSaves()
        {
            ObservableCollection<RomMSave> newlocalSaves = new ObservableCollection<RomMSave>();
            foreach (var rom in FilteredROMs)
            {
                if (rom.Save != null)
                {
                    if (rom.Save.ID != -1)
                    {
                        var save = FetchSaveInfo(rom.Save.ID);

                        if (save != null)
                        {
                            rom.Save.IsInSync = save.LastUpdated > rom.Save.LastUpdated ? SaveSyncStatus.RemoteNewer :
                             (save.LastUpdated < rom.Save.LastUpdated ? SaveSyncStatus.LocalNewer :
                             SaveSyncStatus.InSync);
                        }
                        else
                        {
                            rom.Save.IsInSync = SaveSyncStatus.NotUploaded;
                            rom.Save.ID = -1;
                        }
                    }

                  rom.Save.GameName = Path.GetFileNameWithoutExtension(rom.FileName);

                  newlocalSaves.Add(rom.Save);
                }              
            }

            LocalSaves = newlocalSaves;
        }
        public void LocalSaveEnabled(RomMSave save)
        {
            if (save.SyncEnabled)
            {
                RomMRevision rom = ROMs.Where(x => x.MappingID == CurrentMapping.MappingId).SelectMany(y => y.ROMVersions).First(z => z.Id == save.ROMID);
                if (rom != null)
                {

                    var remotesave = FetchSaveInfo(rom.Save.ID);

                    if(remotesave != null)
                    {
                        rom.Save.IsInSync = remotesave.LastUpdated > rom.Save.LastUpdated ? SaveSyncStatus.RemoteNewer :
                                   (remotesave.LastUpdated < rom.Save.LastUpdated ? SaveSyncStatus.LocalNewer :
                                   SaveSyncStatus.InSync);
                    }
                    else
                    {
                        rom.Save.IsInSync = SaveSyncStatus.NotUploaded;
                    }                 
                }

                foreach (var saves in LocalSaves.Where(x => x.ROMID == save.ROMID && x.ID != save.ID))
                {
                    saves.SyncEnabled = false;
                }

            }
            else
            {
                LocalSaves.First(x => x.ID == save.ID).IsInSync = SaveSyncStatus.NotEnabled;
            }
        }

        public void SyncPotentialSaves()
        {
            if (CurrentMapping != null && !string.IsNullOrEmpty(CurrentMapping.GeneralSavePath) && Directory.Exists(CurrentMapping.GeneralSavePath))
            {
                var newpossiblesaves = new ObservableCollection<PossibleSave>();
                List<string> fileExtensions = new List<string>();

                if (!string.IsNullOrEmpty(CurrentMapping.SaveFileExtensions))
                {
                    if(CurrentMapping.SaveFileExtensions.Contains(';'))
                    {
                        fileExtensions = CurrentMapping.SaveFileExtensions.Split(';').ToList();
                    }
                    else
                    {
                        fileExtensions.Add(CurrentMapping.SaveFileExtensions);
                    }
                }
                    
                foreach (var file in new DirectoryInfo(CurrentMapping.GeneralSavePath).GetFiles("*.*", SearchOption.AllDirectories))
                {
                    var possiblesave = new PossibleSave();

                    if (fileExtensions.Count != 0)
                    {
                        if(fileExtensions.Any(x => file.Extension.TrimStart('.').ToLower() == x.ToLower()))
                        {
                            possiblesave.File = file;
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else
                    {
                        possiblesave.File = file;
                    }

                    if(!FilteredROMs.Any(x => x.Save != null && x.Save.FileName == file.Name && x.Save.SaveFolder == file.DirectoryName))
                    {
                        newpossiblesaves.Add(possiblesave);
                    }
                }

                PossibleSaves = newpossiblesaves;

            }
            else
            {
                Settings.UpdateNotifcationBar("Current mapping has no general save path cannot find possible savefiles", true);
                PossibleSaves = new ObservableCollection<PossibleSave>();
            }
        }
        public void UploadNewSave(PossibleSave possiblesave)
        {
            if (possiblesave.Game != null)
            {
                var save = new RomMSave();
                save.ROMID = possiblesave.Game.Id;
                save.SaveFolder = possiblesave.File.DirectoryName;
                save.FileName = possiblesave.File.Name;
                save.LastUpdated = possiblesave.File.LastWriteTime;
                save.IsInSync = SaveSyncStatus.LocalNewer;
                save.SyncEnabled = true;

                UploadSave(save, CurrentMapping.MappingId, true);
                PossibleSaves.Remove(possiblesave);
                SyncLocalSaves();
            }
            else
            {
                Settings.UpdateNotifcationBar("No game selected cannot upload!", true);
            }
        }

        public void SaveROMRevisions(Guid mappingId)
        {
            foreach (var rom in ROMs.Where(x => x.MappingID == mappingId))
            {
                foreach (var revision in rom.ROMVersions)
                {
                    if (revision.Save == null)
                    {
                        RomMSave save = RemoteSaves.FirstOrDefault(x => x.SyncEnabled && x.ROMID == revision.Id);
                        if (save == null)
                            continue;

                        revision.Save = save;
                    }
                }

                File.WriteAllText($"{Plugin.ROMDataPath}{rom.SHA1}.json", JsonConvert.SerializeObject(rom));
            }
        }

        public void GameLaunched(Game game)
        {
            string romMSHA1 = game.GameId.Split(':')[1];
            if (!File.Exists($"{Plugin.ROMDataPath}{romMSHA1}.json"))
            {
                Plugin.Logger.Error($"{game.Name} GameID is malformed!");
            }

            try
            {
                string json = File.ReadAllText($"{Plugin.ROMDataPath}{romMSHA1}.json");
                LaunchedGame = JsonConvert.DeserializeObject<RomMRomLocal>(json);
            }
            catch (Exception)
            {
                Plugin.Logger.Error($"{game.Name} GameID is malformed or {romMSHA1} json file is corrupted!");
            }

            // Check to see if save needs syncing
            var mapping = Settings.Mappings.FirstOrDefault(x => x.MappingId == LaunchedGame.MappingID);
            if (mapping != null && mapping.DownloadSaveBeforeGame)
            {
                var rom = LaunchedGame.ROMVersions.FirstOrDefault(x => x.IsSelected);
                if(rom != null && rom.Save != null)
                {
                    var save = FetchSaveInfo(rom.Save.ID);
                    rom.Save.IsInSync = save.LastUpdated > rom.Save.LastUpdated ? SaveSyncStatus.RemoteNewer :
                         (save.LastUpdated < rom.Save.LastUpdated ? SaveSyncStatus.LocalNewer :
                         SaveSyncStatus.InSync);


                    // Check to see if save has been deleted or a newer save is on the server
                    if (rom.Save.IsInSync == SaveSyncStatus.RemoteNewer || !File.Exists($"{rom.Save.SaveFolder}/{rom.Save.FileName}"))
                    {
                        rom.Save.IsInSync = SaveSyncStatus.RemoteNewer;
                        SyncSave(rom.Save, LaunchedGame.MappingID);
                    }
                }

                PossibleSaveFiles = new DirectoryInfo(mapping.GeneralSavePath).GetFiles("*.*", SearchOption.AllDirectories).ToList();
            } 
        }
        public void GameStopped()
        {
            var mapping = Settings.Mappings.FirstOrDefault(x => x.MappingId == LaunchedGame.MappingID);
            if (mapping != null && mapping.UploadSaveAfterGame)
            {
                var rom = LaunchedGame.ROMVersions.FirstOrDefault(x => x.IsSelected);

                if (rom != null && !rom.NeverSave)
                {
                    if (rom.Save != null)
                    {
                        rom.Save.IsInSync = SaveSyncStatus.LocalNewer;
                        Settings.SaveController.SyncSave(rom.Save, LaunchedGame.MappingID);
                    }
                    else
                    {

                        if(PossibleSaveFiles.Count > 0)
                        {
                            var files = new DirectoryInfo(mapping.GeneralSavePath).GetFiles("*.*", SearchOption.AllDirectories);
                            var saveFiles = new ObservableCollection<PossibleSave>();
                            string[] fileExtensions = new string[0];

                            if (!string.IsNullOrEmpty(CurrentMapping.SaveFileExtensions))
                            {
                                if (CurrentMapping.SaveFileExtensions.TrimEnd(';').Contains(';'))
                                {
                                    fileExtensions = CurrentMapping.SaveFileExtensions.Split(';');
                                }
                                else
                                {
                                    fileExtensions = new string[1];
                                    fileExtensions[0] = CurrentMapping.SaveFileExtensions;
                                }
                            }

                            foreach (var file in files)
                            {
                                // Check for new or updated possible save files 
                                if(!PossibleSaveFiles.Contains(file) || PossibleSaveFiles.Find(x => x.FullName == file.FullName).LastWriteTime != file.LastWriteTime)
                                {
                                    // If mapping has set file Extensions filter out files that don't have that extention
                                    if(fileExtensions.Length != 0)
                                    {
                                        if (fileExtensions.Any(x => Path.GetExtension(file.FullName) == x))
                                        {
                                            var save = new PossibleSave();
                                            save.File = file;
                                            saveFiles.Add(save);
                                        }
                                    }
                                    else
                                    {
                                        var save = new PossibleSave();
                                        save.File = file;
                                        saveFiles.Add(save);
                                    }

                                        
                                }
                            }

                            if (saveFiles.Count > 0)
                            {
                                RomMSaveSelector saveSelectorControl = new RomMSaveSelector(saveFiles);
                                var window = Plugin.Playnite.Dialogs.CreateWindow(new WindowCreationOptions
                                {
                                    ShowMinimizeButton = false,
                                    ShowMaximizeButton = false,
                                    ShowCloseButton = false,
                                });

                                window.Height = 215;
                                window.Width = 600;

                                window.Title = "Select save!";
                                window.ShowInTaskbar = false;
                                window.ResizeMode = ResizeMode.NoResize;
                                window.Owner = API.Instance.Dialogs.GetCurrentAppWindow();
                                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                                window.Content = saveSelectorControl;

                                window.ShowDialog();

                                if (saveSelectorControl.Cancelled)
                                {
                                    return;
                                }
                                else if (saveSelectorControl.NeverSave)
                                {
                                    rom.NeverSave = true;
                                    File.WriteAllText($"{Plugin.ROMDataPath}{LaunchedGame.SHA1}.json", JsonConvert.SerializeObject(LaunchedGame));
                                }
                                else
                                {
                                    var selectedfile = saveSelectorControl.Saves.FirstOrDefault(x => x.IsSelected);

                                    var save = new RomMSave();
                                    save.FileName = selectedfile.File.Name;
                                    save.SaveFolder = selectedfile.File.DirectoryName;
                                    save.ROMID = rom.Id;
                                    save.IsInSync = SaveSyncStatus.LocalNewer;
                                    save.SyncEnabled = true;

                                    UploadSave(save, mapping.MappingId, true);
                                }
                            }

                        }


                        

                    }
                }
            }
        }

        public void SyncSave(RomMSave save, Guid mappingID)
        {
            switch (save.IsInSync)
            {
                case SaveSyncStatus.RemoteNewer:
                    DownloadSave(save, mappingID);
                    break;

                case SaveSyncStatus.LocalNewer:
                    UploadSave(save, mappingID);
                    break;

                case SaveSyncStatus.NotUploaded:
                    UploadSave(save, mappingID, true);
                    break;

                default:
                    break;
            }
        }
        public void RemoveSaveEntry(RomMSave save)
        {
            var rom = ROMs.Where(x => x.MappingID == CurrentMapping.MappingId).SelectMany(y => y.ROMVersions).First(z => z.Id == save.ROMID);
            if (rom != null)
            {
                rom.Save = null;
            }

            SyncLocalSaves();
            SyncPotentialSaves();
            SyncRemoteSaves();
            SaveROMRevisions(CurrentMapping.MappingId);
        }
        public void DeleteSave(RomMSave save)
        {
            RomMDeleteSaveView savedeleteControl = new RomMDeleteSaveView();
            var window = Plugin.Playnite.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = false,
                ShowMaximizeButton = false,
                ShowCloseButton = false,
            });

            window.Height = 100;
            window.Width = 500;

            window.Title = "Delete save!";
            window.ShowInTaskbar = false;
            window.ResizeMode = ResizeMode.NoResize;
            window.Owner = API.Instance.Dialogs.GetCurrentAppWindow();
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.Content = savedeleteControl;

            window.ShowDialog();

            var rom = ROMs.Where(x => x.MappingID == CurrentMapping.MappingId).SelectMany(y => y.ROMVersions).First(z => z.Id == save.ROMID);

            if(savedeleteControl.Remote || savedeleteControl.Local)
            {
                if (savedeleteControl.Remote)
                {
                    try
                    {
                        if (!Uri.IsWellFormedUriString(Settings.RomMHost, UriKind.RelativeOrAbsolute))
                            throw new ArgumentException("Host is not a valid URL!");

                        var saves = new List<int>();
                        saves.Add(rom.Save.ID);
                        var jsonsaves = JsonConvert.SerializeObject(saves);

                        var response = HttpClientSingleton.Instance.PostAsync($"{Settings.RomMHost}/api/saves/delete", new StringContent(jsonsaves)).GetAwaiter().GetResult();
                        response.EnsureSuccessStatusCode();
                    }
                    catch (Exception ex)
                    {
                        Settings.UpdateNotifcationBar(ex.Message, true);
                        Plugin.Logger.Error(ex.ToString());
                    }
                }
                else
                {
                    rom.Save.IsInSync = SaveSyncStatus.NotUploaded;
                }

                if (savedeleteControl.Local)
                {
                    File.Delete($"{rom.Save.SaveFolder}/{rom.Save.FileName}");
                    rom.Save = null;
                }
            }

            SaveROMRevisions(CurrentMapping.MappingId);
        }
        private RomMSave FetchSaveInfo(int id)
        {
            try
            {
                if (!Uri.IsWellFormedUriString(Settings.RomMHost, UriKind.RelativeOrAbsolute))
                    throw new ArgumentException("Host is not a valid URL!");

                var response = HttpClientSingleton.Instance.GetAsync($"{Settings.RomMHost}/api/saves/{id}").GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();

                Stream body = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                using (StreamReader reader = new StreamReader(body))
                {
                    var data = reader.ReadToEnd();
                    var jsonResponse = JObject.Parse(data);
                    return jsonResponse.ToObject<RomMSave>();
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.Error($"[SaveController] {ex}");
                Settings.UpdateNotifcationBar(ex.Message, true);
                return null;
            }
        }
        private void DownloadSave(RomMSave save, Guid mappingID)
        {
            SettingsViewModel.Instance.Notify = false;

            try
            {
                if (!Uri.IsWellFormedUriString(Settings.RomMHost, UriKind.RelativeOrAbsolute))
                    throw new ArgumentException("Host is not a valid URL!");

                if (string.IsNullOrEmpty(Mappings.First(x => x.MappingId == mappingID).GeneralSavePath))
                    throw new ArgumentException($"{Mappings.First(x => x.MappingId == mappingID).MappingName} has no general save location set, skipping save download!");

                if (string.IsNullOrEmpty(save.SaveFolder))
                    save.SaveFolder = Mappings.First(x => x.MappingId == mappingID).GeneralSavePath;

                var response = HttpClientSingleton.Instance.GetAsync($"{Settings.RomMHost}/api/saves/{save.ID}/content").GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();

                var body = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                File.WriteAllBytes($"{save.SaveFolder}/{save.FileName}", body);
                save.IsInSync = SaveSyncStatus.InSync;

                var rom = ROMs.Where(x => x.MappingID == mappingID).SelectMany(y => y.ROMVersions).First(z => z.Id == save.ROMID);
                rom.Save = save;

                string filesizestring;

                if (rom.Save.FileSize > 1000000)
                    filesizestring = $"({((float)rom.Save.FileSize) / 1000 / 1000}MB)";
                else if (rom.Save.FileSize > 1000)
                    filesizestring = $"({((float)rom.Save.FileSize) / 1000}KB)";
                else
                    filesizestring = $"({rom.Save.FileSize}B)";

                Plugin.PlayniteApi.Notifications.Add(new Playnite.SDK.NotificationMessage($"RomM.Save.Downloaded.{rom.Id}", $"Downloaded {save.FileName} from RomM Server! {filesizestring}", Playnite.SDK.NotificationType.Info));
                Settings.UpdateNotifcationBar($"Downloaded {save.FileName} from RomM Server! {filesizestring}");

                SyncLocalSaves();
                SyncRemoteSaves();
                SaveROMRevisions(mappingID);
            }
            catch (Exception ex)
            {
                Settings.UpdateNotifcationBar(ex.Message, true);
            }
        }
        private void UploadSave(RomMSave save, Guid mappingID, bool NewSave = false, bool UpdateNotifBar = false)
        {
            SettingsViewModel.Instance.Notify = false;

            var rom = ROMs.Where(x => x.MappingID == mappingID).SelectMany(y => y.ROMVersions).First(z => z.Id == save.ROMID);
            HttpResponseMessage response = null;

            try
            {
                if (!Uri.IsWellFormedUriString(Settings.RomMHost, UriKind.RelativeOrAbsolute))
                    throw new ArgumentException("Host is not a valid URL!");

                if (!File.Exists($"{save.SaveFolder}/{save.FileName}"))
                    throw new ArgumentException("Save file does not exist!");

                MultipartFormDataContent request = new MultipartFormDataContent();
                var savefile = new ByteArrayContent(File.ReadAllBytes($"{save.SaveFolder}/{save.FileName}"));
                request.Add(savefile, "saveFile", save.FileName);

               

                if (NewSave)
                {
                    response = HttpClientSingleton.Instance.PostAsync($"{Settings.RomMHost}/api/saves?rom_id={save.ROMID.ToString()}", request).GetAwaiter().GetResult();
                    response.EnsureSuccessStatusCode();
                }
                else
                {
                    response = HttpClientSingleton.Instance.PutAsync($"{Settings.RomMHost}/api/saves/{save.ID}", request).GetAwaiter().GetResult();
                    response.EnsureSuccessStatusCode();
                }

                Stream body = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                using (StreamReader reader = new StreamReader(body))
                {
                    var data = reader.ReadToEnd();

                    var jsonResponse = JObject.Parse(data);
                    var savedata = jsonResponse.ToObject<RomMSave>();

                    save.ID = savedata.ID;
                    save.FileSize = savedata.FileSize;
                    save.LastUpdated = savedata.LastUpdated;
                    save.IsInSync = SaveSyncStatus.InSync;
                    rom.Save = save;

                }

                string filesizestring;

                if (save.FileSize > 1000000)
                    filesizestring = $"({((float)save.FileSize) / 1000 / 1000}MB)";
                else if (save.FileSize > 1000)
                    filesizestring = $"({((float)save.FileSize) / 1000}KB)";
                else
                    filesizestring = $"({save.FileSize}B)";

                Plugin.PlayniteApi.Notifications.Add(new Playnite.SDK.NotificationMessage("RomM.Save.Updated", $"Backed up {save.FileName} to RomM Server! {filesizestring}", Playnite.SDK.NotificationType.Info));
                Settings.UpdateNotifcationBar($"Backed up {save.FileName} to RomM Server! {filesizestring}");

                SyncLocalSaves();
                SyncRemoteSaves();
                SaveROMRevisions(mappingID);

            }
            catch (Exception ex)
            {
                Plugin.Logger.Error(ex.ToString());
                Settings.UpdateNotifcationBar(ex.Message, true);

                if (response != null && response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    UploadSave(save, mappingID, true);
                }
                else
                {
                    rom.Save = save;
                    SyncLocalSaves();
                    SyncRemoteSaves();
                    SaveROMRevisions(mappingID);
                }
            }
        } 
    }
}
