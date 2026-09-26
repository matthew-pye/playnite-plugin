using CommunityToolkit.Mvvm.ComponentModel;

using System.Collections.ObjectModel;
using System.IO;

namespace Graviton.Models.Saves
{
    public partial class SaveDirectoryTree : FileTreeNode<SaveDirectoryTree>
    {
        [ObservableProperty] private bool? _isChecked = false;

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

            if (Children.All(x => x.IsChecked == true))
                computed = true;
            else if (Children.All(x => x.IsChecked == false))
                computed = false;
            else
                computed = null;

            if (IsChecked != computed)
                IsChecked = computed;

            Parent?.RecomputeCheckedFromChildren();
        }

        public static ObservableCollection<SaveDirectoryTree> Build(string rootPath, List<string> sourceFilePaths)
        {
            var roots = new ObservableCollection<SaveDirectoryTree>();

            foreach (var path in sourceFilePaths)
            {
                var isMapped = path.StartsWith(EmulatorMapping.SavePathToken, StringComparison.OrdinalIgnoreCase);

                string relative;

                if (isMapped)
                {
                    relative = path.Substring(EmulatorMapping.SavePathToken.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }
                else
                {
                    relative = Path.GetRelativePath(rootPath, path);

                    if (relative.StartsWith(".."))
                    {
                        // Outside the root - show the absolute path as its own branch
                        relative = path;
                    }
                }

                var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                ObservableCollection<SaveDirectoryTree> currentLevel = roots;
                SaveDirectoryTree? currentNode = null;

                var currentPath = isMapped ? EmulatorMapping.SavePathToken : rootPath;

                for (var i = 0; i < parts.Length; i++)
                {
                    currentPath = Path.Combine(currentPath, parts[i]);

                    var isLastPart = i == parts.Length - 1;

                    var existing = currentLevel.FirstOrDefault(x => x.Name.Equals(parts[i], StringComparison.OrdinalIgnoreCase));

                    if (existing == null)
                    {
                        if (isLastPart)
                        {
                            existing = isMapped ? new SaveDirectoryTree
                                                        {
                                                            Name = parts[i],
                                                            FullPath = currentPath,
                                                            IsDirectory = false,
                                                            Parent = currentNode
                                                        } 
                                                : BuildNode(currentPath, currentNode);
                        }
                        else
                        {
                            existing = new SaveDirectoryTree
                            {
                                Name = parts[i],
                                FullPath = currentPath,
                                IsDirectory = true,
                                Parent = currentNode
                            };
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

            foreach (var directory in Directory.GetDirectories(rootPath).OrderBy(x => x))
            {
                roots.Add(BuildNode(directory));
            }

            foreach (var file in Directory.GetFiles(rootPath).OrderBy(x => x))
            {
                roots.Add(new SaveDirectoryTree
                {
                    Name = Path.GetFileName(file),
                    FullPath = file,
                    IsDirectory = false
                });
            }

            return roots;
        }

        private static SaveDirectoryTree BuildNode(string path,SaveDirectoryTree? parent = null)
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
                foreach (var directory in Directory.GetDirectories(path).OrderBy(x => x))
                {
                    node.Children.Add(BuildNode(directory, node));
                }

                foreach (var file in Directory.GetFiles(path).OrderBy(x => x))
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
            catch
            {
            }

            return node;
        }

        public void CollectSelectedTopLevelPaths(List<string> results)
        {
            if (IsChecked == true)
            {
                results.Add(FullPath);
                return;
            }

            if (IsChecked != null)
                return;

            foreach (var child in Children)
                child.CollectSelectedTopLevelPaths(results);
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