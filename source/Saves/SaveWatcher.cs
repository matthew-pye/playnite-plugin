using Graviton.Notifications;

using Playnite;

using System.IO;

namespace Graviton.Saves
{
    public class SaveWatcher
    {
        private readonly IGravitonContext _plugin;
        private readonly GravitonLogger _logger;

        private readonly Func<int, byte[]?> _getScreenshot;
        private readonly Func<DateTime> _utcNow;

        public byte[]? NewestSaveScreenshot;

        private readonly List<FileSystemWatcher> FileWatchers = new();
        private DateTime LastFileUpdate = DateTime.MinValue;
        private readonly object _fileUpdateLock = new();

        internal SaveWatcher(IGravitonContext plugin, IPlayniteApi playniteAPI, GravitonLogger logger, ScreenshotService service) : this(plugin, playniteAPI, logger, service.GetScreenshotFromSecondsAgo) {}

        internal SaveWatcher(IGravitonContext plugin, IPlayniteApi playniteAPI, GravitonLogger logger, Func<int, byte[]?> getScreenshot, Func<DateTime>? utcNow = null)
        {
            _plugin = plugin;
            _logger = logger;
            _getScreenshot = getScreenshot;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public bool Setup(List<string> sourcePaths)
        {
            // Dispose any old file watchers
            foreach (var watcher in FileWatchers)
            {
                watcher.Dispose();
            }
            FileWatchers.Clear();

            NewestSaveScreenshot = null;
            LastFileUpdate = DateTime.MinValue;

            var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var sourcePath in sourcePaths)
            {
                if (string.IsNullOrWhiteSpace(sourcePath))
                    continue;
                
                string path;
                try
                {
                    path = Path.GetFullPath(sourcePath);
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    _logger.Error($"Invalid save watch path: {sourcePath}");
                    continue;
                }

                if (!uniquePaths.Add(path))
                    continue;
                
                if (Directory.Exists(path))
                {
                    FileSystemWatcher watcher = new()
                    {
                        Path = path,
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                    };
                    watcher.Created += OnChanged;
                    watcher.Changed += OnChanged;
                    watcher.Renamed += OnChanged;
                    FileWatchers.Add(watcher);
                }
                else
                {
                    var dir = Path.GetDirectoryName(path);
                    var isDirectoryPath = sourcePath.EndsWith(Path.DirectorySeparatorChar) || sourcePath.EndsWith(Path.AltDirectorySeparatorChar);

                    if (isDirectoryPath || dir == null || !Directory.Exists(dir))
                    {
                        _logger.Error($"{sourcePath} doesn't exist cannot watch!");
                        continue;
                    }

                    FileSystemWatcher watcher = new()
                    {
                        Path = dir,
                        IncludeSubdirectories = false,
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                        Filter = Path.GetFileName(path),
                    };
                    watcher.Created += OnChanged;
                    watcher.Changed += OnChanged;
                    watcher.Renamed += OnChanged;
                    FileWatchers.Add(watcher);
                }
            }

            return FileWatchers.Count > 0;
        }

        public Task Start()
        {
            foreach (var watcher in FileWatchers)
            {
                watcher.EnableRaisingEvents = true;
            }

            return Task.CompletedTask;
        }

        public Task Stop()
        {
            foreach (var watcher in FileWatchers)
            {
                watcher.Dispose();
            }

            FileWatchers.Clear();
            return Task.CompletedTask;
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            var now = _utcNow();
            lock (_fileUpdateLock)
            {
                if ((now - LastFileUpdate) <= TimeSpan.FromMilliseconds(500))
                    return;
                
                try
                {
                    var image = _getScreenshot(_plugin.Settings.SecondsBeforeSave);
                    if (image != null)
                    {
                        NewestSaveScreenshot = image;
                        LastFileUpdate = now;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to capture save screenshot: {ex}");
                }
            }
        }
    }
}
