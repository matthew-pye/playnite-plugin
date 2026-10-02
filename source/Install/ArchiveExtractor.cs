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

        public static void ExtractArchiveWith7z(string pathTo7z, string archivePath, string installDir, DownloadQueueItem item, CancellationToken ct)
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
                process?.WaitForExit();
                if (process?.ExitCode != 0)
                {
                    throw new Exception(Loc.GetString("ArchiveExtractionFailed", ("Path", archivePath), ("ExitCode", process?.ExitCode.ToString() ?? "?")));
                }
            }
        }
        public static void ExtractArchiveWithEntryProgress(string archivePath, string installDir, DownloadQueueItem item, CancellationToken ct)
        {
            using (var archive = ArchiveFactory.OpenArchive(archivePath))
            {
                var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
                int total = entries.Count;
                int done = 0;

                if (!Directory.Exists(installDir))
                    Directory.CreateDirectory(installDir);

                item.SetProgress(0, Math.Max(1, total), false);

                foreach (var entry in entries)
                {
                    ct.ThrowIfCancellationRequested();

                    entry.WriteToDirectory(installDir);

                    done++;
                    item.SetProgress(done, total, false);
                    var pct = total > 0 ? (double)done / total * 100.0 : 100.0;
                    item.SetStatus(DownloadStatus.Extracting, Loc.GetString("DownloadStatusExtractingPct", ("Percent", pct.ToString("0"))));
                }
            }
        }

        public static void ExtractArchive(string archivePath, string installDir, CancellationToken ct)
        {
            using (var archive = ArchiveFactory.OpenArchive(archivePath))
            {
                var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
                int total = entries.Count;
                int done = 0;

                if (!Directory.Exists(installDir))
                    Directory.CreateDirectory(installDir);

                foreach (var entry in entries)
                {
                    ct.ThrowIfCancellationRequested();

                    entry.WriteToDirectory(installDir);

                    done++;
                    var pct = total > 0 ? (double)done / total * 100.0 : 100.0;
                }
            }
        }
    }
}
