using CommunityToolkit.Mvvm.ComponentModel;

using System.IO;

namespace Graviton.Models
{
    public enum SaveSyncStatus 
    { 
        upload, 
        download, 
        conflict,
        no_op
    }

    public partial class SaveRow : ObservableObject
    {
        [ObservableProperty] private string _gameName = "";

        [ObservableProperty] List<DirectorySaveFile> _saveDirectoryView = new();
        [ObservableProperty] private int _saveID = -1;
        [ObservableProperty] private SaveSyncStatus _syncStatus;  
        
        [ObservableProperty] private bool _syncEnabled = false;

        public void BuildSaveDirectoryView(List<string> SourceFilePaths)
        {
            var roots = new List<DirectorySaveFile>();

            foreach (var path in SourceFilePaths)
            {
                var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var currentLevel = roots;
                string currentPath = "";

                for (int i = 0; i < parts.Length; i++)
                {
                    currentPath = currentPath.Length == 0 ? parts[i] : Path.Combine(currentPath, parts[i]);
                    bool isLastPart = i == parts.Length - 1;

                    var existing = currentLevel.FirstOrDefault(n => n.Name == parts[i]);
                    if (existing == null)
                    {
                        existing = new DirectorySaveFile
                        {
                            Name = parts[i],
                            FullPath = currentPath
                        };
                        currentLevel.Add(existing);
                    }

                    currentLevel = existing.Children;
                }
            }

            SaveDirectoryView = roots;
        }

    }


    public class DirectorySaveFile
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public List<DirectorySaveFile> Children { get; set; } = new();

        public static DirectorySaveFile Build(string path)
        {
            var node = new DirectorySaveFile { Name = Path.GetFileName(path), FullPath = path };
            if (string.IsNullOrEmpty(node.Name)) node.Name = path;

            if (Directory.Exists(path))
            {
                try
                {
                    foreach (var dir in Directory.GetDirectories(path))
                        node.Children.Add(Build(dir));

                    foreach (var file in Directory.GetFiles(path))
                        node.Children.Add(new DirectorySaveFile { Name = Path.GetFileName(file), FullPath = file });
                }
                catch { }
            }

            return node;
        }
    }

}