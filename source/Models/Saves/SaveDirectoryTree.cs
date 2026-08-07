using CommunityToolkit.Mvvm.ComponentModel;

using System.Collections.ObjectModel;
using System.IO;

namespace Graviton.Models.Saves
{
    public partial class SaveDirectoryTree : ObservableObject
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public ObservableCollection<SaveDirectoryTree> Children { get; set; } = new();

        [ObservableProperty] private bool? _isChecked = false;

        public SaveDirectoryTree? Parent { get; set; }

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

        public static ObservableCollection<SaveDirectoryTree> Build(string rootPath, List<string> sourceFilePaths)
        {
            var roots = new ObservableCollection<SaveDirectoryTree>();

            foreach (var path in sourceFilePaths)
            {
                bool isMapped = path.StartsWith(EmulatorMapping.SavePathToken, StringComparison.OrdinalIgnoreCase);

                string relative;
                if (isMapped)
                {
                    // Already relative under the placeholder - just strip the token + leading separator
                    relative = path.Substring(EmulatorMapping.SavePathToken.Length)
                                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }
                else
                {
                    relative = Path.GetRelativePath(rootPath, path);
                    if (relative.StartsWith(".."))
                        relative = path; // outside the root - show the absolute path as its own branch
                }

                var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                var currentLevel = roots;
                SaveDirectoryTree? currentNode = null;
                var currentPath = isMapped ? EmulatorMapping.SavePathToken : rootPath;

                for (int i = 0; i < parts.Length; i++)
                {
                    currentPath = Path.Combine(currentPath, parts[i]);
                    var isLastPart = i == parts.Length - 1;

                    var existing = currentLevel.FirstOrDefault(n => n.Name == parts[i]);
                    if (existing == null)
                    {
                        if (isLastPart)
                        {
                            existing = isMapped
                                // Virtual node - don't touch disk, we don't know if it's a directory
                                ? new SaveDirectoryTree { Name = parts[i], FullPath = currentPath, IsDirectory = false, Parent = currentNode }
                                : BuildNode(currentPath, currentNode);
                        }
                        else
                        {
                            existing = new SaveDirectoryTree { Name = parts[i], FullPath = currentPath, IsDirectory = true, Parent = currentNode };
                        }

                        currentLevel.Add(existing);
                    }

                    currentNode = existing;
                    currentLevel = existing.Children;
                }
            }

            return roots;
        }

        public static ObservableCollection<SaveDirectoryTree> BuildFromDisk(string rootPath)
        {
            var roots = new ObservableCollection<SaveDirectoryTree>();

            if (!Directory.Exists(rootPath))
                return roots;

            foreach (var dir in Directory.GetDirectories(rootPath).OrderBy(d => d))
                roots.Add(BuildNode(dir, null));

            foreach (var file in Directory.GetFiles(rootPath).OrderBy(f => f))
            {
                roots.Add(new SaveDirectoryTree
                {
                    Name = Path.GetFileName(file),
                    FullPath = file,
                    IsDirectory = false,
                    Parent = null
                });
            }

            return roots;
        }

        private static SaveDirectoryTree BuildNode(string path, SaveDirectoryTree? parent = null)
        {
            var node = new SaveDirectoryTree
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
                    node.Children.Add(BuildNode(dir, node));

                foreach (var file in Directory.GetFiles(path).OrderBy(f => f))
                {
                    node.Children.Add(new SaveDirectoryTree
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

        public static List<string> CollectSelectedPaths(IEnumerable<SaveDirectoryTree> roots)
        {
            var results = new List<string>();

            foreach (var root in roots)
                root.CollectSelectedTopLevelPaths(results);

            return results;
        }
    }
}