using Graviton.Install.Downloads;

using Playnite;

using System.IO;
namespace Graviton.Install
{
    public static class GravitonInstallHelpers
    {
        public static void CopyFileWithProgress(string source, string destination, DownloadQueueItem item, CancellationToken token)
        {
            item.SetStatus(DownloadStatus.Installing, Loc.GetString("DownloadStatusInstalling"));

            const int bufferSize = 1024 * 1024;

            var destinationExisted = File.Exists(destination);

            if (!string.IsNullOrEmpty(Path.GetDirectoryName(destination))) 
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            try
            {
                using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan);
                using var destinationStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.SequentialScan);

                var buffer = new byte[bufferSize];

                long totalBytes = sourceStream.Length;
                long copiedBytes = 0;

                int read;

                while ((read = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();

                    destinationStream.Write(buffer, 0, read);
                    copiedBytes += read;

                    item.SetProgress(copiedBytes, totalBytes, false);
                    item.SetStatus(DownloadStatus.Installing, Loc.GetString("DownloadStatusInstallingPct", ("$Percent", (double)copiedBytes / totalBytes * 100)));
                }
            }
            catch
            {
                // Only delete it if this operation created the file.
                if (!destinationExisted)
                {
                    try
                    {
                        if (File.Exists(destination))
                            File.Delete(destination);
                    }
                    catch {}
                }

                throw;
            }
        }

        public static void CopyDirectoryWithProgress(string sourceDirectory, string destinationDirectory, DownloadQueueItem item, CancellationToken token)
        {
            if (!Directory.Exists(sourceDirectory))
                throw new DirectoryNotFoundException($"Source directory does not exist: {sourceDirectory}");
            
            item.SetStatus(DownloadStatus.Installing, Loc.GetString("DownloadStatusInstalling"));

            var files = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories).ToList();

            var createdFiles = new List<string>();

            try
            {
                int currentFileCount = 0;
                item.SetProgress(currentFileCount, files.Count, false);

                foreach (var sourceFile in files)
                {
                    token.ThrowIfCancellationRequested();

                    var relativePath = Path.GetRelativePath(sourceDirectory, sourceFile);

                    var destinationFile = Path.Combine(destinationDirectory, relativePath);

                    if (!string.IsNullOrEmpty(Path.GetDirectoryName(destinationFile)))
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
                    
                    var existed = File.Exists(destinationFile);

                    File.Copy(sourceFile, destinationFile, true);

                    if (!existed)
                        createdFiles.Add(destinationFile);

                    currentFileCount++;
                    item.SetProgress(currentFileCount, files.Count, false);
                    item.SetStatus(DownloadStatus.Installing, Loc.GetString("DownloadStatusInstallingRatio", ("$Current", currentFileCount), ("$Max", files.Count)));
                }
            }
            catch
            {
                ArchiveExtractor.CleanupCreatedFiles(createdFiles);
                throw;
            }
        }

        public static List<string> GetInstalledTopPaths(string sourceDirectory, string destinationDirectory)
        {
            var paths = new List<string>();

            foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                paths.Add(Path.Combine(destinationDirectory, Path.GetFileName(directory)));
            }

            foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                paths.Add(Path.Combine(destinationDirectory, Path.GetFileName(file)));
            }

            return paths;
        }

        public static string SanitizeString(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = string.Concat(value.Where(c => !invalid.Contains(c))).Trim();

            return string.IsNullOrWhiteSpace(sanitized) ? "Unnamed" : sanitized;
        }
    }
}