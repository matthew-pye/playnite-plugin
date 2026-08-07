using Graviton.Models;
using Graviton.Models.Notifications;
using Graviton.Models.RomM;
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
    internal class SaveManager
    {
        private GravitonPlugin _plugin;
        private IPlayniteApi _playniteAPI;
        private ILogger _logger;
        private IRomMServer _romMServer;
        private SaveController SaveController => _plugin.SaveController!;

        public SaveManager(GravitonPlugin plugin, IPlayniteApi playniteAPI, ILogger logger, IRomMServer romMServer)
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;
            _romMServer = romMServer;
        }

        public async Task<GravitonSave> Upload(GravitonSave save, bool overwrite = false, byte[]? screenshot = null, RomMNegotiateOperations? operation = null)
        {
            if (_plugin.GameSessionHandlers.Count() > 0)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.sync.cannotstart", Loc.GetString("SyncCannotStart"), GravitonSeverity.Info));
                return save;
            }

            var rom = _plugin.ImportedGames.FirstOrDefault(x => x.Value.Id == save.ROMID).Value;
            if(rom == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.upload.failed", Loc.GetString("UploadROMNotFound"), GravitonSeverity.Error));
                return save;
            }

            var mapping = _plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == rom.MappingID);
            if (mapping == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.upload.failed", Loc.GetString("UploadMappingNotFound"), GravitonSeverity.Error));
                return save;
            }

            bool isPacked = false;
            string savePath = save.SourceFilePaths[0].Replace(EmulatorMapping.SavePathToken, mapping.SavePath);
            
            if (save.SourceFilePaths.Count > 1 || (!File.Exists(savePath) && Directory.Exists(savePath)))
            {
                savePath = $"{_plugin.PluginDataPath}/temp/{save.Filename}";
                isPacked = true;

                List<string>? skippedPaths = null;
                if (!SaveHelpers.PackSave(save.SourceFilePaths, mapping.SavePath, savePath, out skippedPaths))
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.upload.failed", Loc.GetString("UploadPackFailed"), GravitonSeverity.Error));
                    return save;
                }

                if (skippedPaths != null && skippedPaths.Count > 0)
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.paths.skipped", Loc.GetString("UploadPathsSkipped"), GravitonSeverity.Error));
                    return save;
                }
            }
            else if(save.SourceFilePaths.Count == 1 && !File.Exists(savePath) && !Directory.Exists(savePath))
            {
                GravitonNotify.Add(new GravitonNotification("graviton.files.missing", Loc.GetString("UploadFilesMissing"), GravitonSeverity.Error));
                return save;
            }

            var savebytes = File.ReadAllBytes(savePath);
            if (savebytes.Length < 1)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.save.zerobytes", Loc.GetString("SaveFileZeroBytes", ("GameName", rom.Name!)), GravitonSeverity.Error));
                return save;
            }

            if (save.SaveID != -1)
                await UntrackSave(save.SaveID);

            var content = new MultipartFormDataContent();
            
            var savecontent = new ByteArrayContent(savebytes);
            savecontent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(savecontent, "saveFile", Path.GetFileName(savePath));

            var response = await _romMServer.RawPOSTAsync($"/api/saves?rom_id={rom.Id}&slot={save.Slot}&autocleanup={_plugin.Settings.AutoCleanupSaves}&autocleanup_limit={_plugin.Settings.AutoCleanupSavesLimit}&device_id={_plugin.Settings.AccountState.DeviceID}&overwrite={overwrite}", content);

            if (response?.Status == HttpStatusCode.Conflict)
            {
                var autoConflictResolve = await AutoConflictResolve(save, rom, savePath, isPacked, operation);
                
                if (autoConflictResolve == SaveSyncStatus.conflict)
                    autoConflictResolve = SaveController.Negotiator.ResolveConflict(save);

                switch (autoConflictResolve)
                {
                    case SaveSyncStatus.upload:
                        return await Upload(save, true, null, operation);
                    case SaveSyncStatus.download:
                        return await Download(save);
                    default:
                        GravitonNotify.Add(new GravitonNotification("graviton.upload.failed", Loc.GetString("UploadConflictResolveFailed"), GravitonSeverity.Error));
                        rom.LocalSave!.Status = SaveStatus.Conflicted;
                        rom.Save();
                        return save;
                }
            }

            if (isPacked)
                File.Delete(savePath);

            if (response?.Status != HttpStatusCode.OK || response.Content == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.upload.failed", Loc.GetString("UploadServerFailed"), GravitonSeverity.Error));
                return save;
            }

            try
            {
                var stringresponse = new StreamReader(await response.Content.ReadAsStreamAsync());
                var result = JsonSerializer.Deserialize<RomMSave>(stringresponse.ReadToEnd());
                if (result == null)
                    throw new Exception();

                if(screenshot != null)
                {
                    content = new MultipartFormDataContent();

                    savecontent = new ByteArrayContent(screenshot);
                    savecontent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    content.Add(savecontent, "screenshotFile", (Path.GetFileNameWithoutExtension(result.FileName!) + ".jpg"));

                    _ = await _romMServer.PUTAsync($"/api/saves/{result.ID}?device_id={_plugin.Settings.AccountState.DeviceID}", content);

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

                GravitonNotify.Add(new GravitonNotification("graviton.upload.success", Loc.GetString("SaveUploadSuccess", ("GameName", rom.Name!), ("Size", save.FileSizeString)), GravitonSeverity.Success));
                return save;
            }
            catch (Exception)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.upload.failed", Loc.GetString("DeserializeResponseFailed"), GravitonSeverity.Error));
                return save;
            }
        }

        public async Task<GravitonSave> Download(GravitonSave save, bool skipSavingROM = false)
        {
            if (_plugin.GameSessionHandlers.Count() > 0)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.sync.cannotstart", Loc.GetString("SyncCannotStart"), GravitonSeverity.Info));
                return save;
            }

            var rom = _plugin.ImportedGames.FirstOrDefault(x => x.Value.Id == save.ROMID).Value;
            if (rom == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.download.failed", Loc.GetString("DownloadROMNotFound"), GravitonSeverity.Error));
                return save;
            }

            var mapping = _plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == rom.MappingID);
            if (mapping == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.download.failed", Loc.GetString("DownloadMappingNotFound"), GravitonSeverity.Error));
                return save;
            }

            var savedata = await _romMServer.RawGETAsync($"/api/saves/{save.SaveID}/content?device_id={_plugin.Settings.AccountState.DeviceID}&optimistic=false");
            if (savedata == null || savedata.Status != HttpStatusCode.OK)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.download.failed", Loc.GetString("DownloadServerDataFailed"), GravitonSeverity.Error));
                return save;
            }

            if (!Directory.Exists($"{_plugin.PluginDataPath}/temp/"))
                Directory.CreateDirectory($"{_plugin.PluginDataPath}/temp/");

            var tempDir = $"{_plugin.PluginDataPath}/temp/{save.Filename}";
            
            using var ms = new MemoryStream();
            savedata.Content!.ReadAsStream().CopyTo(ms);
            File.WriteAllBytes(tempDir, ms.ToArray());

            if (ArchiveFactory.IsArchive(tempDir, out _))
            {
                // Check downloaded file matches the server hash
                var downloadedHash = SaveHelpers.ComputePackedContentHash(tempDir);
                if(downloadedHash != save.ServerHash)
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.download.failed", Loc.GetString("DownloadHashFailed"), GravitonSeverity.Error));
                    return save;
                }

                var paths = SaveHelpers.UnpackSave(tempDir, mapping.SavePath);
                if(paths == null)
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.download.failed", Loc.GetString("DownloadUnpackFailed"), GravitonSeverity.Error));
                    return save;
                }

                save.SourceFilePaths = paths;
                save.SourceFilePaths = save.SourceFilePaths.Select(x => x.Replace(mapping.SavePath, EmulatorMapping.SavePathToken)).ToObservableCollection();
            }
            else
            {
                var downloadedHash = SaveHelpers.ComputeFileContentHash(tempDir);
                if (downloadedHash != save.ServerHash)
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.download.failed", Loc.GetString("DownloadHashFailed"), GravitonSeverity.Error));
                    return save;
                }

                var savelocation = save.SourceFilePaths[0].Replace(EmulatorMapping.SavePathToken, mapping.SavePath);
                File.Move(tempDir, savelocation, true);
            }

            save.LastSyncedAt = save.ServerLastUpdatedAt!.Value;
            save.ContentHash = save.ServerHash;
            save.LastSyncedContentHash = save.ContentHash;
            save.FileSize = ms.Length;
            save.Status = SaveStatus.Synced;
            
            if(!skipSavingROM)
            {
                rom.LocalSave = save;
                rom.Save();
            }

            var deviceid = new { device_id = _plugin.Settings.AccountState.DeviceID };
            await _romMServer.POSTAsync($"/api/saves/{save.SaveID}/downloaded", deviceid);
            
            GravitonNotify.Add(new GravitonNotification("graviton.download.success", Loc.GetString("SaveDownloadSuccess", ("GameName", rom.Name!), ("Size", save.FileSizeString)), GravitonSeverity.Success));
            return save;
        }

        public async Task<GravitonSave> TrackNewRemoteSave(GravitonSave save)
        {
            if (_plugin.GameSessionHandlers.Count() > 0)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.sync.cannotstart", Loc.GetString("SyncCannotStart"), GravitonSeverity.Info));
                return save;
            }

            var rom = _plugin.ImportedGames.FirstOrDefault(x => x.Value.Id == save.ROMID).Value;
            if (rom == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.download.failed", Loc.GetString("DownloadROMNotFound"), GravitonSeverity.Error));
                return save;
            }

            var mapping = _plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == rom.MappingID);
            if (mapping == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.download.failed", Loc.GetString("DownloadMappingNotFound"), GravitonSeverity.Error));
                return save;
            }

            if(rom.LocalSave != null && rom.LocalSave.SaveID != -1)
            {
                var result = await _playniteAPI.Dialogs.ShowMessageAsync(Loc.GetString("ExistingSaveConfirm", ("Slot", save.Slot!), ("Filename", save.Filename)), Loc.GetString("ExistingSaveTitle"), MessageBoxButtons.YesNo, MessageBoxSeverity.Warning);
                if(result == Playnite.MessageBoxResult.No)
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.download.failed", Loc.GetString("SaveAlreadyTrackedDownload"), GravitonSeverity.Info));
                    return save;
                }

                await UntrackSave(save.SaveID);
            }

            var savedata = await _romMServer.RawGETAsync($"/api/saves/{save.SaveID}/content?device_id={_plugin.Settings.AccountState.DeviceID}&optimistic=false");
            if (savedata == null || savedata.Status != HttpStatusCode.OK)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.download.failed", Loc.GetString("DownloadServerDataFailed"), GravitonSeverity.Error));
                return save;
            }

            if (!Directory.Exists($"{_plugin.PluginDataPath}/temp/"))
                Directory.CreateDirectory($"{_plugin.PluginDataPath}/temp/");

            var tempDir = $"{_plugin.PluginDataPath}/temp/{save.Filename}";

            using var ms = new MemoryStream();
            savedata.Content!.ReadAsStream().CopyTo(ms);
            File.WriteAllBytes(tempDir, ms.ToArray());

            if (ArchiveFactory.IsArchive(tempDir, out _))
            {
                string savepath = mapping.SavePath;

                var result = await _playniteAPI.Dialogs.ShowMessageAsync(Loc.GetString("SaveLocationConfirm", ("Path", savepath)), Loc.GetString("SaveLocationTitle"), MessageBoxButtons.YesNo, MessageBoxSeverity.Warning);
                if (result == Playnite.MessageBoxResult.No)
                {
                    var savepaths = await _playniteAPI.Dialogs.SelectFolderAsync(savepath);
                    if(savepaths == null || savepaths.Count < 1)
                    {
                        GravitonNotify.Add(new GravitonNotification("graviton.download.failed", Loc.GetString("DownloadExtractionPathFailed"), GravitonSeverity.Error));
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
                    GravitonNotify.Add(new GravitonNotification("graviton.download.failed", Loc.GetString("DownloadUnpackFailed"), GravitonSeverity.Error));
                    return save;
                }

                save.SourceFilePaths = paths.Select(x => x.Replace(mapping.SavePath, EmulatorMapping.SavePathToken)).ToObservableCollection();
            }
            else
            {
                var savelocation = save.SourceFilePaths[0].Replace(EmulatorMapping.SavePathToken, mapping.SavePath);
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
            await _romMServer.POSTAsync($"/api/saves/{save.SaveID}/downloaded", deviceid);

            GravitonNotify.Add(new GravitonNotification("graviton.download.success", Loc.GetString("SaveDownloadSuccess", ("GameName", rom.Name!), ("Size", save.FileSizeString)), GravitonSeverity.Success));
            return save;
        }

        public async Task<GravitonSave> TrackNewLocalSave(GravitonSave save)
        {
            return await Upload(save);
        }

        public async Task<GravitonSave?> DownloadArchivedSave(RomMSave save)
        {
            var rom = _plugin.ImportedGames.FirstOrDefault(x => x.Value.Id == save.ROMID).Value;
            if (rom == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.download.failed", Loc.GetString("DownloadROMNotFound"), GravitonSeverity.Error));
                return null;
            }

            if(rom.LocalSave != null)
            {
                var result = await _playniteAPI.Dialogs.ShowMessageAsync(Loc.GetString("ReplaceSaveConfirm", ("GameName", rom.Name!)), Loc.GetString("ReplaceSaveTitle"), MessageBoxButtons.YesNo);
                if(result == Playnite.MessageBoxResult.No)
                {
                    return null;
                }
            }

            var mapping = _plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == rom.MappingID);
            if (mapping == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.download.failed", Loc.GetString("DownloadMappingNotFound"), GravitonSeverity.Error));
                return null;
            }

            var savedata = await _romMServer.RawGETAsync($"/api/saves/{save.ID}/content?device_id={_plugin.Settings.AccountState.DeviceID}&optimistic=false");
            if (savedata == null || savedata.Status != HttpStatusCode.OK)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.download.failed", Loc.GetString("DownloadServerDataFailed"), GravitonSeverity.Error));
                return null;
            }

            if (!Directory.Exists($"{_plugin.PluginDataPath}/temp/"))
                Directory.CreateDirectory($"{_plugin.PluginDataPath}/temp/");

            var tempDir = $"{_plugin.PluginDataPath}/temp/{save.FileName}";

            using var ms = new MemoryStream();
            savedata.Content!.ReadAsStream().CopyTo(ms);
            File.WriteAllBytes(tempDir, ms.ToArray());

            GravitonSave newsave = new GravitonSave();
            newsave.ROMID = rom.Id;
            newsave.IsCurrent = true;

            if (ArchiveFactory.IsArchive(tempDir, out _))
            {
                var paths = SaveHelpers.UnpackSave(tempDir, mapping.SavePath);
                if (paths == null)
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.download.failed", Loc.GetString("DownloadUnpackFailed"), GravitonSeverity.Error));
                    return null;
                }

                newsave.SourceFilePaths = paths;
                newsave.SourceFilePaths = newsave.SourceFilePaths.Select(x => x.Replace(mapping.SavePath, EmulatorMapping.SavePathToken)).ToObservableCollection();
            }
            else
            {
                var savelocation = Path.Combine(mapping.SavePath, save.FileName!);
                File.Move(tempDir, savelocation, true);
                newsave.SourceFilePaths = new() { savelocation.Replace(mapping.SavePath, EmulatorMapping.SavePathToken) };
            }

            newsave.Filename = save.FileName!;
            newsave.LastSyncedAt = DateTime.Parse(save.UpdatedAt!);
            newsave.ContentHash = save.ContentHash;
            newsave.LastSyncedContentHash = save.ContentHash;
            newsave.FileSize = ms.Length;
            newsave.Status = SaveStatus.LocalNewer;

            var deviceid = new { device_id = _plugin.Settings.AccountState.DeviceID };
            await _romMServer.POSTAsync($"/api/saves/{save.ID}/downloaded", deviceid);

            return await TrackNewLocalSave(newsave);
        }

        public async Task CheckRestoredSaveNeedUploading(RomMRomLocal rom, byte[]? screenshot = null)
        {
            if (rom.LocalSave == null)
                return;


            var negotiate = SaveController.Negotiator.BuildNegotiate(new() { rom });
            if (negotiate.Saves.Count <= 0)
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

        public async Task UntrackSave(int saveID)
        {
            var deviceid = new { device_id = _plugin.Settings.AccountState.DeviceID };
            await _romMServer.POSTAsync($"/api/saves/{saveID}/untrack", deviceid);
        }

        private async Task<SaveSyncStatus> AutoConflictResolve(GravitonSave save, RomMRomLocal rom, string localFilePath, bool isPacked, RomMNegotiateOperations? operation = null)
        {
            string? localHash = null;
            localHash = isPacked ? SaveHelpers.ComputePackedContentHash(localFilePath) : SaveHelpers.ComputeFileContentHash(localFilePath);

            if (localHash == null)
            {
                return SaveSyncStatus.conflict;
            }

            if (isPacked) 
                File.Delete(localFilePath); 
        
            // Skip negotaiting save if a negotaition operation is already under way
            if(operation == null)
            {
                var negotiate = SaveController.Negotiator.BuildNegotiate(new() { rom });
                var negotiateResponse = negotiate.Saves.Count > 0 ? await SaveController.Negotiator.Negotiate(negotiate) : null;
                operation = negotiateResponse?.Operations.FirstOrDefault(x => x.ROMID == rom.Id && x.Slot == save.Slot);
            }

            string? serverHeadHash = operation?.ServerContentHash;
            if (operation != null)
            {
                save.ServerHash = operation.ServerContentHash;
                save.SaveID = operation.SaveID;
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
