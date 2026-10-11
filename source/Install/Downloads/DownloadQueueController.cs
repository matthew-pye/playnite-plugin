using Graviton.Models.Install;
using Graviton.Notifications;

using Playnite;

using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace Graviton.Install.Downloads
{
    public class DownloadQueueController
    {
        private IGravitonContext _plugin;
        private IPlayniteApi _playniteAPI;
        private GravitonLogger _logger;
        private IRomMServer _romMServer;

        private readonly DownloadQueueViewModel DownloadQueueVM;
        private readonly SemaphoreSlim concurrencyGate;


        private readonly ConcurrentDictionary<string, CancellationTokenSource> activeDownloads = new();

        public int MaxConcurrent { get; }

        private readonly Action<Action> _dispatchToUI;
        private readonly Func<Task> _finishDelay;
        private readonly Action<Func<Task>> _startWorker;

        internal DownloadQueueController(IGravitonContext plugin, IPlayniteApi playniteAPI, GravitonLogger logger, IRomMServer romMServer, DownloadQueueViewModel downloadQueueVM, int maxConcurrent, Action<Action>? dispatchToUI = null, Func<Task>? finishDelay = null, Action<Func<Task>>? startWorker = null) 
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;
            _romMServer = romMServer;

            DownloadQueueVM = downloadQueueVM;

            MaxConcurrent = Math.Max(1, maxConcurrent);
            concurrencyGate = new SemaphoreSlim(MaxConcurrent, MaxConcurrent);

            _dispatchToUI = dispatchToUI ?? UIDispatcher.Invoke;
            _finishDelay = finishDelay ?? (() =>
            {
                return Task.Delay(3000);
            });
            _startWorker = startWorker ?? (work =>
            {
                _ = Task.Run(work);
            });
        }

        public DownloadQueueViewModel ViewModel => DownloadQueueVM;

        public void Enqueue(DownloadRequest req, DownloadRequestBackup backup)
        {
            var item = new DownloadQueueItem
            {
                Id = req.Id,
                DisplayName = req.DisplayName,
                QueuedOn = DateTime.Now,
                Cts = new CancellationTokenSource()
            };

            // Backup download to be restored if needed
            var backupPath = Path.Combine(_plugin.PluginDataPath, "temp", "downloads");
            var tempPath = Path.Combine(backupPath, $"{req.Id}.tmp");

            if(!Directory.Exists(backupPath))
                Directory.CreateDirectory(backupPath);

            File.WriteAllText(tempPath, JsonSerializer.Serialize(backup));

            activeDownloads[item.Id] = item.Cts;

            item.SetStatus(DownloadStatus.Queued, _playniteAPI.GetLocalizedString("DownloadStatusQueued"));
            item.SetProgress(0, 1, true);

            _dispatchToUI(() =>
            {
                DownloadQueueVM.Items.Add(item);
            });

            // fire and forget background worker
            _startWorker(() =>
            {
                return ProcessItem(item, req);
            });
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
                    _logger?.Warn(ex, "An error occurred while cancelling a download.");
                }
            }
        }

        public bool IsDownloading(string Id) => activeDownloads.TryGetValue(Id, out _);

        private async Task ProcessItem(DownloadQueueItem item, DownloadRequest req)
        {
            bool downloadFailed = false;
            bool downloadStarted = false;

            try
            {
                await concurrencyGate.WaitAsync(item.Cts.Token).ConfigureAwait(false);

                downloadStarted = true;
                await Download(item, req).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                item.SetStatus(DownloadStatus.Canceled, _playniteAPI.GetLocalizedString("DownloadStatusCanceled"));
                item.SetProgress(0, 1, false);

                TryCleanupTempDirectory(req);

                req.InstallCompletion.TrySetCanceled();
                await req.OnCancelled.Invoke();
                
                await _finishDelay().ConfigureAwait(false);
                downloadFailed = true;
            }
            catch (Exception ex)
            {
                item.SetStatus(DownloadStatus.Failed, _playniteAPI.GetLocalizedString("DownloadStatusFailed"));
                TryCleanupTempDirectory(req);

                req.InstallCompletion.TrySetException(ex);
                await req.OnFailed.Invoke(ex);
                
                await _finishDelay().ConfigureAwait(false);
                downloadFailed = true;
            }
            finally 
            {
                if(downloadStarted)
                    concurrencyGate.Release();
            }

            // Exit if download failed
            if (downloadFailed)
            {
                activeDownloads.TryRemove(item.Id, out _);
                RemoveFromList(item);

                var tempPath = Path.Combine(_plugin.PluginDataPath, "temp", "downloads", $"{req.Id}.tmp");
                if(File.Exists(tempPath))
                    File.Delete(tempPath);

                return;
            }
               
            try
            {
                if (req.WaitForInstall != null)
                {
                    item.SetProgress(0, 1, true);
                    item.SetStatus(DownloadStatus.Waiting, _playniteAPI.GetLocalizedString("DownloadStatusWaiting"));
                    await req.WaitForInstall.WaitAsync(item.Cts.Token);
                }

                await req.OnDownloadComplete.Invoke(item, req);

                item.SetStatus(DownloadStatus.Completed, _playniteAPI.GetLocalizedString("DownloadStatusCompleted"));
                item.SetProgress(item.ProgressMaximum, item.ProgressMaximum, false);

                req.InstallCompletion.TrySetResult(true);
            }
            catch (OperationCanceledException)
            {
                item.SetStatus(DownloadStatus.Canceled, _playniteAPI.GetLocalizedString("DownloadStatusCanceled"));
                item.SetProgress(0, 1, false);

                req.InstallCompletion.TrySetCanceled();
                await req.OnCancelled.Invoke();

            }
            catch (Exception ex)
            {
                item.SetStatus(DownloadStatus.Failed, _playniteAPI.GetLocalizedString("DownloadStatusFailed"));

                req.InstallCompletion.TrySetException(ex);
                await req.OnFailed.Invoke(ex); 
            }
            finally
            {
                TryCleanupTempDirectory(req);
                activeDownloads.TryRemove(item.Id, out _);

                await _finishDelay().ConfigureAwait(false);
                RemoveFromList(item);

                var tempPath = Path.Combine(_plugin.PluginDataPath, "temp", "downloads", $"{req.Id}.tmp");
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        private async Task Download(DownloadQueueItem item, DownloadRequest req)
        {
            var ct = item.Cts.Token;

            item.SetStatus(DownloadStatus.Downloading, _playniteAPI.GetLocalizedString("DownloadStatusDownloading"));
            item.SetProgress(0, 1, true);

            using var response = await _romMServer.RawGETAsync(req.DownloadUrl);
            if (response == null || response.Content == null)
                throw new Exception(_playniteAPI.GetLocalizedString("DownloadServerNullResponse"));

            if ((int)response.Status < 200 || (int)response.Status >= 300)
            {
                throw new HttpRequestException($"Download request returned HTTP {(int?)response.Status} ({response.Status})");
            }

            var totalBytes = response.Content.Headers.ContentLength;
            item.SetProgress(0, totalBytes ?? 1, !totalBytes.HasValue);

            using var httpStream = response.Content.ReadAsStream();

            await CopyDownloadToFileAsync(httpStream, req.DownloadPath, downloaded =>
                {
                    if (totalBytes.HasValue && totalBytes.Value > 0)
                    {
                        item.SetProgress(downloaded, totalBytes.Value, false);
                        var pct = (double)downloaded / totalBytes.Value * 100.0;
                        item.SetStatus(DownloadStatus.Downloading, _playniteAPI.GetLocalizedString("DownloadStatusDownloadingPct", ("Percent", pct.ToString("0"))));
                    }
                    else
                    {
                        item.SetProgress(downloaded, Math.Max(1, downloaded), true);
                        item.SetStatus(DownloadStatus.Downloading, _playniteAPI.GetLocalizedString("DownloadStatusDownloading"));
                    }
                },
                ct).ConfigureAwait(false);
        }

        internal static async Task CopyDownloadToFileAsync(Stream source, string downloadPath, Action<long> onProgress, CancellationToken cancellationToken)
        {
            var downloadDirectory = Path.GetDirectoryName(downloadPath);

            if (!string.IsNullOrEmpty(downloadDirectory))
                Directory.CreateDirectory(downloadDirectory);

            byte[] buffer = new byte[1024 * 256];
            long downloaded = 0;
            long lastUiUpdate = 0;
            const long uiUpdateThreshold = 1024 * 512;

            using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, true);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int read = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (read <= 0)
                    break;
                
                await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
                downloaded += read;

                if (downloaded - lastUiUpdate >= uiUpdateThreshold)
                {
                    lastUiUpdate = downloaded;
                    onProgress(downloaded);
                }
            }
        }

        private void RemoveFromList(DownloadQueueItem item)
        {
            if (item == null) return;

            _dispatchToUI(() =>
            {
                DownloadQueueVM.Items.Remove(item);
            });
        }

        private void TryCleanupTempDirectory(DownloadRequest req)
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
                _logger?.Warn(ex, $"Cleanup failed for {req.DisplayName}.");
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
