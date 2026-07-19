using CommunityToolkit.Mvvm.ComponentModel;

using Graviton.Models.RomM.Saves;

using System.Collections.ObjectModel;
using System.IO;

namespace Graviton.Models
{
    public partial class SaveRow : ObservableObject
    {
        [ObservableProperty] private Guid? _localSaveGuid = null;

        [ObservableProperty] private string _gameName = "";  
        [ObservableProperty] private int _gameID = -1;  

        [ObservableProperty] private int _saveID = -1;
        [ObservableProperty] private SaveSyncStatus _syncStatus;         
        [ObservableProperty] private string? _slot;
        [ObservableProperty] private bool _syncEnabled = false;
        [ObservableProperty] private int _historicSaveCount = 0;

        [ObservableProperty] ObservableCollection<DirectorySaveFile> _saveDirectoryView = new();
        [ObservableProperty] List<string>? _sourcePaths = null;

        public void BuildSaveDirectoryView(string rootPath, List<string> sourceFilePaths)
        {
            var roots = new ObservableCollection<DirectorySaveFile>();

            foreach (var path in sourceFilePaths)
            {
                var relative = Path.GetRelativePath(rootPath, path).StartsWith("..") ? path : Path.GetRelativePath(rootPath, path);
                var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                var currentLevel = roots;
                string currentPath = rootPath;

                for (int i = 0; i < parts.Length; i++)
                {
                    currentPath = Path.Combine(currentPath, parts[i]);
                    var existing = currentLevel.FirstOrDefault(n => n.Name == parts[i]);
                    if (existing == null)
                    {
                        existing = new DirectorySaveFile { Name = parts[i], FullPath = currentPath };
                        currentLevel.Add(existing);
                    }
                    currentLevel = existing.Children;
                }
            }

            SaveDirectoryView = roots;
        }
    }


    public partial class DirectorySaveFile : ObservableObject
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public ObservableCollection<DirectorySaveFile> Children { get; set; } = new();

        [ObservableProperty] private bool? _isChecked = false;

        public DirectorySaveFile? Parent { get; set; }

        partial void OnIsCheckedChanged(bool? value)
        {
            if (value.HasValue)
                SetChildrenChecked(value.Value);
        
            Parent?.RecomputeCheckedFromChildren();
        }

        private void SetChildrenChecked(bool value)
        {
            foreach (var child in Children)
            {
                child.IsChecked = value;
                child.SetChildrenChecked(value);
            }
        }

        private void RecomputeCheckedFromChildren()
        {
            if (Children.Count == 0)
                return;

            bool? computed;
            if (Children.All(c => c.IsChecked == true))
                computed = true;
            else if (Children.All(c => c.IsChecked == false))
                computed = false;
            else
                computed = null;

            if (IsChecked != computed)
            {
                IsChecked = computed;
                OnPropertyChanged(nameof(IsChecked));
                Parent?.RecomputeCheckedFromChildren();
            }
        }

        public static DirectorySaveFile Build(string path, DirectorySaveFile? parent = null)
        {
            var node = new DirectorySaveFile
            {
                Name = string.IsNullOrEmpty(Path.GetFileName(path)) ? path : Path.GetFileName(path),
                FullPath = path,
                IsDirectory = Directory.Exists(path),
                Parent = parent
            };

            if (!node.IsDirectory)
                return node;

            try
            {
                foreach (var dir in Directory.GetDirectories(path).OrderBy(d => d))
                    node.Children.Add(Build(dir, node));

                foreach (var file in Directory.GetFiles(path).OrderBy(f => f))
                {
                    node.Children.Add(new DirectorySaveFile
                    {
                        Name = Path.GetFileName(file),
                        FullPath = file,
                        IsDirectory = false,
                        Parent = node
                    });
                }
            }
            catch { }

            return node;
        }

        public void CollectSelectedTopLevelPaths(List<string> results)
        {
            if (IsChecked == true)
            {
                results.Add(FullPath);
                return;
            }

            if (IsChecked == null)
            {
                foreach (var child in Children)
                    child.CollectSelectedTopLevelPaths(results);
            }
        }
    }

}