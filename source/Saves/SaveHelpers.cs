using Graviton.Models;
using Graviton.Models.Notifications;

using Playnite;

using Nanook.GrindCore.MD;

using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Archives.Zip;

using System.IO;
using System.Text;
using System.Collections.ObjectModel;

namespace Graviton.Saves
{
    public static class SaveHelpers
    {
        public static bool PackSave(ObservableCollection<string> sourcePaths, string savePathRoot, string outputArchivePath, out List<string> skippedPaths)
        {
            bool missingfiles = false;

            List<string> localSkippedpaths = new();

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
                        localSkippedpaths.Add(processedpath);
                        missingfiles = true;
                    }
                }

                archive.SaveTo(outputArchivePath, CompressionType.Deflate);
            }
            catch (Exception ex)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.packsave.failed", Loc.GetString("PackSaveFailed", ("Path", outputArchivePath)), GravitonSeverity.Error, ex));
                skippedPaths = localSkippedpaths;
                return false;
            }

            if (missingfiles)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.packsave.missingfiles", Loc.GetString("PackSaveFilesSkipped"), GravitonSeverity.Warn));
            }

            skippedPaths = localSkippedpaths;
            return true;
        }


        public static ObservableCollection<string>? UnpackSave(string tempSaveLocation, string destinationPath)
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

                return CollapseUnpackedPaths(sourcePaths, destinationFull);
            }
            catch (Exception ex)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.unpacksave.failed", Loc.GetString("FailedUnpack", ("SaveLoc", tempSaveLocation)), GravitonSeverity.Error, ex));
                return null;
            }
        }

        public static string? ComputePackedContentHash(string zipPath)
        {
            for (int i = 0; i < 3;)
            {
                try
                {
                    var entryHashes = new List<string>();

                    using (var archive = ZipArchive.OpenArchive(zipPath))
                    {
                        if (!archive.Entries.Any())
                        {
                            GravitonNotify.Add(new GravitonNotification("graviton.archive.empty", Loc.GetString("ComputeHashArchiveEmpty", ("Path", zipPath)), GravitonSeverity.Error));
                            return null;
                        }
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
                catch (Exception)
                {
                    GravitonPlugin.Logger.Error($"Failed to compute content hash for {zipPath}, retrying #{i++}");
                    Task.Delay(100).GetAwaiter().GetResult();
                }
            }

            GravitonNotify.Add(new GravitonNotification("graviton.computehash.failed", Loc.GetString("ComputeHashFailed", ("Path", zipPath)), GravitonSeverity.Error));
            return null;
        }

        public static string? ComputeFileContentHash(string path)
        {
            for (int i = 0; i < 3;)
            {
                try
                {
                    using (var md5 = MD5.Create())
                    using (var stream = File.OpenRead(path))
                    {
                        var hash = md5.ComputeHash(stream);
                        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }
                }
                catch (Exception)
                {
                    GravitonPlugin.Logger.Error($"Failed to compute content hash for {path}, retrying #{i++}");
                    Task.Delay(100);
                }
            }

            GravitonNotify.Add(new GravitonNotification("graviton.computehash.failed", Loc.GetString("ComputeHashFailed", ("Path", path)), GravitonSeverity.Error));
            return null;
        }


        private static ObservableCollection<string> CollapseUnpackedPaths(List<string> filePaths, string root)
        {
            var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var individualFiles = new List<string>();

            foreach (var file in filePaths)
            {
                var directory = Path.GetDirectoryName(file);
                if (string.IsNullOrEmpty(directory))
                {
                    individualFiles.Add(file);
                    continue;
                }

                var normalizedDir = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (normalizedDir.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                    individualFiles.Add(file);
                else
                    folders.Add(normalizedDir);
            }

            var prunedFolders = folders.Where(folder => !folders.Any(other => !other.Equals(folder, StringComparison.OrdinalIgnoreCase) && folder.StartsWith(other + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)));

            var result = new List<string>(individualFiles);
            result.AddRange(prunedFolders.Select(f => f + Path.DirectorySeparatorChar));
            return result.ToObservableCollection();
        }


    }
}
