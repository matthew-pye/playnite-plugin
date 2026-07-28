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
        private static GravitonPlugin Plugin => GravitonPlugin.Instance;
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

               var operation = response.Operations.FirstOrDefault(x => x.SaveID == rom.LocalSave.SaveID);
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

                var mapping = Plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == rom.MappingID);
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
            if (response == null)
                return;

            if (response == null)
            {
                rom.LocalSave.Status = SaveStatus.Unknown;
                rom.LocalSave.ServerHash = null;
            }
            else
            {
                var operation = response.Operations.FirstOrDefault(x => x.SaveID == rom.LocalSave.SaveID);
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

                    if(operation.Action == "conflict")
                    {
                        operation.Action = ResolveConflict(rom.LocalSave).ToString();
                    }

                    switch (operation.Action)
                    {
                        case "upload":
                            await Upload(rom.LocalSave);
                            break;

                        case "download":
                            await Download(rom.LocalSave);
                            break;

                        case "no_op":
                            rom.LocalSave.Status = SaveStatus.Synced;
                            break;

                        default:
                            rom.LocalSave.Status = SaveStatus.Unknown;
                            break;
                    }
                }

                var mapping = Plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == rom.MappingID);
                if (mapping != null)
                    rom.LocalSave.SaveDirectoryTrees = SaveDirectoryTree.Build(mapping.SavePath, rom.LocalSave.SourceFilePaths);

            }

            rom.Save();

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
            negotiate.DeviceID = Plugin.Settings.AccountState.DeviceID;

            foreach (var rom in roms)
            {
                var mapping = Plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == rom.MappingID);
                if (mapping == null)
                    continue;

                RomMNegotiateSave negotiateSave = new()
                {
                    ROMID = rom.Id,
                    Slot = rom.LocalSave.Slot
                };

                if (rom.LocalSave.SourceFilePaths.Count == 1 && Path.HasExtension(rom.LocalSave.SourceFilePaths[0]))
                {
                    negotiateSave.FileSize = new FileInfo(rom.LocalSave.SourceFilePaths[0]).Length;
                    rom.LocalSave.FileSize = negotiateSave.FileSize;

                    negotiateSave.FileName = rom.LocalSave.Filename;
                    negotiateSave.UpdatedAt = new FileInfo(rom.LocalSave.SourceFilePaths[0]).LastWriteTimeUtc.ToString("O");

                    negotiateSave.ContentHash = ComputeFileContentHash(rom.LocalSave.SourceFilePaths[0]);
                    rom.LocalSave.ContentHash = negotiateSave.ContentHash;
                }
                else
                {
                    List<DateTime> saveWritetimes = new List<DateTime>();
                    foreach (var savepath in rom.LocalSave.SourceFilePaths)
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

                    var packedsavepath = $"{Plugin.PluginDataPath}/temp/{rom.LocalSave.Filename}";
                    if (!PackSave(rom.LocalSave.SourceFilePaths, mapping.SavePath, packedsavepath))
                        continue;

                    negotiateSave.FileSize = new FileInfo(packedsavepath).Length;
                    rom.LocalSave.FileSize = negotiateSave.FileSize;

                    negotiateSave.FileName = Path.GetFileName(packedsavepath);
                    negotiateSave.UpdatedAt = saveWritetimes.Max().ToString("O");

                    negotiateSave.ContentHash = ComputePackedContentHash(packedsavepath);
                    rom.LocalSave.ContentHash = negotiateSave.ContentHash;

                    if (!(rom.LocalSave.SourceFilePaths.Count == 1 && Path.HasExtension(rom.LocalSave.SourceFilePaths[0])))
                        File.Delete(packedsavepath);
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

        public static async Task<GravitonSave> Upload(GravitonSave save)
        {
            var rom = Plugin.ImportedGames!.FirstOrDefault(x => x.Value.Id == save.ROMID).Value;
            if(rom == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.upload.failed", "Failed to find ROM that matches save, skipping upload", GravitonSeverity.Error));
                return save;
            }

            var mapping = Plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == rom.MappingID);
            if (mapping == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.upload.failed", "Failed to mapping, skipping upload", GravitonSeverity.Error));
                return save;
            }

            if(save.SaveID != -1)
                await UntrackSave(rom.LocalSave.SaveID);

            bool isPacked = false;
            string savePath = "";
            if (save.SourceFilePaths.Count > 1 || !Path.HasExtension(save.SourceFilePaths[0]))
            {
                savePath = $"{Plugin.PluginDataPath}/temp/{save.Filename}";
                isPacked = true;
                if (!PackSave(save.SourceFilePaths, mapping.SavePath, savePath))
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.upload.failed", "Failed to pack save, skipping upload", GravitonSeverity.Error));
                    return save;
                }
            }
            else
            {
                savePath = save.SourceFilePaths[0];
            }

            var savebytes = File.ReadAllBytes(savePath);
            var content = new MultipartFormDataContent();
            
            var savecontent = new ByteArrayContent(savebytes);
            savecontent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(savecontent, "saveFile", Path.GetFileName(savePath));

            var response = await HttpClientSingleton.RomMRawPostContentAsync($"/api/saves?rom_id={rom.Id}&slot=Autosave&autocleanup={Plugin.Settings.AutoCleanupSaves}&autocleanup_limit={Plugin.Settings.AutoCleanupSavesLimit}&device_id={Plugin.Settings.AccountState.DeviceID}", content);
            
            if(response?.Status == HttpStatusCode.Conflict)
            {
                var result = ResolveConflict(save);

                switch (result)
                {
                    case SaveSyncStatus.upload:
                        response.Status = HttpStatusCode.OK;
                        break;
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
                save.LastSyncedAt = DateTime.Parse(result.UpdatedAt!);
                save.ServerLastUpdatedAt = DateTime.Parse(result.UpdatedAt!);
                save.ContentHash = result.ContentHash;
                save.ServerHash = result.ContentHash;
                save.FileSize = result.FileSize!.Value;
                save.Status = SaveStatus.Synced;
                save.IsCurrent = true;

                rom.LocalSave = save;
                rom.Save();

                var deviceid = new { device_id = Plugin.Settings.AccountState.DeviceID };
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
            var rom = Plugin.ImportedGames!.FirstOrDefault(x => x.Value.Id == save.ROMID).Value;
            if (rom == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.download.failed", "Failed to find ROM that matches save, skipping download", GravitonSeverity.Error));
                return save;
            }

            var mapping = Plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == rom.MappingID);
            if (mapping == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.download.failed", "Failed to mapping, skipping download", GravitonSeverity.Error));
                return save;
            }

            var savedata = await HttpClientSingleton.RomMRawGetAsync($"/api/saves/{save.SaveID}/content?device_id={Plugin.Settings.AccountState.DeviceID}&optimistic=false");
            if (savedata == null || savedata.Status != HttpStatusCode.OK)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.download.failed", "Failed to get save data from server, skipping download", GravitonSeverity.Error));
                return save;
            }

            if (!Directory.Exists($"{Plugin.PluginDataPath}/temp/"))
                Directory.CreateDirectory($"{Plugin.PluginDataPath}/temp/");

            var tempDir = $"{Plugin.PluginDataPath}/temp/{save.Filename}";
            
            using var ms = new MemoryStream();
            savedata.Content!.CopyTo(ms);
            File.WriteAllBytes(tempDir, ms.ToArray());

            if (mapping.ExtractArchivedSaves && ArchiveFactory.IsArchive(tempDir, out _))
            {
                var paths = UnpackSave(tempDir, mapping.SavePath);
                if(paths == null)
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.download.failed", "Failed to unpack save data, skipping download", GravitonSeverity.Error));
                    return save;
                }

                save.SourceFilePaths = paths;
                save.SourceFilePaths.ForEach(x => x.Replace(mapping.SavePath, EmulatorMapping.MappingPathToken));
            }
            else
            {
                var savelocation = save.SourceFilePaths[0].Replace(EmulatorMapping.MappingPathToken, mapping.SavePath);
                savelocation.Replace(save.Filename, "");
                File.Move(tempDir, savelocation, true);
            }

            save.LastSyncedAt = save.ServerLastUpdatedAt!.Value;
            save.ContentHash = save.ServerHash;
            save.FileSize = ms.Length;
            save.Status = SaveStatus.Synced;
            
            if(!SkipSavingROM)
            {
                rom.LocalSave = save;
                rom.Save();
            }

            var deviceid = new { device_id = Plugin.Settings.AccountState.DeviceID };
            await HttpClientSingleton.RomMPostJsonAsync($"/api/saves/{save.SaveID}/downloaded", deviceid);
            
            GravitonNotify.Add(new GravitonNotification("graviton.download.success", $"{rom.Name} save downloaded ({save.FileSizeString})", GravitonSeverity.Success));
            return save;
        }

        public static async Task<GravitonSave> TrackNewRemoteSave(GravitonSave save)
        {
            var rom = Plugin.ImportedGames!.FirstOrDefault(x => x.Value.Id == save.ROMID).Value;
            if (rom == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.download.failed", "Failed to find ROM that matches save, Skipping download", GravitonSeverity.Error));
                return save;
            }

            var mapping = Plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == rom.MappingID);
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

            var savedata = await HttpClientSingleton.RomMRawGetAsync($"/api/saves/{save.SaveID}/content?device_id={Plugin.Settings.AccountState.DeviceID}&optimistic=false");
            if (savedata == null || savedata.Status != HttpStatusCode.OK)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.download.failed", "Failed to get save data from server, skipping download", GravitonSeverity.Error));
                return save;
            }

            if (!Directory.Exists($"{Plugin.PluginDataPath}/temp/"))
                Directory.CreateDirectory($"{Plugin.PluginDataPath}/temp/");

            var tempDir = $"{Plugin.PluginDataPath}/temp/{save.Filename}";

            using var ms = new MemoryStream();
            savedata.Content!.CopyTo(ms);
            File.WriteAllBytes(tempDir, ms.ToArray());

            if (mapping.ExtractArchivedSaves && ArchiveFactory.IsArchive(tempDir, out _))
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

                paths.ForEach(x => x.Replace(mapping.SavePath, EmulatorMapping.MappingPathToken));
                save.SourceFilePaths = paths;
            }
            else
            {
                var savelocation = save.SourceFilePaths[0].Replace(EmulatorMapping.MappingPathToken, mapping.SavePath);
                savelocation.Replace(save.Filename, "");
                File.Move(tempDir, savelocation, true);
            }

            save.LastSyncedAt = save.ServerLastUpdatedAt!.Value;
            save.ContentHash = save.ServerHash;
            save.FileSize = ms.Length;
            save.Status = SaveStatus.Synced;

            rom.LocalSave = save;
            rom.Save();

            var deviceid = new { device_id = Plugin.Settings.AccountState.DeviceID };
            await HttpClientSingleton.RomMPostJsonAsync($"/api/saves/{save.SaveID}/downloaded", deviceid);

            GravitonNotify.Add(new GravitonNotification("graviton.download.success", $"{rom.Name} save downloaded ({save.FileSizeString})", GravitonSeverity.Success));
            return save;
        }

        public static async Task<GravitonSave> TrackNewLocalSave(GravitonSave save)
        {
            return await Upload(save);
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
            var deviceid = new { device_id = Plugin.Settings.AccountState.DeviceID };
            await HttpClientSingleton.RomMPostJsonAsync($"/api/saves/{saveID}/untrack", deviceid);
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
                        GravitonPlugin.Logger.Warn($"Selected save path no longer exists, skipping: {path}");
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
