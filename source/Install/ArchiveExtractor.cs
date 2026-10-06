using Playnite;

using SharpCompress.Archives;

using System.Diagnostics;
using System.IO;

namespace Graviton.Install.Downloads
{
    public static class ArchiveExtractor
    {
        public static bool IsFileCompressed(string filePath)
        {
            if (Path.GetExtension(filePath).Equals(".iso", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return ArchiveFactory.IsArchive(filePath, out var type);
        }

        public static async Task ExtractArchiveWith7z(string pathTo7z, string archivePath, string installDir, DownloadQueueItem item, CancellationToken ct)
        {
            if (archivePath == null || archivePath.Contains("../") || archivePath.Contains(@"..\"))
            {
                throw new ArgumentException("Invalid archive path");
            }
            if (installDir == null || installDir.Contains("../") || installDir.Contains(@"..\"))
            {
                throw new ArgumentException("Invalid install directory path");
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = pathTo7z,
                Arguments = $"x \"{archivePath.Replace("\"", "\\\"")}\" -o\"{installDir.Replace("\"", "\\\"")}\" -y",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            ct.ThrowIfCancellationRequested();
            using (Process? process = Process.Start(startInfo))
            {
                if(process == null)
                    throw new Exception(Loc.GetString("ProcessFailedToStart"));

                while (!process!.HasExited)
                {
                    if (ct.IsCancellationRequested)
                    {
                        try
                        {
                            process.Kill(true);
                        }
                        catch {}

                        ct.ThrowIfCancellationRequested();
                    }

                    // Wait 100ms before again checking if process has exited
                    await Task.Delay(100, ct);
                }

                if (process.ExitCode != 0)
                {
                    throw new Exception(Loc.GetString("ArchiveExtractionFailed", ("Path", archivePath), ("ExitCode", process?.ExitCode.ToString() ?? "?")));
                }
            }
        }
        public static void ExtractArchiveWithEntryProgress(string archivePath, string installDir, DownloadQueueItem item, CancellationToken ct)
        {
            var createdFiles = new List<string>();

            try
            {
                using var archive = ArchiveFactory.OpenArchive(archivePath);

                var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();

                int total = entries.Count;
                int done = 0;

                Directory.CreateDirectory(installDir);

                item.SetProgress(0, Math.Max(1, total), false);

                foreach (var entry in entries)
                {
                    ct.ThrowIfCancellationRequested();

                    if (entry.Key == null)
                        continue;

                    var destination = Path.Combine(installDir, entry.Key);

                    var existed = File.Exists(destination);

                    entry.WriteToDirectory(installDir);

                    if (!existed && File.Exists(destination))
                        createdFiles.Add(destination);

                    done++;
                    item.SetProgress(done, total, false);
                    var pct = total > 0 ? (double)done / total * 100.0 : 100.0;
                    item.SetStatus(DownloadStatus.Extracting, Loc.GetString("DownloadStatusExtractingPct", ("Percent", pct.ToString("0"))));
                }
            }
            catch
            {
                CleanupCreatedFiles(createdFiles);
                throw;
            }
        }

        public static void ExtractArchive(string archivePath, string installDir, CancellationToken ct)
        {
            var createdFiles = new List<string>();

            try
            {
                using var archive = ArchiveFactory.OpenArchive(archivePath);

                var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();

                int total = entries.Count;
                int done = 0;

                Directory.CreateDirectory(installDir);


                foreach (var entry in entries)
                {
                    ct.ThrowIfCancellationRequested();

                    if (entry.Key == null)
                        continue;

                    var destination = Path.Combine(installDir, entry.Key);
                    var existed = File.Exists(destination);

                    entry.WriteToDirectory(installDir);

                    if (!existed && File.Exists(destination))
                        createdFiles.Add(destination);

                    done++;
                }
            }
            catch
            {
                CleanupCreatedFiles(createdFiles);
                throw;
            }
        }

        public static void CleanupCreatedFiles(IEnumerable<string> files)
        {
            foreach (var file in files.Reverse())
            {
                try
                {
                    if (File.Exists(file))
                        File.Delete(file);
                }
                catch { }
            }

            var directories = files.Select(Path.GetDirectoryName).Where(x => !string.IsNullOrEmpty(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(x => x!.Length);

            foreach (var directory in directories)
            {
                try
                {
                    if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory!).Any())
                        Directory.Delete(directory!);
                    
                }
                catch { }
            }
        }
    }
}
