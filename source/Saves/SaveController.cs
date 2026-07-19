using Graviton.Models;
using Graviton.Models.Notifications;
using Graviton.Models.RomM;
using Graviton.Models.RomM.Rom;
using Graviton.Models.RomM.Saves;

using Playnite;

using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;

using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Graviton.Saves
{
    internal class SaveController
    {
        private GravitonPlugin _plugin;
        private IPlayniteApi _playniteAPI;
        private ILogger _logger;

        private string GetSaveFileName(string gamename, LocalSave save) => save.SourceFilePaths.Count == 1 && Path.HasExtension(save.SourceFilePaths[0]) ? Path.GetFileName(save.SourceFilePaths[0]) : $"{gamename}-{save.SaveID}.rommsave.zip";
        private static readonly Regex ServerTimestampTagPattern = new(@"[ _]?\[\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}\](?=\.[^.]+$)", RegexOptions.Compiled);

        private List<MessageBoxResponse> ConflictMessageBoxResponses = new();


        public SaveController(GravitonPlugin plugin, IPlayniteApi playniteAPI, ILogger logger)
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;

            ConflictMessageBoxResponses = new List<MessageBoxResponse>
            {
                new MessageBoxResponse(Loc.GetString("UseRemote")),
                new MessageBoxResponse(Loc.GetString("UseLocal")),
                new MessageBoxResponse(Loc.GetString("Skip"),  isDefault: true, isCancel: true),
            };
        }

        public async Task<List<SaveRow>?> GetLocalSaves(EmulatorMapping mapping)
        {
            var roms = _plugin.ImportedGames!.Where(x => x.Value.MappingID == mapping.MappingId).Select(y => y.Value);

            var negotiate = BuildNegotiate(mapping, roms.SelectMany(x => x.Saves).ToList(), _plugin.ImportedGames!.Where(x => x.Value.MappingID == mapping.MappingId).Select(y => y.Value).ToList());
            
            if (negotiate.Saves.Count <= 0)
                return null;

            var response = await HttpClientSingleton.RomMPostJsonAsync("/api/sync/negotiate", negotiate);
            if (response == null)
                return null;

            try
            {
                var result = JsonSerializer.Deserialize<RomMNegotiateResponse>(response);
                if (result == null)
                    return null;

                List<SaveRow> saverows = new();


                foreach (var save in result.Operations)
                {
                    var rom = roms.First(x => x.Id == save.ROMID);
                    var savefilename = ServerTimestampTagPattern.Replace(save.FileName!, "");
                    LocalSave? localSave = rom.Saves.FirstOrDefault(y => y.SaveID == save.SaveID);

                    if (localSave == null)
                    {
                        localSave = rom.Saves.FirstOrDefault(y => GetSaveFileName(rom.Name!, y) == savefilename);
                        if(localSave != null)
                        {
                            localSave.SaveID = save.SaveID != null ? (int)save.SaveID : -1;
                            string json = JsonSerializer.Serialize(rom);
                            File.WriteAllText($"{_plugin.PluginDataPath}/Games/{rom.SHA1}.json", json);
                        }
                    }

                    var row = new SaveRow
                    {
                        LocalSaveGuid = localSave?.LocalID,
                        GameName = rom.Name!,
                        GameID = rom.Id,
                        SaveID = save.SaveID == null ? -1 : (int)save.SaveID,
                        SyncStatus = Enum.Parse<SaveSyncStatus>(save.Action!),
                        SyncEnabled = localSave?.Enabled ?? true
                    };

                    row.BuildSaveDirectoryView(mapping.SavePath, localSave?.SourceFilePaths ?? new List<string>());

                    saverows.Add(row);
                }

                return saverows;
            }
            catch (Exception ex)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.getlocalsaves.failed", Loc.GetString("FailedGetSaves", ("Error", ex.Message)), GravitonSeverity.Error, ex));
                _logger.Error(response.RootElement.ToString());
                return null; 
            }  

        }

        public async Task<List<SaveRow>?> GetRemoteSaves(EmulatorMapping mapping)
        {
            var response = await HttpClientSingleton.RomMGetAsync($"/api/saves?platform_id={mapping.RomMPlatformId}&device_id={_plugin.Settings.AccountState.DeviceID}");
            if (response == null)
                return null;

            var result = JsonSerializer.Deserialize<List<RomMSave>>(response);
            if (result == null)
                return null;

            var roms = _plugin.ImportedGames!.Where(x => x.Value.MappingID == mapping.MappingId).Select(y => y.Value);

            List<SaveRow> saverows = new();
            foreach (var save in result)
            {
                var rom = roms.FirstOrDefault(x => x.Id == save.ROMID);
                if(rom == null)
                {
                    _logger.Warn($"Failed to find rom that matched ROM ID:{save.ROMID} for save ID:{save.ID}!");
                    continue;
                }

                var row = new SaveRow()
                {
                    LocalSaveGuid = null,
                    GameName = rom.Name!,
                    GameID = save.ROMID,
                    SaveID = save.ID,
                    SyncStatus = SaveSyncStatus.download,
                    Slot = save.Slot
                };

                var filename = ServerTimestampTagPattern.Replace(save.FileName!, "");

                row.SaveDirectoryView.Add(new DirectorySaveFile
                {
                    Name = filename!,
                    FullPath = $"{{MappingSavePath}}\\{filename}"              
                });

                saverows.Add(row);
            }

            return saverows;

        }

        public async Task<List<SaveRow>?> FindUntrackedAutoDetectSaves(EmulatorMapping mapping, List<SaveRow> LocalSaves)
        {
            if (mapping.FindSaveLayout == SaveLayoutStyle.Disabled)
                return null;

            List<SaveRow> saverows = new List<SaveRow>();
            var roms = _plugin.ImportedGames!.Where(x => x.Value.MappingID == mapping.MappingId).Select(y => y.Value);

            if (mapping.FindSaveLayout == SaveLayoutStyle.WholeFolder)
            {
                foreach (var dir in Directory.EnumerateDirectories(mapping.SavePath, "*", SearchOption.AllDirectories))
                {
                    var foundrom = roms.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x.FileName) == Path.GetFileName(dir));
                    if (foundrom == null)
                        continue;

                    SaveRow row = new SaveRow
                    { 
                        LocalSaveGuid = null,
                        GameName = foundrom.Name!,
                        GameID = foundrom.Id,
                        SyncStatus = SaveSyncStatus.upload,
                        SourcePaths = new List<string> { dir },
                    };

                    row.SaveDirectoryView.Add(new DirectorySaveFile
                    {
                        Name = Path.GetFileName(dir)!,
                        FullPath = dir
                    });

                    saverows.Add(row);
                }

                return saverows;
            }

            var extentions = mapping.FindSaveFileExtensions.Split(';');
            if (extentions == null)
                return null;
     
            foreach (var rom in roms)
            {
                var files = Directory.EnumerateFiles(mapping.SavePath, $"{Path.GetFileNameWithoutExtension(rom.FileName)}.*", SearchOption.AllDirectories).ToList();
                if (files != null && files.Count > 0)
                {
                    foreach (var file in files.ToList())
                    {
                        if (LocalSaves.SelectMany(x => x.SaveDirectoryView).Any(y => y.FullPath == file))
                            files.Remove(file);

                        if (!extentions.Any(x => file.EndsWith(x)))
                            files.Remove(file);
                    }

                    if (files == null || files.Count <= 0)
                        continue;

                    if(mapping.FindSaveLayout == SaveLayoutStyle.SingleFile)
                    {
                        foreach (var file in files)
                        {
                            SaveRow row = new SaveRow
                            {
                                LocalSaveGuid = null,
                                GameName = rom.Name!,
                                GameID = rom.Id,
                                SyncStatus = SaveSyncStatus.upload,
                                SourcePaths = new List<string> { file },
                            };

                            row.SaveDirectoryView.Add(new DirectorySaveFile
                            {
                                Name = Path.GetFileName(file),
                                FullPath = file,
                            });
                            saverows.Add(row);
                        }
                    }
                    else
                    {
                        SaveRow row = new SaveRow
                        {
                            LocalSaveGuid = null,
                            GameName = rom.Name!,
                            GameID = rom.Id,
                            SyncStatus = SaveSyncStatus.upload,
                            SourcePaths = files,
                        };

                        row.BuildSaveDirectoryView(mapping.SavePath, files);
                        saverows.Add(row);
                    }

                }
            }

            return saverows;           
        }

        public async Task<SaveRow?> DownloadNewSave(EmulatorMapping mapping, SaveRow newsave)
        {
            if (newsave.Slot == null)
            {
                var response = await HttpClientSingleton.RomMGetAsync($"/api/saves/{newsave.SaveID}?device_id={_plugin.Settings.AccountState.DeviceID}");
                if (response == null)
                    return null;

                var result = JsonSerializer.Deserialize<RomMSave>(response);
                if (result == null)
                    return null;

                var copysavedata = await HttpClientSingleton.RomMGetByteArrayAsync($"/api/saves/{newsave.SaveID}/content?device_id={_plugin.Settings.AccountState.DeviceID}&optimistic=true");
                if (copysavedata == null)
                    return null;

                using var content = new MultipartFormDataContent();
                var savecontent = new ByteArrayContent(copysavedata);
                savecontent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Add(savecontent, "saveFile", Path.GetFileName(result.FileName!));

                response = await HttpClientSingleton.RomMPostContentAsync($"/api/saves?rom_id={result.ROMID}&slot=Autosave&device_id={_plugin.Settings.AccountState.DeviceID}", content);
                if (response == null)
                    return null;

                result = JsonSerializer.Deserialize<RomMSave>(response);
                if (result == null)
                    return null;

                newsave.SaveID = result.ID;
                newsave.Slot = result.Slot;
            }

            var tracksave = new { device_id = _plugin.Settings.AccountState.DeviceID };

            var reponse = await HttpClientSingleton.RomMPostJsonAsync($"/api/saves/{newsave.SaveID}/track", tracksave);
            if (reponse == null)
                return null;

            var savedata = await HttpClientSingleton.RomMGetByteArrayAsync($"/api/saves/{newsave.SaveID}/content?device_id={_plugin.Settings.AccountState.DeviceID}&optimistic=false");
            if (savedata == null)
                return null;

            var savepath = newsave.SaveDirectoryView[0].FullPath.Replace("{MappingSavePath}", mapping.SavePath);
            var tempDir = $"{_plugin.PluginDataPath}/temp/{Path.GetFileName(savepath)}";

            File.WriteAllBytes(tempDir, savedata);

            List<string> sourcepaths = new List<string>();

            if (mapping.ExtractArchivedSaves && ArchiveFactory.IsArchive(tempDir, out _))
            {
                var result = UnpackSave(tempDir, mapping.SavePath);
                if (result == null) 
                    return null;

                sourcepaths = result;
            }
            else
            {
                File.Move(tempDir, savepath, overwrite: true);
                sourcepaths.Add(savepath);
            }

            var rom = _plugin.ImportedGames!.FirstOrDefault(x => x.Value.Id == newsave.GameID).Value;
            if (rom == null)
                return null;

            var save = new LocalSave
            {
                Enabled = true,
                SaveID = newsave.SaveID,
                Slot = newsave.Slot,
                PackedFilename = Path.GetFileName(savepath),
            };
            save.SourceFilePaths = sourcepaths;

            rom.Saves.Add(save);
            string json = JsonSerializer.Serialize(rom);
            File.WriteAllText($"{_plugin.PluginDataPath}/Games/{rom.SHA1}.json", json);

            reponse = await HttpClientSingleton.RomMPostJsonAsync($"/api/saves/{newsave.SaveID}/downloaded", tracksave);
            if (reponse == null)
                return null;

            var row = new SaveRow
            {
                GameName = rom.Name!,
                GameID = rom.Id,
                SaveID = save.SaveID!,
                SyncStatus = SaveSyncStatus.no_op,
                SyncEnabled = true, 
                LocalSaveGuid = save.LocalID
            };

            row.BuildSaveDirectoryView(mapping.SavePath, sourcepaths);

            return row;
  
        }

        public async Task<SaveRow?> UploadNewSave(EmulatorMapping mapping, RomMRomLocal rom, List<string> sourceFiles)
        {

            var isPackedTemp = !(sourceFiles.Count == 1 && Path.HasExtension(sourceFiles[0]));
            var uploadfilepath = isPackedTemp ? $"{_plugin.PluginDataPath}/temp/{rom.Name}.rommsave.zip" : sourceFiles[0];

            if (isPackedTemp)
                if (!PackSave(sourceFiles, mapping.SavePath, uploadfilepath))
                    return null;

            using var content = new MultipartFormDataContent();
            var savecontent = new ByteArrayContent(await File.ReadAllBytesAsync(uploadfilepath));
            savecontent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(savecontent, "saveFile", Path.GetFileName(uploadfilepath));

            var response = await HttpClientSingleton.RomMPostContentAsync($"/api/saves?rom_id={rom.Id}&slot=Autosave&autocleanup={_plugin.Settings.AutoCleanupSaves}&autocleanup_limit={_plugin.Settings.AutoCleanupSavesLimit}&device_id={_plugin.Settings.AccountState.DeviceID}", content);
            if (isPackedTemp)
                File.Delete(uploadfilepath);

            if (response == null)
                return null;

            try
            {
                var result = JsonSerializer.Deserialize<RomMSave>(response);
                var save = new LocalSave
                {
                    SourceFilePaths = sourceFiles,
                    SaveID = result!.ID,
                    PackedFilename = $"{rom.Name}.rommsave.zip",
                    Enabled = true
                };
                rom.Saves.Add(save);
                string json = JsonSerializer.Serialize(rom);
                File.WriteAllText($"{_plugin.PluginDataPath}/Games/{rom.SHA1}.json", json);

                var saverow = new SaveRow
                { 
                    GameName = rom.Name!,
                    GameID = rom.Id,
                    SaveID = save.SaveID,
                    SyncStatus = SaveSyncStatus.no_op,
                    SyncEnabled = true,
                    LocalSaveGuid = save.LocalID
                };

                saverow.BuildSaveDirectoryView(mapping.SavePath, sourceFiles);
                return saverow;

            }
            catch (Exception ex)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.uploadnewsave.failed", Loc.GetString("FailedUploadSaves", ("Error", ex.Message)), GravitonSeverity.Error, ex));
                return null;
            }
            
        }

        public async Task UntrackSave(int saveID)
        {
            var deviceid = new { device_id = _plugin.Settings.AccountState.DeviceID };
            await HttpClientSingleton.RomMPostJsonAsync($"/api/saves/{saveID}/untrack", deviceid);
        }

        public async Task<SaveRow?> NegotiateSave(EmulatorMapping mapping, LocalSave save, RomMRomLocal rom)
        {
            var negotiate = BuildNegotiate(mapping, new List<LocalSave> { save }, new List<RomMRomLocal>{ rom });

            var response = await HttpClientSingleton.RomMPostJsonAsync("/api/sync/negotiate", negotiate);
            if (response == null)
                return null;

            try
            {
                var result = JsonSerializer.Deserialize<RomMNegotiateResponse>(response);
                if (result == null)
                    return null;

                var completedOperations = 0;
                var failedOperations = 0;

                foreach (var operation in result.Operations)
                {
                    var savefilename = ServerTimestampTagPattern.Replace(operation.FileName!, "");

                    if (operation.SaveID != save.SaveID)
                        continue;

                    if (operation.Action == "conflict")
                    {
                        switch (_plugin.Settings.SaveConflictStyle)
                        {
                            case SaveConflictStyle.PreferRemote:
                                operation.Action = "download";
                                break;
                            case SaveConflictStyle.PreferLocal:
                                operation.Action = "upload";
                                break;
                            default:
                                var conflictresponse = await GravitonPlugin.PlayniteApi.Dialogs.ShowMessageAsync($"{Loc.GetString("WantKeepSave")}\n{Loc.GetString("Remote")}:\n\t{Loc.GetString("LastModified")}: {operation.UpdatedAt}\n{Loc.GetString("Local")}:\n\t{Loc.GetString("LastModified")}: {save.UpdatedAt}", Loc.GetString("SaveConflict"), MessageBoxSeverity.Question, ConflictMessageBoxResponses, new List<MessageBoxOption>());
                                if (conflictresponse == null || conflictresponse.Title == Loc.GetString("Skip"))
                                    operation.Action = "no_op";
                                else if (conflictresponse.Title == Loc.GetString("UseRemote"))
                                    operation.Action = "download";
                                else if (conflictresponse.Title == Loc.GetString("UseLocal"))
                                    operation.Action = "upload";
                                break;
                        }             
                    }

                    switch (operation.Action)
                    {
                        case "upload":
                            if (await UploadSave(mapping, save, rom))
                                completedOperations++;
                            else
                                return null;
                            break;

                        case "download":
                            if (await DownloadSave(mapping, save))
                                completedOperations++;
                            else
                                return null;
                            break;

                        default:
                            break;
                    }

                }

                var negotiatecomplete = new { operations_completed = completedOperations, operations_failed = failedOperations };
                await HttpClientSingleton.RomMPostJsonAsync($"/api/sync/sessions/{result.SessionID}/complete", negotiatecomplete);

                var row = new SaveRow()
                {
                    LocalSaveGuid = null,
                    GameName = rom.Name!,
                    GameID = rom.Id,
                    SaveID = save.SaveID,
                    SyncStatus = SaveSyncStatus.no_op,
                    Slot = save.Slot
                };

                row.BuildSaveDirectoryView(mapping.SavePath, save.SourceFilePaths);

                return row;
            }
            catch (Exception ex)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.negotiatesaves.failed", Loc.GetString("FailedNegotiateSaves", ("Error", ex.Message)), GravitonSeverity.Error, ex));
                _logger.Error(response.RootElement.ToString());
                return null;
            }
        }

        public async Task<bool> NegotiateSaves(RomMRomLocal rom)
        {
            var mapping = _plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == rom.MappingID);
            if(mapping == null)
                return false;

            var negotiate = BuildNegotiate(mapping, rom.Saves, new List<RomMRomLocal> { rom });

            // Nothing to sync
            if (negotiate.Saves.Count <= 0) 
                return true;

            var response = await HttpClientSingleton.RomMPostJsonAsync("/api/sync/negotiate", negotiate);
            if (response == null)
                return false;

            try
            {
                var result = JsonSerializer.Deserialize<RomMNegotiateResponse>(response);
                if (result == null)
                    return false;

                var completedOperations = 0;
                var failedOperations = 0;

                foreach (var save in result.Operations)
                {
                    var savefilename = ServerTimestampTagPattern.Replace(save.FileName!, "");
                    LocalSave? localSave = rom.Saves.FirstOrDefault(y => y.SaveID == save.SaveID);

                    if (localSave == null)
                        continue;

                    if (save.Action == "conflict")
                    {
                        switch (_plugin.Settings.SaveConflictStyle)
                        {
                            case SaveConflictStyle.PreferRemote:
                                save.Action = "download";
                                break;
                            case SaveConflictStyle.PreferLocal:
                                save.Action = "upload";
                                break;
                            default:
                                var conflictresponse = await GravitonPlugin.PlayniteApi.Dialogs.ShowMessageAsync($"{Loc.GetString("WantKeepSave")}\n{Loc.GetString("Remote")}:\n\t{Loc.GetString("LastModified")}: {save.UpdatedAt}\n{Loc.GetString("Local")}:\n\t{Loc.GetString("LastModified")}: {save.UpdatedAt}", Loc.GetString("SaveConflict"), MessageBoxSeverity.Question, ConflictMessageBoxResponses, new List<MessageBoxOption>());
                                if (conflictresponse == null || conflictresponse.Title == Loc.GetString("Skip"))
                                    save.Action = "no_op";
                                else if (conflictresponse.Title == Loc.GetString("UseRemote"))
                                    save.Action = "download";
                                else if (conflictresponse.Title == Loc.GetString("UseLocal"))
                                    save.Action = "upload";
                                break;
                        }
                    }

                    switch (save.Action)
                    {
                        case "upload":
                            if (await UploadSave(mapping, localSave!, rom))
                                completedOperations++;
                            else   
                                failedOperations++;
                            break;

                        case "download":
                            if(await DownloadSave(mapping, localSave))
                                completedOperations++;
                            else
                                failedOperations++;
                            break;

                        default:
                            break;
                    }

                }

                var negotiatecomplete = new { operations_completed = completedOperations, operations_failed = failedOperations };
                await HttpClientSingleton.RomMPostJsonAsync($"/api/sync/sessions/{result.SessionID}/complete", negotiatecomplete);
            }
            catch (Exception ex)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.negotiatesaves.failed", Loc.GetString("FailedNegotiateSaves", ("Error", ex.Message)), GravitonSeverity.Error, ex));
                _logger.Error(response.RootElement.ToString());
                return false;
            }

            return true;
        }



        private RomMNegotiate BuildNegotiate(EmulatorMapping mapping, List<LocalSave> saves, List<RomMRomLocal> roms)
        {
            RomMNegotiate negotiate = new RomMNegotiate();
            negotiate.DeviceID = _plugin.Settings.AccountState.DeviceID;

            foreach (var save in saves)
            {
                var rom = roms.FirstOrDefault(x => x.Saves.Any(y => y.LocalID == save.LocalID));
                if (rom == null)
                    continue;

                RomMNegotiateSave negotiateSave = new()
                {
                    ROMID = rom.Id,
                    Slot = save.Slot
                };

                var hashfilepath = "";

                if (save.SourceFilePaths.Count == 1 && Path.HasExtension(save.SourceFilePaths[0]))
                {
                    negotiateSave.FileSize = new FileInfo(save.SourceFilePaths[0]).Length;
                    negotiateSave.FileName = Path.GetFileName(save.SourceFilePaths[0]);
                    negotiateSave.UpdatedAt = new FileInfo(save.SourceFilePaths[0]).LastWriteTimeUtc.ToString("O");
                    save.UpdatedAt = negotiateSave.UpdatedAt;
                    hashfilepath = save.SourceFilePaths[0];
                }
                else
                {
                    List<DateTime> saveWritetimes = new List<DateTime>();
                    foreach (var savepath in save.SourceFilePaths)
                    {
                        if (Path.HasExtension(savepath))
                        {
                            saveWritetimes.Add(new FileInfo(savepath).LastWriteTimeUtc);
                        }
                        else
                        {
                            saveWritetimes.Add(new DirectoryInfo(savepath).LastWriteTimeUtc);
                        }
                    }

                    var packedsavepath = $"{_plugin.PluginDataPath}/temp/{save.PackedFilename}";
                    if (!PackSave(save.SourceFilePaths, mapping.SavePath, packedsavepath))
                        continue;

                    negotiateSave.FileSize = new FileInfo(packedsavepath).Length;
                    negotiateSave.FileName = Path.GetFileName(packedsavepath);
                    negotiateSave.UpdatedAt = saveWritetimes.Max().ToString("O");
                    save.UpdatedAt = negotiateSave.UpdatedAt;
                    hashfilepath = packedsavepath;
                }

                using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(hashfilepath))
                    {
                        byte[] hash = md5.ComputeHash(stream);
                        negotiateSave.ContentHash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }
                }
                if(!(save.SourceFilePaths.Count == 1 && Path.HasExtension(save.SourceFilePaths[0])))
                    File.Delete(hashfilepath);

                negotiate.Saves.Add(negotiateSave);
            }

            return negotiate;
        }

        private async Task<bool> DownloadSave(EmulatorMapping mapping, LocalSave save)
        {
            var savedata = await HttpClientSingleton.RomMGetByteArrayAsync($"/api/saves/{save.SaveID}/content?device_id={_plugin.Settings.AccountState.DeviceID}&optimistic=false");
            if (savedata == null)
                return false;

            var savepath = save.SourceFilePaths.Count == 1 ? save.SourceFilePaths[0] : ServerTimestampTagPattern.Replace(save.PackedFilename!, "");
            var tempDir = $"{_plugin.PluginDataPath}/temp/{Path.GetFileName(savepath)}";

            File.WriteAllBytes(tempDir, savedata);

            List<string> sourcepaths = new List<string>();

            if (mapping.ExtractArchivedSaves && ArchiveFactory.IsArchive(tempDir, out _))
            {
                var result = UnpackSave(tempDir, mapping.SavePath);
                if (result == null)
                    return false;

                sourcepaths = result;
            }
            else
            {
                File.Move(tempDir, savepath, overwrite: true);
                sourcepaths.Add(savepath);
            }

            var deviceid = new { device_id = _plugin.Settings.AccountState.DeviceID };
            var response = await HttpClientSingleton.RomMPostJsonAsync($"/api/saves/{save.SaveID}/downloaded", deviceid);
            if (response == null)
                return false;

            var bytes = savedata.Length < 1000 ? $"{savedata.Length}B" : savedata.Length < 1000000 ? $"{(((float)savedata.Length) / 1000).ToString("F1")}KB" : $"{(((float)savedata.Length) / 1000000).ToString("F1")}MB";
            GravitonNotify.Add(new GravitonNotification($"graviton.save.{save.SaveID}.downloaded", Loc.GetString("DownloadedSave", ("SavePath", Path.GetFileName(savepath)), ("Bytes", bytes)), GravitonSeverity.Success));

            return true;
        }

        private async Task<bool> UploadSave(EmulatorMapping mapping, LocalSave save, RomMRomLocal rom)
        {
            var isPackedTemp = save.SourceFilePaths.Count > 1;
            var uploadfilepath = isPackedTemp ? $"{_plugin.PluginDataPath}/temp/{save.PackedFilename}" : save.SourceFilePaths[0];

            if (isPackedTemp)
                if (!PackSave(save.SourceFilePaths, mapping.SavePath, uploadfilepath))
                    return false;

            using var content = new MultipartFormDataContent();
            var savebytes = await File.ReadAllBytesAsync(uploadfilepath);
            var savecontent = new ByteArrayContent(savebytes);
            savecontent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(savecontent, "saveFile", Path.GetFileName(uploadfilepath));

            var response = await HttpClientSingleton.RomMPostContentAsync($"/api/saves?rom_id={rom.Id}&slot=Autosave&autocleanup={_plugin.Settings.AutoCleanupSaves}&autocleanup_limit={_plugin.Settings.AutoCleanupSavesLimit}&device_id={_plugin.Settings.AccountState.DeviceID}", content);
            if (isPackedTemp)
                File.Delete(uploadfilepath);

            if (response == null)
                return false;

            var result = JsonSerializer.Deserialize<RomMSave>(response);
            if (result == null)
                return false;

            var bytes = savebytes.Length < 1000 ? $"{savebytes.Length}B" : savebytes.Length < 1000000 ? $"{(((float)savebytes.Length) / 1000).ToString("F1")}KB" : $"{(((float)savebytes.Length) / 1000000).ToString("F1")}MB";
            GravitonNotify.Add(new GravitonNotification($"graviton.save.{save.SaveID}.uploaded", Loc.GetString("DownloadedSave", ("SavePath", Path.GetFileName(uploadfilepath)), ("Bytes", bytes)), GravitonSeverity.Success));

            rom.Saves.Remove(save);
            save.SaveID = result.ID;
            rom.Saves.Add(save);
            string json = JsonSerializer.Serialize(rom);
            File.WriteAllText($"{_plugin.PluginDataPath}/Games/{rom.SHA1}.json", json);

            return true;
        }


        private List<string>? UnpackSave(string tempSaveLocation, string destinationPath)
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
                    _logger.Warn(ex, $"Extraction succeeded but failed to delete temp file {tempSaveLocation}");
                }

                return sourcePaths;
            }
            catch (Exception ex)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.unpacksave.failed", Loc.GetString("FailedUnpack", ("SaveLoc", tempSaveLocation)), GravitonSeverity.Error, ex));
                return null;
            }
        }

        private bool PackSave(List<string> sourcePaths, string savePathRoot, string outputArchivePath)
        {
            try
            {
                using var archive = ZipArchive.CreateArchive();

                foreach (var path in sourcePaths)
                {
                    if (File.Exists(path))
                    {
                        var relativeKey = Path.GetRelativePath(savePathRoot, path).Replace('\\', '/');

                        var stream = File.OpenRead(path);
                        var lastModified = File.GetLastWriteTimeUtc(path);

                        archive.AddEntry(relativeKey, stream, closeStream: true, size: stream.Length, modified: lastModified);
                    }
                    else if (Directory.Exists(path))
                    {
                        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                        {
                            var relativeKey = Path.GetRelativePath(savePathRoot, file).Replace('\\', '/');

                            var stream = File.OpenRead(file);
                            var lastModified = File.GetLastWriteTimeUtc(file);

                            archive.AddEntry(relativeKey, stream, closeStream: true, size: stream.Length, modified: lastModified);
                        }
                    }
                    else
                    {
                        _logger.Warn($"Selected save path no longer exists, skipping: {path}");
                    }
                }

                archive.SaveTo(outputArchivePath, CompressionType.Deflate);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to pack save archive to {outputArchivePath}");
                return false;
            }

            return true;
        }
    }
}