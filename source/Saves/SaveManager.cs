using Graviton.Models;
using Graviton.Models.Notifications;
using Graviton.Models.RomM;
using Graviton.Models.RomM.Rom;
using Graviton.Models.RomM.Saves;
using Graviton.Models.Saves;

using Nanook.GrindCore.MD;

using Playnite;

using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;

using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace Graviton.Saves
{
    internal static class SaveManager
    {
        private static GravitonPlugin _plugin => GravitonPlugin.Instance;
        private static IPlayniteApi PlayniteAPI => GravitonPlugin.PlayniteApi;
 
        public static async Task<List<RomMRomLocal>?> SoftNegotiateSaves(List<RomMRomLocal> roms)
        {
            var negotiate = BuildNegotiate(roms);

            if (negotiate.Saves.Count <= 0) // Nothing to sync
            {
                GravitonPlugin.Logger.Error("[SaveManager] No saves in negotiate, skipping!");
                return null;
            }

            var response = await Negotiate(negotiate);

            foreach (var rom in roms)
            {
                if(response == null)
                {
                    rom.LocalSave.Status = SaveStatus.Unknown;
                    rom.LocalSave.ServerHash = null;
                    continue;
                }

               var operation = response.Operations.FirstOrDefault(x => x.ROMID == rom.Id && x.Slot == rom.LocalSave.Slot);
               rom.LocalSave.ServerLastUpdatedAt = null;

               if (operation == null)
               {
                    rom.LocalSave.Status = SaveStatus.Unknown;
                    rom.LocalSave.ServerHash = null; 
               }
               else
               {
                    rom.LocalSave.ServerHash = operation.ServerContentHash;
                    DateTime lastUpdatedAt;
                    if (DateTime.TryParse(operation.ServerUpdatedAt!, out lastUpdatedAt))
                        rom.LocalSave.ServerLastUpdatedAt = lastUpdatedAt;

                    switch (operation.Action)
                    {
                        case "upload":
                            rom.LocalSave.Status = SaveStatus.LocalNewer;
                            break;

                        case "download":
                            rom.LocalSave.Status = SaveStatus.RemoteNewer;
                            break;

                        case "no_op":
                            rom.LocalSave.Status = SaveStatus.Synced;
                            break;

                        case "conflict":
                            rom.LocalSave.Status = SaveStatus.Conflicted;
                            break;

                        default:
                            rom.LocalSave.Status = SaveStatus.Unknown;
                            break;
                    }
                }

                var mapping = _plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == rom.MappingID);
                if (mapping != null)
                    rom.LocalSave.SaveDirectoryTrees = SaveDirectoryTree.Build(mapping.SavePath, rom.LocalSave.SourceFilePaths);

                rom.Save();
            }

            return roms;
        }

        public static async Task NegotiateSave(RomMRomLocal rom)
        {
            var negotiate = BuildNegotiate(new() { rom });
            if (negotiate.Saves.Count <= 0) // Nothing to sync
            {
                GravitonPlugin.Logger.Error("[SaveManager] No saves in negotiate, skipping!");
                return;
            }

            var response = await Negotiate(negotiate);

            int operationCompleted = 0;
            int operationFailed = 0;

            if (response == null)
            {
                rom.LocalSave.Status = SaveStatus.Unknown;
                rom.LocalSave.ServerHash = null;
                rom.LocalSave.ServerLastUpdatedAt = null;
                rom.Save();
                return;
            }
            else
            {
                var operation = response.Operations.FirstOrDefault(x => x.ROMID == rom.Id && x.Slot == rom.LocalSave.Slot);
                rom.LocalSave.ServerLastUpdatedAt = null;

                if (operation == null)
                {
                    rom.LocalSave.Status = SaveStatus.Unknown;
                    rom.LocalSave.ServerHash = null;
                    operationFailed++;
                }
                else
                {
                    rom.LocalSave.ServerHash = operation.ServerContentHash;
                    DateTime lastUpdatedAt;
                    if (DateTime.TryParse(operation.ServerUpdatedAt!, out lastUpdatedAt))
                        rom.LocalSave.ServerLastUpdatedAt = lastUpdatedAt;

                    var action = operation.Action;

                    if (operation.Action == "conflict")
                    {
                        action = ResolveConflict(rom.LocalSave).ToString();
                    }

                    switch (action)
                    {
                        case "upload":
                            await Upload(rom.LocalSave);
                            rom.LocalSave.IsTempRestored = false;
                            operationCompleted++;
                            break;

                        case "download":
                            await Download(rom.LocalSave);
                            rom.LocalSave.IsTempRestored = false;
                            operationCompleted++;
                            break;

                        case "no_op":
                            rom.LocalSave.Status = SaveStatus.Synced;
                            rom.LocalSave.IsTempRestored = false;
                            operationCompleted++;
                            break;

                        default:
                            rom.LocalSave.Status = SaveStatus.Unknown;
                            operationFailed++;
                            break;
                    }
                }

                var mapping = _plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == rom.MappingID);
                if (mapping != null)
                    rom.LocalSave.SaveDirectoryTrees = SaveDirectoryTree.Build(mapping.SavePath, rom.LocalSave.SourceFilePaths);

            }

            rom.Save();

            var deviceid = new { operations_completed = operationCompleted, operations_failed = operationFailed};
            await HttpClientSingleton.RomMPostJsonAsync($"/api/sync/sessions/{response.SessionID}/complete", deviceid);

        }

        private static async Task<RomMNegotiateResponse?> Negotiate(RomMNegotiate negotiate)
        {
            
            var response = await HttpClientSingleton.RomMPostJsonAsync("/api/sync/negotiate", negotiate);
            if (response == null)
                return null;

            try
            {
                return JsonSerializer.Deserialize<RomMNegotiateResponse>(response);
            }
            catch (Exception ex)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.negotiatesaves.failed", Loc.GetString("FailedNegotiateSaves", ("Error", ex.Message)), GravitonSeverity.Error, ex));
                return null;
            }
        }

        private static RomMNegotiate BuildNegotiate(List<RomMRomLocal> roms)
        {
            RomMNegotiate negotiate = new RomMNegotiate();
            negotiate.DeviceID = _plugin.Settings.AccountState.DeviceID;

            foreach (var rom in roms)
            {
                var mapping = _plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == rom.MappingID);
                if (mapping == null)
                    continue;

                RomMNegotiateSave negotiateSave = new()
                {
                    ROMID = rom.Id,
                    Slot = rom.LocalSave.Slot
                };

                var path = rom.LocalSave.SourceFilePaths[0].Replace(EmulatorMapping.MappingPathToken, mapping.SavePath);

                if (rom.LocalSave.SourceFilePaths.Count == 1 && File.Exists(path))
                {
                    negotiateSave.FileSize = new FileInfo(path).Length;
                    rom.LocalSave.FileSize = negotiateSave.FileSize;

                    negotiateSave.FileName = rom.LocalSave.Filename;
                    negotiateSave.UpdatedAt = new FileInfo(path).LastWriteTimeUtc.ToString("O");

                    negotiateSave.ContentHash = ComputeFileContentHash(path);
                    rom.LocalSave.ContentHash = negotiateSave.ContentHash;
                }
                else if(rom.LocalSave.SourceFilePaths.Count > 1)
                {
                    var packedsavepath = $"{_plugin.PluginDataPath}/temp/{rom.LocalSave.Filename}";
                    if (!PackSave(rom.LocalSave.SourceFilePaths, mapping.SavePath, packedsavepath))
                        continue;

                    negotiateSave.FileSize = new FileInfo(packedsavepath).Length;
                    rom.LocalSave.FileSize = negotiateSave.FileSize;

                    List<DateTime> saveWritetimes = new List<DateTime>();
                    foreach (var savepath in rom.LocalSave.SourceFilePaths)
                    {
                        path = savepath.Replace(EmulatorMapping.MappingPathToken, mapping.SavePath);

                        if (File.Exists(path))
                        {
                            saveWritetimes.Add(new FileInfo(path).LastWriteTimeUtc);
                        }
                        else
                        {
                            saveWritetimes.Add(new DirectoryInfo(path).LastWriteTimeUtc);
                        }
                    }

                    negotiateSave.FileName = Path.GetFileName(packedsavepath);
                    negotiateSave.UpdatedAt = saveWritetimes.Max().ToString("O");

                    negotiateSave.ContentHash = ComputePackedContentHash(packedsavepath);
                    rom.LocalSave.ContentHash = negotiateSave.ContentHash;

                    if (!(rom.LocalSave.SourceFilePaths.Count == 1 && Path.HasExtension(rom.LocalSave.SourceFilePaths[0])))
                        File.Delete(packedsavepath);
                }
                else
                {
                    continue;
                }

                negotiate.Saves.Add(negotiateSave);
            }

            return negotiate;
        }

        public static SaveSyncStatus ResolveConflict(GravitonSave save)
        {
            var window = PlayniteAPI.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = false,
                ShowMaximizeButton = false,
                ShowCloseButton = true,
                DefaultWidth = 650,
                DefaultHeight = 315
            });

            if (save.ServerLastUpdatedAt == null)
                return SaveSyncStatus.conflict;

            var resolveConflictView = new ResolveConflictView(save.ServerLastUpdatedAt.Value, save.LastSyncedAt);

            window.Title = "Save Conflict";
            window.Content = resolveConflictView;
            window.ResizeMode = ResizeMode.NoResize;
            window.Owner = PlayniteAPI.GetLastActiveWindow();
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();

            return resolveConflictView.Status;
        }

        public static async Task<GravitonSave> Upload(GravitonSave save, bool overwrite = false)
        {
            var rom = _plugin.ImportedGames!.FirstOrDefault(x => x.Value.Id == save.ROMID).Value;
            if(rom == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.upload.failed", "Failed to find ROM that matches save, skipping upload", GravitonSeverity.Error));
                return save;
            }

            var mapping = _plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == rom.MappingID);
            if (mapping == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.upload.failed", "Failed to mapping, skipping upload", GravitonSeverity.Error));
                return save;
            }

            if(save.SaveID != -1)
                await UntrackSave(rom.LocalSave.SaveID);

            bool isPacked = false;
            string savePath = save.SourceFilePaths[0].Replace(EmulatorMapping.MappingPathToken, mapping.SavePath);
            if (save.SourceFilePaths.Count > 1 || !File.Exists(savePath))
            {
                savePath = $"{_plugin.PluginDataPath}/temp/{save.Filename}";
                isPacked = true;
                if (!PackSave(save.SourceFilePaths, mapping.SavePath, savePath))
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.upload.failed", "Failed to pack save, skipping upload", GravitonSeverity.Error));
                    return save;
                }
            }

            var savebytes = File.ReadAllBytes(savePath);
            var content = new MultipartFormDataContent();
            
            var savecontent = new ByteArrayContent(savebytes);
            savecontent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(savecontent, "saveFile", Path.GetFileName(savePath));

            var response = await HttpClientSingleton.RomMRawPostContentAsync($"/api/saves?rom_id={rom.Id}&slot={save.Slot}&autocleanup={_plugin.Settings.AutoCleanupSaves}&autocleanup_limit={_plugin.Settings.AutoCleanupSavesLimit}&device_id={_plugin.Settings.AccountState.DeviceID}&overwrite={overwrite}", content);

            if (response?.Status == HttpStatusCode.Conflict)
            {
                var autoConflictResolve = await AutoConflictResolve(save, rom, savePath, isPacked);
                
                if (autoConflictResolve == SaveSyncStatus.conflict)
                    autoConflictResolve = ResolveConflict(save);

                switch (autoConflictResolve)
                {
                    case SaveSyncStatus.upload:
                        return await Upload(save, true);
                    case SaveSyncStatus.download:
                        return await Download(save);
                    default:
                        GravitonNotify.Add(new GravitonNotification("graviton.upload.failed", "Failed to resolve save conflict, skipping upload", GravitonSeverity.Error));
                        return save;
                }
            }

            if (isPacked)
                File.Delete(savePath);

            if (response?.Status != HttpStatusCode.OK || response.Content == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.upload.failed", "Response from server doesn't indicate success, skipping upload", GravitonSeverity.Error));
                return save;
            }

            try
            {
                var stringresponse = new StreamReader(response.Content);
                var result = JsonSerializer.Deserialize<RomMSave>(stringresponse.ReadToEnd());
                if (result == null)
                    throw new Exception();

                if (save.HistoricSaves == null)
                    save.HistoricSaves = new();

                var savecopy = JsonSerializer.Deserialize<GravitonSave>(JsonSerializer.Serialize(save));
                if(savecopy != null)
                {
                    savecopy.IsCurrent = false;
                    save.HistoricSaves.Add(savecopy);
                }
                    
                save.SaveID = result.ID;
                save.Status = SaveStatus.Synced;
                save.IsCurrent = true;

                save.LastSyncedAt = DateTime.Parse(result.UpdatedAt!);
                save.ContentHash = result.ContentHash;
                save.FileSize = result.FileSize!.Value;

                save.LastSyncedContentHash = result.ContentHash;

                save.ServerLastUpdatedAt = DateTime.Parse(result.UpdatedAt!);
                save.ServerHash = result.ContentHash;

                rom.LocalSave = save;
                rom.Save();

                var deviceid = new { device_id = _plugin.Settings.AccountState.DeviceID };
                await HttpClientSingleton.RomMPostJsonAsync($"/api/saves/{save.SaveID}/track", deviceid);

                GravitonNotify.Add(new GravitonNotification("graviton.upload.success", $"{rom.Name} save backed up ({save.FileSizeString})", GravitonSeverity.Success));
                return save;
            }
            catch (Exception)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.upload.failed", "Failed to deserialize server response!", GravitonSeverity.Error));
                return save;
            }
        }

        public static async Task<GravitonSave> Download(GravitonSave save, bool SkipSavingROM = false)
        {
            var rom = _plugin.ImportedGames!.FirstOrDefault(x => x.Value.Id == save.ROMID).Value;
            if (rom == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.download.failed", "Failed to find ROM that matches save, skipping download", GravitonSeverity.Error));
                return save;
            }

            var mapping = _plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == rom.MappingID);
            if (mapping == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.download.failed", "Failed to mapping, skipping download", GravitonSeverity.Error));
                return save;
            }

            var savedata = await HttpClientSingleton.RomMRawGetAsync($"/api/saves/{save.SaveID}/content?device_id={_plugin.Settings.AccountState.DeviceID}&optimistic=false");
            if (savedata == null || savedata.Status != HttpStatusCode.OK)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.download.failed", "Failed to get save data from server, skipping download", GravitonSeverity.Error));
                return save;
            }

            if (!Directory.Exists($"{_plugin.PluginDataPath}/temp/"))
                Directory.CreateDirectory($"{_plugin.PluginDataPath}/temp/");

            var tempDir = $"{_plugin.PluginDataPath}/temp/{save.Filename}";
            
            using var ms = new MemoryStream();
            savedata.Content!.CopyTo(ms);
            File.WriteAllBytes(tempDir, ms.ToArray());

            if (ArchiveFactory.IsArchive(tempDir, out _))
            {
                var paths = UnpackSave(tempDir, mapping.SavePath);
                if(paths == null)
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.download.failed", "Failed to unpack save data, skipping download", GravitonSeverity.Error));
                    return save;
                }

                save.SourceFilePaths = paths;
                save.SourceFilePaths= save.SourceFilePaths.Select(x => x.Replace(mapping.SavePath, EmulatorMapping.MappingPathToken)).ToList();
            }
            else
            {
                var savelocation = save.SourceFilePaths[0].Replace(EmulatorMapping.MappingPathToken, mapping.SavePath);
                File.Move(tempDir, savelocation, true);
            }

            save.LastSyncedAt = save.ServerLastUpdatedAt!.Value;
            save.ContentHash = save.ServerHash;
            save.LastSyncedContentHash = save.ContentHash;
            save.FileSize = ms.Length;
            save.Status = SaveStatus.Synced;
            
            if(!SkipSavingROM)
            {
                rom.LocalSave = save;
                rom.Save();
            }

            var deviceid = new { device_id = _plugin.Settings.AccountState.DeviceID };
            await HttpClientSingleton.RomMPostJsonAsync($"/api/saves/{save.SaveID}/downloaded", deviceid);
            
            GravitonNotify.Add(new GravitonNotification("graviton.download.success", $"{rom.Name} save downloaded ({save.FileSizeString})", GravitonSeverity.Success));
            return save;
        }

        public static async Task<GravitonSave> TrackNewRemoteSave(GravitonSave save)
        {
            var rom = _plugin.ImportedGames!.FirstOrDefault(x => x.Value.Id == save.ROMID).Value;
            if (rom == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.download.failed", "Failed to find ROM that matches save, Skipping download", GravitonSeverity.Error));
                return save;
            }

            var mapping = _plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == rom.MappingID);
            if (mapping == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.download.failed", "Failed to mapping, Skipping download", GravitonSeverity.Error));
                return save;
            }

            if(rom.LocalSave.SaveID != -1)
            {
                var result = await PlayniteAPI.Dialogs.ShowMessageAsync($"A save is already being tracked for this game, Do you want to replace the save being tracked?\n\nSlot:{rom.LocalSave.Slot}\nFilename:{rom.LocalSave.Filename}", "Existing Save!", MessageBoxButtons.YesNo, MessageBoxSeverity.Warning);
                if(result == Playnite.MessageBoxResult.No)
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.download.failed", "A save is already being tracked, Skipping download", GravitonSeverity.Info));
                    return save;
                }

                await UntrackSave(rom.LocalSave.SaveID);
            }

            var savedata = await HttpClientSingleton.RomMRawGetAsync($"/api/saves/{save.SaveID}/content?device_id={_plugin.Settings.AccountState.DeviceID}&optimistic=false");
            if (savedata == null || savedata.Status != HttpStatusCode.OK)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.download.failed", "Failed to get save data from server, skipping download", GravitonSeverity.Error));
                return save;
            }

            if (!Directory.Exists($"{_plugin.PluginDataPath}/temp/"))
                Directory.CreateDirectory($"{_plugin.PluginDataPath}/temp/");

            var tempDir = $"{_plugin.PluginDataPath}/temp/{save.Filename}";

            using var ms = new MemoryStream();
            savedata.Content!.CopyTo(ms);
            File.WriteAllBytes(tempDir, ms.ToArray());

            if (ArchiveFactory.IsArchive(tempDir, out _))
            {
                string savepath = mapping.SavePath;

                var result = await PlayniteAPI.Dialogs.ShowMessageAsync($"Do you want the save to be unpacked here:\n{savepath}", "Save location!", MessageBoxButtons.YesNo, MessageBoxSeverity.Warning);
                if (result == Playnite.MessageBoxResult.No)
                {
                    var savepaths = await PlayniteAPI.Dialogs.SelectFolderAsync(savepath);
                    if(savepaths == null || savepaths.Count < 1)
                    {
                        GravitonNotify.Add(new GravitonNotification("graviton.download.failed", "Failed to set extraction path, skipping download", GravitonSeverity.Error));
                        return save;
                    }
                    else
                    {
                        savepath = savepaths[0];
                    }
                }

                var paths = UnpackSave(tempDir, savepath);
                if (paths == null)
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.download.failed", "Failed to unpack save data, skipping download", GravitonSeverity.Error));
                    return save;
                }

                paths= save.SourceFilePaths.Select(x => x.Replace(mapping.SavePath, EmulatorMapping.MappingPathToken)).ToList();
                save.SourceFilePaths = paths;
            }
            else
            {
                var savelocation = save.SourceFilePaths[0].Replace(EmulatorMapping.MappingPathToken, mapping.SavePath);
                File.Move(tempDir, savelocation, true);
            }

            save.LastSyncedAt = save.ServerLastUpdatedAt!.Value;
            save.ContentHash = save.ServerHash;
            save.LastSyncedContentHash = save.ServerHash;
            save.FileSize = ms.Length;
            save.Status = SaveStatus.Synced;

            rom.LocalSave = save;
            rom.Save();

            var deviceid = new { device_id = _plugin.Settings.AccountState.DeviceID };
            await HttpClientSingleton.RomMPostJsonAsync($"/api/saves/{save.SaveID}/downloaded", deviceid);

            GravitonNotify.Add(new GravitonNotification("graviton.download.success", $"{rom.Name} save downloaded ({save.FileSizeString})", GravitonSeverity.Success));
            return save;
        }

        public static async Task<GravitonSave> TrackNewLocalSave(GravitonSave save)
        {
            return await Upload(save);
        }

        public static async Task<GravitonSave?> DownloadArchivedSave(RomMSave save)
        {
            var rom = _plugin.ImportedGames!.FirstOrDefault(x => x.Value.Id == save.ROMID).Value;
            if (rom == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.download.failed", "Failed to find ROM that matches save, skipping download", GravitonSeverity.Error));
                return null;
            }

            if(rom.LocalSave.SaveID != -1)
            {
                var result = await PlayniteAPI.Dialogs.ShowMessageAsync($"{rom.Name} already has a save being tracked do you want to replace it?", "Replace save", MessageBoxButtons.YesNo);
                if(result == Playnite.MessageBoxResult.No)
                {
                    return null;
                }
            }

            var mapping = _plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == rom.MappingID);
            if (mapping == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.download.failed", "Failed to mapping, skipping download", GravitonSeverity.Error));
                return null;
            }

            var savedata = await HttpClientSingleton.RomMRawGetAsync($"/api/saves/{save.ID}/content?device_id={_plugin.Settings.AccountState.DeviceID}&optimistic=false");
            if (savedata == null || savedata.Status != HttpStatusCode.OK)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.download.failed", "Failed to get save data from server, skipping download", GravitonSeverity.Error));
                return null;
            }

            if (!Directory.Exists($"{_plugin.PluginDataPath}/temp/"))
                Directory.CreateDirectory($"{_plugin.PluginDataPath}/temp/");

            var tempDir = $"{_plugin.PluginDataPath}/temp/{save.FileName}";

            using var ms = new MemoryStream();
            savedata.Content!.CopyTo(ms);
            File.WriteAllBytes(tempDir, ms.ToArray());

            GravitonSave newsave = new GravitonSave();
            newsave.ROMID = rom.Id;
            newsave.IsCurrent = true;

            if (ArchiveFactory.IsArchive(tempDir, out _))
            {
                var paths = UnpackSave(tempDir, mapping.SavePath);
                if (paths == null)
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.download.failed", "Failed to unpack save data, skipping download", GravitonSeverity.Error));
                    return null;
                }

                newsave.SourceFilePaths = paths;
                newsave.SourceFilePaths = newsave.SourceFilePaths.Select(x => x.Replace(mapping.SavePath, EmulatorMapping.MappingPathToken)).ToList();
            }
            else
            {
                var savelocation = Path.Combine(mapping.SavePath, save.FileName!);
                File.Move(tempDir, savelocation, true);
                newsave.SourceFilePaths = new() { savelocation.Replace(mapping.SavePath, EmulatorMapping.MappingPathToken) };
            }

            newsave.Filename = save.FileName!;
            newsave.LastSyncedAt = DateTime.Parse(save.UpdatedAt!);
            newsave.ContentHash = save.ContentHash;
            newsave.LastSyncedContentHash = save.ContentHash;
            newsave.FileSize = ms.Length;
            newsave.Status = SaveStatus.LocalNewer;

            var deviceid = new { device_id = _plugin.Settings.AccountState.DeviceID };
            await HttpClientSingleton.RomMPostJsonAsync($"/api/saves/{save.ID}/downloaded", deviceid);

            return await TrackNewLocalSave(newsave);
        }

        public static async Task CheckRestoredSaveNeedUploading(RomMRomLocal rom)
        {
            string? prevContentHash = rom.LocalSave.ContentHash;
            var negotiate = BuildNegotiate(new() { rom });
            if (negotiate.Saves.Count <= 0) // Nothing to sync
            {
                GravitonPlugin.Logger.Error("[SaveManager] No saves in negotiate, skipping!");
                return;
            }

            if (prevContentHash != negotiate.Saves[0].ContentHash)
            {
                await NegotiateSave(rom);
            }
        }

        public static async Task UntrackSave(int saveID)
        {
            var deviceid = new { device_id = _plugin.Settings.AccountState.DeviceID };
            await HttpClientSingleton.RomMPostJsonAsync($"/api/saves/{saveID}/untrack", deviceid);
        }

        private static async Task<SaveSyncStatus> AutoConflictResolve(GravitonSave save, RomMRomLocal rom, string localFilePath, bool isPacked)
        {
            string? localHash = null;
            localHash = isPacked ? ComputePackedContentHash(localFilePath) : ComputeFileContentHash(localFilePath); 

            if (isPacked) 
                File.Delete(localFilePath); 
        
            var negotiate = BuildNegotiate(new() { rom });
            var negotiateResponse = negotiate.Saves.Count > 0 ? await Negotiate(negotiate) : null;
            var operation = negotiateResponse?.Operations.FirstOrDefault(x => x.ROMID == rom.Id && x.Slot == save.Slot);

            string? serverHeadHash = operation?.ServerContentHash;
            if (operation != null)
            {
                save.ServerHash = operation.ServerContentHash;
                save.SaveID = operation.SaveID ?? save.SaveID;
                if (DateTime.TryParse(operation.ServerUpdatedAt, out var updatedAt))
                    save.ServerLastUpdatedAt = updatedAt;
            }

            bool haveLocal = !string.IsNullOrEmpty(localHash);

            // Unchanged since last sync
            if (haveLocal && !string.IsNullOrEmpty(save.LastSyncedContentHash) && localHash == save.LastSyncedContentHash)
                return SaveSyncStatus.download;

            // Byte Identical to the current save
            if (haveLocal && !string.IsNullOrEmpty(serverHeadHash) && localHash == serverHeadHash)
                return SaveSyncStatus.download;

            return SaveSyncStatus.conflict;
        }


        #region Helper Functions
        private static List<string>? UnpackSave(string tempSaveLocation, string destinationPath)
        {
            if (!File.Exists(tempSaveLocation))
            {
                GravitonNotify.Add(new GravitonNotification("graviton.unpacksave.failed", Loc.GetString("SaveArchiveNotFound", ("SaveLoc", tempSaveLocation)), GravitonSeverity.Error));
                return null;
            }

            try
            {
                Directory.CreateDirectory(destinationPath);

                using var archive = ArchiveFactory.OpenArchive(tempSaveLocation);

                var destinationFull = Path.GetFullPath(destinationPath);
                var fileEntries = archive.Entries.Where(e => !e.IsDirectory).ToList();

                var resolvedPaths = new List<string>(fileEntries.Count);
                foreach (var entry in fileEntries)
                {
                    var resolvedPath = Path.GetFullPath(Path.Combine(destinationFull, entry.Key!));
                    if (!resolvedPath.StartsWith(destinationFull, StringComparison.OrdinalIgnoreCase))
                    {
                        GravitonNotify.Add(new GravitonNotification("graviton.unpacksave.failed", Loc.GetString("ArchiveResolvesOutside", ("Entry", entry.Key!)), GravitonSeverity.Error));
                        return null;
                    }
                    resolvedPaths.Add(resolvedPath);
                }

                archive.WriteToDirectory(destinationPath, new ExtractionOptions { ExtractFullPath = true, Overwrite = true });

                if (fileEntries.Count > 0 && !Directory.EnumerateFileSystemEntries(destinationPath).Any())
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.unpacksave.failed", Loc.GetString("ExtractionEmpty"), GravitonSeverity.Error));
                    return null;
                }

                var sourcePaths = resolvedPaths.Where(File.Exists).ToList();

                try
                {
                    File.Delete(tempSaveLocation);
                }
                catch (Exception ex)
                {
                    GravitonPlugin.Logger.Warn(ex, $"Extraction succeeded but failed to delete temp file {tempSaveLocation}");
                }

                return sourcePaths;
            }
            catch (Exception ex)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.unpacksave.failed", Loc.GetString("FailedUnpack", ("SaveLoc", tempSaveLocation)), GravitonSeverity.Error, ex));
                return null;
            }
        }

        private static bool PackSave(List<string> sourcePaths, string savePathRoot, string outputArchivePath)
        {
            try
            {
                using var archive = ZipArchive.CreateArchive();

                foreach (var path in sourcePaths)
                {
                    var processedpath = path.Replace(EmulatorMapping.MappingPathToken, savePathRoot);

                    if (File.Exists(processedpath))
                    {
                        var relativeKey = Path.GetRelativePath(savePathRoot, processedpath).Replace('\\', '/');

                        var stream = File.OpenRead(processedpath);
                        var lastModified = File.GetLastWriteTimeUtc(processedpath);

                        archive.AddEntry(relativeKey, stream, closeStream: true, size: stream.Length, modified: lastModified);
                    }
                    else if (Directory.Exists(processedpath))
                    {
                        foreach (var file in Directory.GetFiles(processedpath, "*", SearchOption.AllDirectories))
                        {
                            var relativeKey = Path.GetRelativePath(savePathRoot, file).Replace('\\', '/');

                            var stream = File.OpenRead(file);
                            var lastModified = File.GetLastWriteTimeUtc(file);

                            archive.AddEntry(relativeKey, stream, closeStream: true, size: stream.Length, modified: lastModified);
                        }
                    }
                    else
                    {
                        GravitonPlugin.Logger.Warn($"Selected save path no longer exists, skipping: {processedpath}");
                    }
                }

                archive.SaveTo(outputArchivePath, CompressionType.Deflate);
            }
            catch (Exception ex)
            {
                GravitonPlugin.Logger.Error(ex, $"Failed to pack save archive to {outputArchivePath}");
                return false;
            }

            return true;
        }

        public static string ComputePackedContentHash(string zipPath)
        {
            var entryHashes = new List<string>();

            using (var archive = ZipArchive.OpenArchive(zipPath))
            {
                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory).OrderBy(e => e.Key, StringComparer.Ordinal))
                {
                    using (var md5 = MD5.Create())
                    using (var entryStream = entry.OpenEntryStream())
                    {
                        var hash = md5.ComputeHash(entryStream);
                        var hex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                        entryHashes.Add($"{entry.Key}:{hex}");
                    }
                }
            }

            var combined = string.Join("\n", entryHashes);
            using (var md5 = MD5.Create())
            {
                var combinedHash = md5.ComputeHash(Encoding.UTF8.GetBytes(combined));
                return BitConverter.ToString(combinedHash).Replace("-", "").ToLowerInvariant();
            }
        }

        public static string ComputeFileContentHash(string path)
        {
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(path))
            {
                var hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
        #endregion

    }
}
