using System.IO;

namespace Graviton.Saves
{
    public class SaveWatcher
    {
        public byte[]? NewestSaveScreenshot;

        private List<FileSystemWatcher> FileWatchers = new();
        private DateTime LastFileUpdate = DateTime.UtcNow;
        private readonly object _fileUpdateLock = new object();
        private ScreenshotService ScreenCapture;

        int index = 0;
        public SaveWatcher(ScreenshotService service)
        {
            ScreenCapture = service;
        }

        public bool Setup(List<string> sourcePaths)
        {
            NewestSaveScreenshot = null;

            foreach (var path in sourcePaths)
            {
                if(File.Exists(path))
                {
                    var dir = Path.GetDirectoryName(path);
                    if (dir == null)
                        continue;

                    FileSystemWatcher watcher = new()
                    {
                        Path = dir,
                        IncludeSubdirectories = false,
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                        Filter = Path.GetFileName(path),
                    };
                    watcher.Changed += OnChanged;
                    FileWatchers.Add(watcher);
                }
                else if (Directory.Exists(path))
                {
                    FileSystemWatcher watcher = new()
                    {
                        Path = path,
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                    };
                    watcher.Changed += OnChanged;
                    FileWatchers.Add(watcher);
                }
                else
                {
                    GravitonPlugin.Logger.Error($"{path} doesn't exist cannot watch!");
                }
            }

            if(FileWatchers.Count > 0)
            {
                return true;
            }

            return false;
        }

        public async Task Start()
        {
            foreach(var watcher in FileWatchers)
                watcher.EnableRaisingEvents = true;
        }

        public async Task Stop()
        {
            foreach (var watcher in FileWatchers)
                watcher.Dispose();

            FileWatchers.Clear();
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            var now = DateTime.UtcNow;
            lock (_fileUpdateLock)
            {
                // Debounce changes incase multiple file change at once don't clash with each other
                if ((now - LastFileUpdate) > TimeSpan.FromMilliseconds(500))
                {
                    var image = ScreenCapture.GetScreenshotFromSecondsAgo(GravitonPlugin.Instance.Settings.SecondsBeforeSave);
                    if(image != null)
                    {
                        NewestSaveScreenshot = image;
                        File.WriteAllBytes($"{GravitonPlugin.Instance.PluginDataPath}/temp/{index++}.jpg", image);
                        LastFileUpdate = now;
                    }
                }
            }
        }

    }
}
