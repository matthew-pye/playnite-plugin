using Graviton.Models;
using Graviton.Models.Notifications;
using Graviton.Models.RomM.Rom;
using Graviton.Models.RomM.Saves;
using Graviton.Models.Saves;

using Playnite;

using SharpCompress.Archives;

using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Graviton.Saves
{
    internal static class SaveManager
    {
        private static GravitonPlugin _plugin => GravitonPlugin.Instance;
        private static IPlayniteApi PlayniteAPI => GravitonPlugin.PlayniteApi;

        public static async Task<GravitonSave> Upload(GravitonSave save, bool overwrite = false, byte[]? screenshot = null)
        {
            if (GameSessionHandler.IsAGameRunning)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.sync.cannotstart", "Cannot do save sync operations as a game is currently running!", GravitonSeverity.Info));
                return save;
            }

            var rom = _plugin.ImportedGames.FirstOrDefault(x => x.Value.Id == save.ROMID).Value;
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

            bool isPacked = false;
            string savePath = save.SourceFilePaths[0].Replace(EmulatorMapping.MappingPathToken, mapping.SavePath);
            
            if (save.SourceFilePaths.Count > 1 || (!File.Exists(savePath) && Directory.Exists(savePath)))
            {
                savePath = $"{_plugin.PluginDataPath}/temp/{save.Filename}";
                isPacked = true;

                List<string>? skippedPaths = null;
                if (!SaveHelpers.PackSave(save.SourceFilePaths, mapping.SavePath, savePath, out skippedPaths))
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.upload.failed", "Failed to pack save, skipping upload", GravitonSeverity.Error));
                    return save;
                }

                if (skippedPaths != null && skippedPaths.Count > 0)
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.paths.skipped", "One or more paths were skipped when packing save, skipping upload", GravitonSeverity.Error));
                    return save;
                }
            }
            else if(save.SourceFilePaths.Count == 1 && !File.Exists(savePath) && !Directory.Exists(savePath))
            {
                GravitonNotify.Add(new GravitonNotification("graviton.files.missing", "Save files are missing, skipping upload", GravitonSeverity.Error));
                return save;
            }

            var savebytes = File.ReadAllBytes(savePath);
            if (savebytes.Length < 1)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.save.zerobytes", $"The save file for {rom.Name} has 0 bytes, skipping sync", GravitonSeverity.Error));
                return save;
            }

            if (save.SaveID != -1)
                await UntrackSave(save.SaveID);

            var content = new MultipartFormDataContent();
            
            var savecontent = new ByteArrayContent(savebytes);
            savecontent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(savecontent, "saveFile", Path.GetFileName(savePath));

            var response = await HttpClientSingleton.RomMRawPostContentAsync($"/api/saves?rom_id={rom.Id}&slot={save.Slot}&autocleanup={_plugin.Settings.AutoCleanupSaves}&autocleanup_limit={_plugin.Settings.AutoCleanupSavesLimit}&device_id={_plugin.Settings.AccountState.DeviceID}&overwrite={overwrite}", content);

            if (response?.Status == HttpStatusCode.Conflict)
            {
                var autoConflictResolve = await AutoConflictResolve(save, rom, savePath, isPacked);
                
                if (autoConflictResolve == SaveSyncStatus.conflict)
                    autoConflictResolve = SaveNegotiator.ResolveConflict(save);

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

                if(screenshot != null)
                {
                    content = new MultipartFormDataContent();

                    savecontent = new ByteArrayContent(screenshot);
                    savecontent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    content.Add(savecontent, "screenshotFile", (Path.GetFileNameWithoutExtension(result.FileName!) + ".jpg"));

                    _ = await HttpClientSingleton.RomMPutContentAsync($"/api/saves/{result.ID}?device_id={_plugin.Settings.AccountState.DeviceID}", content);

                }

                if (save.HistoricSaves == null)
                    save.HistoricSaves = new();

                var savecopy = JsonSerializer.Deserialize<GravitonSave>(JsonSerializer.Serialize(save));
                if(savecopy != null)
                {
                    // Dont add historic save if the historic save list already contains this save just update it
                    var historicsave = save.HistoricSaves.FirstOrDefault(x => x.SaveID == savecopy.SaveID);
                    if (historicsave == null)
                    {
                        savecopy.IsCurrent = false;
                        savecopy.IsHistoric = true;
                        save.HistoricSaves.Add(savecopy);
                    }
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

                save.MissingFiles = new();

                rom.LocalSave = save;
                rom.Save();

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
            if (GameSessionHandler.IsAGameRunning)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.sync.cannotstart", "Cannot do save sync operations as a game is currently running!", GravitonSeverity.Info));
                return save;
            }

            var rom = _plugin.ImportedGames.FirstOrDefault(x => x.Value.Id == save.ROMID).Value;
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
                var paths = SaveHelpers.UnpackSave(tempDir, mapping.SavePath);
                if(paths == null)
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.download.failed", "Failed to unpack save data, skipping download", GravitonSeverity.Error));
                    return save;
                }

                save.SourceFilePaths = paths;
                save.SourceFilePaths = save.SourceFilePaths.Select(x => x.Replace(mapping.SavePath, EmulatorMapping.MappingPathToken)).ToObservableCollection();
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
            if (GameSessionHandler.IsAGameRunning)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.sync.cannotstart", "Cannot do save sync operations as a game is currently running!", GravitonSeverity.Info));
                return save;
            }

            var rom = _plugin.ImportedGames.FirstOrDefault(x => x.Value.Id == save.ROMID).Value;
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

            if(save.SaveID != -1)
            {
                var result = await PlayniteAPI.Dialogs.ShowMessageAsync($"A save is already being tracked for this game, Do you want to replace the save being tracked?\n\nSlot:{save.Slot}\nFilename:{save.Filename}", "Existing Save!", MessageBoxButtons.YesNo, MessageBoxSeverity.Warning);
                if(result == Playnite.MessageBoxResult.No)
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.download.failed", "A save is already being tracked, Skipping download", GravitonSeverity.Info));
                    return save;
                }

                await UntrackSave(save.SaveID);
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

                var paths = SaveHelpers.UnpackSave(tempDir, savepath);
                if (paths == null)
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.download.failed", "Failed to unpack save data, skipping download", GravitonSeverity.Error));
                    return save;
                }

                save.SourceFilePaths = paths.Select(x => x.Replace(mapping.SavePath, EmulatorMapping.MappingPathToken)).ToObservableCollection();
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
            var rom = _plugin.ImportedGames.FirstOrDefault(x => x.Value.Id == save.ROMID).Value;
            if (rom == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.download.failed", "Failed to find ROM that matches save, skipping download", GravitonSeverity.Error));
                return null;
            }

            if(rom.LocalSave != null)
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
                var paths = SaveHelpers.UnpackSave(tempDir, mapping.SavePath);
                if (paths == null)
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.download.failed", "Failed to unpack save data, skipping download", GravitonSeverity.Error));
                    return null;
                }

                newsave.SourceFilePaths = paths;
                newsave.SourceFilePaths = newsave.SourceFilePaths.Select(x => x.Replace(mapping.SavePath, EmulatorMapping.MappingPathToken)).ToObservableCollection();
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

        public static async Task CheckRestoredSaveNeedUploading(RomMRomLocal rom, byte[]? screenshot = null)
        {
            if (rom.LocalSave == null)
                return;

            var negotiate = SaveNegotiator.BuildNegotiate(new() { rom });
            if (negotiate.Saves.Count <= 0) // Nothing to sync
            {
                GravitonPlugin.Logger.Error("[SaveManager] No saves in negotiate, skipping!");
                return;
            }

            if (rom.LocalSave.LastSyncedContentHash != negotiate.Saves[0].ContentHash)
            {
                var result = await Upload(rom.LocalSave, false, screenshot);
                if(result.Status == SaveStatus.Synced)
                {
                    rom.LocalSave.IsTempRestored = false;
                    rom.Save();
                }
            }
            else
            {
                rom.LocalSave.IsTempRestored = false;
                rom.Save();
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
            localHash = isPacked ? SaveHelpers.ComputePackedContentHash(localFilePath) : SaveHelpers.ComputeFileContentHash(localFilePath);

            if (localHash == null)
            {
                return SaveSyncStatus.conflict;
            }

            if (isPacked) 
                File.Delete(localFilePath); 
        
            var negotiate = SaveNegotiator.BuildNegotiate(new() { rom });
            var negotiateResponse = negotiate.Saves.Count > 0 ? await SaveNegotiator.Negotiate(negotiate) : null;
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

    }
}
