using Graviton.Notifications;

using Playnite;

using System.IO;
using System.Net.Http;

namespace Graviton.Install.Downloads
{
    public class DownloadQueueController
    {
        private GravitonPlugin _plugin;
        private IPlayniteApi _playniteAPI;
        private GravitonLogger _logger;
        private IRomMServer _romMServer;

        private readonly DownloadQueueViewModel DownloadQueueVM;
        private readonly SemaphoreSlim concurrencyGate;

        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, CancellationTokenSource> activeDownloads = new();

        public GravitonLogger? Logger;
        public int MaxConcurrent { get; }

        public DownloadQueueController(GravitonPlugin plugin, IPlayniteApi playniteAPI, GravitonLogger logger, IRomMServer romMServer, DownloadQueueViewModel downloadQueueVM, int maxConcurrent)
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;
            _romMServer = romMServer;

            DownloadQueueVM = downloadQueueVM;

            MaxConcurrent = Math.Max(1, maxConcurrent);
            concurrencyGate = new SemaphoreSlim(MaxConcurrent, MaxConcurrent);
        }

        public DownloadQueueViewModel ViewModel => DownloadQueueVM;

        public void Enqueue(DownloadRequest req)
        {
            var item = new DownloadQueueItem
            {
                Id = req.Id,
                DisplayName = req.DisplayName,
                QueuedOn = DateTime.Now,
                Cts = new CancellationTokenSource()
            };

            activeDownloads[item.Id] = item.Cts;

            item.SetStatus(DownloadStatus.Queued, Loc.GetString("DownloadStatusQueued"));
            item.SetProgress(0, 1, true);

            UIDispatcher.Invoke(() => DownloadQueueVM.Items.Add(item));

            // fire and forget background worker
            Task.Run(async () => await ProcessItem(item, req));
        }

        public void Cancel(string Id)
        {
            if (activeDownloads.TryGetValue(Id, out var cts))
            {
                try
                {
                    cts.Cancel();
                }
                catch (Exception ex)
                {
                    Logger?.Warn(ex, "An error occurred while cancelling a download.");
                }
            }
        }

        private async Task ProcessItem(DownloadQueueItem item, DownloadRequest req)
        {
            bool downloadFailed = false;

            await concurrencyGate.WaitAsync().ConfigureAwait(false);

            try
            {
                await Download(item, req).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                item.SetStatus(DownloadStatus.Canceled, Loc.GetString("DownloadStatusCanceled"));
                item.SetProgress(0, 1, false);

                TryCleanupPartialInstall(req);

                req.InstallCompletion.TrySetCanceled();
                await req.OnCancelled.Invoke();
                
                await Task.Delay(3000).ConfigureAwait(false);
                downloadFailed = true;
            }
            catch (Exception ex)
            {
                item.SetStatus(DownloadStatus.Failed, Loc.GetString("DownloadStatusFailed"));
                TryCleanupPartialInstall(req);

                req.InstallCompletion.TrySetException(ex);
                await req.OnFailed.Invoke(ex);
                
                await Task.Delay(3000).ConfigureAwait(false);
                downloadFailed = true;
            }
            finally 
            {
                concurrencyGate.Release();
            }

            // Exit if download failed
            if (downloadFailed)
            {
                activeDownloads.TryRemove(item.Id, out _);
                RemoveFromList(item);
                return;
            }
               
            try
            {
                await req.OnDownloadComplete.Invoke(item, req);

                item.SetStatus(DownloadStatus.Completed, Loc.GetString("DownloadStatusCompleted"));
                item.SetProgress(item.ProgressMaximum, item.ProgressMaximum, false);

                req.InstallCompletion.TrySetResult(true);
            }
            catch (OperationCanceledException)
            {
                item.SetStatus(DownloadStatus.Canceled, Loc.GetString("DownloadStatusCanceled"));
                item.SetProgress(0, 1, false);

                TryCleanupPartialInstall(req);

                await req.OnCancelled.Invoke();

                req.InstallCompletion.TrySetCanceled();
            }
            catch (Exception ex)
            {
                item.SetStatus(DownloadStatus.Failed, Loc.GetString("DownloadStatusFailed"));
                TryCleanupPartialInstall(req);

                await req.OnFailed.Invoke(ex);

                req.InstallCompletion.TrySetException(ex);
            }
            finally
            {
                activeDownloads.TryRemove(item.Id, out _);

                await Task.Delay(3000).ConfigureAwait(false);
                RemoveFromList(item);
            }
        }

