
using Graviton.Models.Notifications;
using Graviton.Models.RomM;
using Graviton.Models.RomM.Rom;
using Graviton.Models.Saves;

using Nanook.GrindCore.MD;

using Playnite;

using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;

using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Graviton.Saves
{
    internal static class SaveManager
    {
        private static GravitonPlugin Plugin => GravitonPlugin.Instance;
        private static readonly Regex ServerTimestampTagPattern = new(@"[ _]?\[\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}\](?=\.[^.]+$)", RegexOptions.Compiled);


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
               if (operation == null)
               {
                    rom.LocalSave.Status = SaveStatus.Unknown;
                    rom.LocalSave.ServerHash = null;
                }
               else
               {
                    rom.LocalSave.ServerHash = operation.ServerContentHash;

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

        public static async Task<List<RomMRomLocal>?> NegotiateSaves(List<RomMRomLocal> roms)
        {
            var negotiate = BuildNegotiate(roms);
            var response = await Negotiate(negotiate);
            if (response == null)
                return null;

            foreach (var rom in roms)
            {
               
            }

            return roms;

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

        #region Helper Functions
        private static string ComputePackedContentHash(string zipPath)
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

        private static string ComputeFileContentHash(string path)
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
