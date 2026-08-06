using Graviton.Models;
using Graviton.Models.Notifications;
using Graviton.Models.RomM;
using Graviton.Models.RomM.Rom;
using Graviton.Models.RomM.Saves;
using Graviton.Models.Saves;

using Playnite;

using System.IO;
using System.Text.Json;
using System.Windows;

namespace Graviton.Saves
{
    internal static class SaveNegotiator
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
                if (rom.LocalSave == null)
                    continue;

                if (response == null)
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
                        case SaveSyncStatus.upload:
                            rom.LocalSave.Status = SaveStatus.LocalNewer;
                            break;

                        case SaveSyncStatus.download:
                            rom.LocalSave.Status = SaveStatus.RemoteNewer;
                            break;

                        case SaveSyncStatus.no_op:
                            rom.LocalSave.Status = SaveStatus.Synced;
                            break;

                        case SaveSyncStatus.conflict:
                            rom.LocalSave.Status = SaveStatus.Conflicted;
                            break;

                        default:
                            rom.LocalSave.Status = SaveStatus.Unknown;
                            break;
                    }
                }

                var mapping = _plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == rom.MappingID);
                if (mapping != null)
                    rom.LocalSave.SaveDirectoryTrees = SaveDirectoryTree.Build(mapping.SavePath, rom.LocalSave.SourceFilePaths.ToList());

                rom.Save();
            }

            return roms;
        }

        public static async Task NegotiateSave(RomMRomLocal rom, byte[]? screenshot = null)
        {
            if (GameSessionHandler.IsAGameRunning)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.sync.cannotstart", Loc.GetString("SyncCannotStart"), GravitonSeverity.Info));
                return;
            }

            if (rom.LocalSave == null)
                return;

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
                GravitonNotify.Add(new GravitonNotification("graviton.syncresponse.null", Loc.GetString("ServerNoResponseSync"), GravitonSeverity.Error));
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

                    if (operation.Action == SaveSyncStatus.conflict)
                    {
                        action = ResolveConflict(rom.LocalSave!);
                    }

                    switch (action)
                    {
                        case SaveSyncStatus.upload:
                            var saveID = rom.LocalSave.SaveID;
                            rom.LocalSave.Status = SaveStatus.LocalNewer;
                            rom.LocalSave.ServerHash = operation.ServerContentHash;
                            rom.LocalSave.ServerLastUpdatedAt = DateTime.Parse(operation.ServerUpdatedAt!);

                            var result = await SaveManager.Upload(rom.LocalSave!, false, screenshot, operation);

                            if(saveID == result.SaveID)
                            {
                                operationFailed++;
                                break;
                            }

                            rom.LocalSave.IsTempRestored = false;
                            operationCompleted++;
                            break;

                        case SaveSyncStatus.download:

                            if(rom.LocalSave.HistoricSaves == null)
                                rom.LocalSave.HistoricSaves = new();

                            var savecopy = JsonSerializer.Deserialize<GravitonSave>(JsonSerializer.Serialize(rom.LocalSave));
                            if(savecopy != null)
                            {
                                var historicsave = rom.LocalSave.HistoricSaves.FirstOrDefault(x => x.SaveID == savecopy.SaveID);
                                if (historicsave == null) 
                                { 
                                    savecopy.IsCurrent = false; 
                                    savecopy.IsHistoric = true; 
                                    rom.LocalSave.HistoricSaves.Add(savecopy); 
                                }
                            }
                            
                            rom.LocalSave.SaveID = operation.SaveID;
                            rom.LocalSave.Status = SaveStatus.RemoteNewer;
                            rom.LocalSave.ServerHash = operation.ServerContentHash;
                            rom.LocalSave.ServerLastUpdatedAt = DateTime.Parse(operation.ServerUpdatedAt!);

                            await SaveManager.Download(rom.LocalSave);
                            rom.LocalSave.IsTempRestored = false;
                            operationCompleted++;
                            break;

                        case SaveSyncStatus.no_op:
                            rom.LocalSave.Status = SaveStatus.Synced;
                            rom.LocalSave.IsTempRestored = false;
                            GravitonNotify.Add(new GravitonNotification("graviton.sync.noop", Loc.GetString("NoSyncNeeded", ("GameName", rom.Name!)), GravitonSeverity.Info));
                            operationCompleted++;
                            break;

                        case SaveSyncStatus.conflict:
                            rom.LocalSave.Status = SaveStatus.Conflicted;
                            rom.LocalSave.IsTempRestored = false;
                            GravitonNotify.Add(new GravitonNotification("graviton.sync.conflicted", Loc.GetString("SyncStillConflicted", ("GameName", rom.Name!)), GravitonSeverity.Warn));
                            operationFailed++;
                            break;

                        default:
                            rom.LocalSave.Status = SaveStatus.Unknown;
                            operationFailed++;
                            break;
                    }
                }

                var mapping = _plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == rom.MappingID);
                if (mapping != null)
                    rom.LocalSave.SaveDirectoryTrees = SaveDirectoryTree.Build(mapping.SavePath, rom.LocalSave.SourceFilePaths.ToList());

            }

            rom.Save();

            var deviceid = new { operations_completed = operationCompleted, operations_failed = operationFailed};
            await RomMServer.POSTAsync($"/api/sync/sessions/{response.SessionID}/complete", deviceid);

        }

        public static async Task<RomMNegotiateResponse?> Negotiate(RomMNegotiate negotiate)
        { 
            var response = await RomMServer.POSTAsync("/api/sync/negotiate", negotiate);
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

        public static RomMNegotiate BuildNegotiate(List<RomMRomLocal> roms)
        {
            RomMNegotiate negotiate = new RomMNegotiate();
            negotiate.DeviceID = _plugin.Settings.AccountState.DeviceID;

            foreach (var rom in roms)
            {
                if (rom.LocalSave == null)
                    continue;

                var mapping = _plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == rom.MappingID);
                if (mapping == null)
                    continue;

                if(rom.LocalSave.Status == SaveStatus.MissingFiles)
                {
                    // Check to see if missing files/folders have returned
                    foreach (var missingPath in rom.LocalSave.MissingFiles.ToList())
                    {
                        if(File.Exists(missingPath))
                        {
                            rom.LocalSave.MissingFiles.Remove(missingPath);
                        }
                        else if (Directory.Exists(missingPath))
                        {
                            rom.LocalSave.MissingFiles.Remove(missingPath);
                        }
                    }

                    // If missing files are still present skip negotiate
                    if(rom.LocalSave.MissingFiles.Count > 0)
                        continue;
                }

                RomMNegotiateSave negotiateSave = new()
                {
                    ROMID = rom.Id,
                    Slot = rom.LocalSave.Slot
                };

                var path = rom.LocalSave.SourceFilePaths[0].Replace(EmulatorMapping.MappingPathToken, mapping.SavePath);

                // Single file save
                if (rom.LocalSave.SourceFilePaths.Count == 1 && File.Exists(path))
                {
                    negotiateSave.FileSize = new FileInfo(path).Length;
                    if (negotiateSave.FileSize == 0)
                    {
                        GravitonNotify.Add(new GravitonNotification("graviton.save.zerobytes", Loc.GetString("SaveFileZeroBytes", ("GameName", rom.Name!)), GravitonSeverity.Error));
                        continue;
                    }

                    negotiateSave.ContentHash = SaveHelpers.ComputeFileContentHash(path);
                    if (negotiateSave.ContentHash == null)
                    {
                        continue;
                    }

                    rom.LocalSave.FileSize = negotiateSave.FileSize;
                    rom.LocalSave.ContentHash = negotiateSave.ContentHash;
                   
                    negotiateSave.FileName = rom.LocalSave.Filename;
                    negotiateSave.UpdatedAt = new FileInfo(path).LastWriteTimeUtc.ToString("O");
                }
                else if(rom.LocalSave.SourceFilePaths.Count > 1 || Directory.Exists(path)) //Multi-file / Folder / Multi-folder save
                {

                    var packedsavepath = $"{_plugin.PluginDataPath}/temp/{rom.LocalSave.Filename}";
                    var savePacked = SaveHelpers.PackSave(rom.LocalSave.SourceFilePaths, mapping.SavePath, packedsavepath, out var skippedPaths);

                    // Skip negotiate if pack fails or there are missing paths
                    if (!savePacked || skippedPaths.Count > 0)
                    {
                        if (skippedPaths.Count > 0)
                        {
                            rom.LocalSave.Status = SaveStatus.MissingFiles;
                            rom.LocalSave.MissingFiles = skippedPaths;
                            rom.Save();
                        }
                       
                       continue;
                    }

                    // Skip 0 byte saves
                    negotiateSave.FileSize = new FileInfo(packedsavepath).Length;
                    if (negotiateSave.FileSize == 0)
                    {
                        GravitonNotify.Add(new GravitonNotification("graviton.save.zerobytes", Loc.GetString("SaveFileZeroBytes", ("GameName", rom.Name!)), GravitonSeverity.Error));
                        continue;
                    }

                    negotiateSave.ContentHash = SaveHelpers.ComputePackedContentHash(packedsavepath);
                    if (negotiateSave.ContentHash == null)
                    {
                        continue;
                    }

                    rom.LocalSave.FileSize = negotiateSave.FileSize;
                    rom.LocalSave.ContentHash = negotiateSave.ContentHash;     

                    List<DateTime> saveWritetimes = new List<DateTime>();
                    foreach (var savepath in rom.LocalSave.SourceFilePaths)
                    {
                        path = savepath.Replace(EmulatorMapping.MappingPathToken, mapping.SavePath);

                        if (File.Exists(path))
                        {
                            saveWritetimes.Add(new FileInfo(path).LastWriteTimeUtc);
                        }
                        else if (Directory.Exists(path))
                        {
                            var dirInfo = new DirectoryInfo(path);
                            var writeTimes = dirInfo.GetFiles("*", SearchOption.AllDirectories).Select(x => x.LastWriteTimeUtc).ToList();
                            saveWritetimes.AddRange(writeTimes);
                        }
                        else
                        {
                            rom.LocalSave.Status = SaveStatus.MissingFiles;
                            skippedPaths.Add(path);
                        }
                    }

                    if(rom.LocalSave.Status == SaveStatus.MissingFiles)
                    {
                        rom.LocalSave.MissingFiles = skippedPaths;
                        rom.Save();
                        continue;
                    }

                    negotiateSave.FileName = Path.GetFileName(packedsavepath);
                    negotiateSave.UpdatedAt = saveWritetimes.Max().ToString("O");

                    if (File.Exists(packedsavepath))
                        File.Delete(packedsavepath);
                }
                else // File for single file/folder save is missing
                {
                    rom.LocalSave.Status = SaveStatus.MissingFiles;
                    rom.LocalSave.MissingFiles.Add(path);
                    rom.Save();
                    GravitonNotify.Add(new GravitonNotification("graviton.save.missingfiles", Loc.GetString("SaveFileMissingFiles", ("GameName", rom.Name!)), GravitonSeverity.Error));
                    continue;
                }

                negotiate.Saves.Add(negotiateSave);
            }

            return negotiate;
        }

        public static SaveSyncStatus ResolveConflict(GravitonSave save)
        {

            if (save.ServerLastUpdatedAt == null)
                return SaveSyncStatus.conflict;

            var window = PlayniteAPI.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = false,
                ShowMaximizeButton = false,
                ShowCloseButton = true,
                DefaultWidth = 650,
                DefaultHeight = 315
            });

            var resolveConflictView = new ResolveConflictView(save.ServerLastUpdatedAt.Value, save.LastSyncedAt!.Value);

            window.Title = Loc.GetString("SaveConflict");
            window.Content = resolveConflictView;
            window.ResizeMode = ResizeMode.NoResize;
            window.Owner = PlayniteAPI.GetLastActiveWindow();
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();

            return resolveConflictView.Status;
        }
    }
}