        private async Task Download(DownloadQueueItem item, DownloadRequest req)
        {
            var ct = item.Cts.Token;

            item.SetStatus(DownloadStatus.Downloading, Loc.GetString("DownloadStatusDownloading"));
            item.SetProgress(0, 1, true);

            var response = await _romMServer.RawGETAsync(req.DownloadUrl);
            if (response == null || response.Content == null)
                throw new Exception(Loc.GetString("DownloadServerNullResponse"));

            if (response.Status == null || (int)response.Status < 200 || (int)response.Status >= 300)
            {
                throw new HttpRequestException($"Download request returned HTTP {(int?)response.Status} ({response.Status})");
            }

            var totalBytes = response.Content.Headers.ContentLength;
            item.SetProgress(0, totalBytes ?? 1, !totalBytes.HasValue);

            var downloadDirectory = Path.GetDirectoryName(req.DownloadPath);

            if (!string.IsNullOrEmpty(downloadDirectory))
                Directory.CreateDirectory(downloadDirectory);

            byte[] buffer = new byte[1024 * 256];
            long downloaded = 0;
            long lastUiUpdate = 0;
            const long uiUpdateThreshold = 1024 * 512; // 512KB

            using (var httpStream = response.Content.ReadAsStream())
            using (var fileStream = new FileStream(req.DownloadPath, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, true))
            {
                while (true)
                {
                    ct.ThrowIfCancellationRequested();

                    int read = await httpStream.ReadAsync(buffer, 0, buffer.Length, ct);
                    if (read <= 0)
                    {
                        break;
                    }

                    await fileStream.WriteAsync(buffer, 0, read, ct);

                    downloaded += read;

                    if (downloaded - lastUiUpdate >= uiUpdateThreshold)
                    {
                        lastUiUpdate = downloaded;

                        if (totalBytes.HasValue && totalBytes.Value > 0)
                        {
                            item.SetProgress(downloaded, totalBytes.Value, false);
                            var pct = (double)downloaded / totalBytes.Value * 100.0;
                            item.SetStatus(DownloadStatus.Downloading, Loc.GetString("DownloadStatusDownloadingPct", ("Percent", pct.ToString("0"))));
                        }
                        else
                        {
                            item.SetProgress(downloaded, Math.Max(1, downloaded), true);
                            item.SetStatus(DownloadStatus.Downloading, Loc.GetString("DownloadStatusDownloading"));
                        }
                    }
                }
            }     
        }

        private void RemoveFromList(DownloadQueueItem item)
        {
            if (item == null) return;

            UIDispatcher.Invoke(() => DownloadQueueVM.Items.Remove(item));
        }

        private void TryCleanupPartialInstall(DownloadRequest req)
        {
            try
            {
                // delete partial downloaded file first
                SafeDeleteFileWithRetry(req.DownloadPath);

                // delete folder (recursively) if it exists
                var tempDir = Path.GetDirectoryName(req.DownloadPath);
                if (!string.IsNullOrEmpty(tempDir))
                    SafeDeleteDirectoryWithRetry(tempDir);
            }
            catch (Exception ex)
            {
                Logger?.Warn(ex, $"Cleanup failed for {req.DisplayName}.");
            }
        }

        private static void SafeDeleteFileWithRetry(string path, int retries = 6, int delayMs = 150)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            for (int i = 0; i < retries; i++)
            {
                try
                {
                    if (!File.Exists(path))
                        return;

                    // clear attributes that can block deletion
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                    return;
                }
                catch (IOException)
                {
                    Thread.Sleep(delayMs);
                }
                catch (UnauthorizedAccessException)
                {
                    Thread.Sleep(delayMs);
                }
            }
        }

        private static void SafeDeleteDirectoryWithRetry(string dir, int retries = 6, int delayMs = 150)
        {
            if (string.IsNullOrWhiteSpace(dir))
                return;

            for (int i = 0; i < retries; i++)
            {
                try
                {
                    if (!Directory.Exists(dir))
                        return;

                    // make sure children are deletable
                    ClearAttributesRecursive(dir);

                    Directory.Delete(dir, recursive: true);
                    return;
                }
                catch (IOException)
                {
                    Thread.Sleep(delayMs);
                }
                catch (UnauthorizedAccessException)
                {
                    Thread.Sleep(delayMs);
                }
            }
        }

        private static void ClearAttributesRecursive(string dir)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
                }
                foreach (var d in Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories))
                {
                    try { new DirectoryInfo(d).Attributes = FileAttributes.Normal; } catch { }
                }
                try { new DirectoryInfo(dir).Attributes = FileAttributes.Normal; } catch { }
            }
            catch
            {
                // ignore - best effort
            }
        }
    }
}
