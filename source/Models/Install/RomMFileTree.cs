using CommunityToolkit.Mvvm.ComponentModel;

using Graviton.Models.RomM.Rom;

using System.Collections.ObjectModel;
using System.IO;

namespace Graviton.Models.Install
{
    public partial class RomMFileTree : FileTreeNode<RomMFileTree>
    {
        [ObservableProperty] private RomMFile? _file;

        // Builds a file tree from the ROM files 
        public static ObservableCollection<RomMFileTree> Build(string rootPath, List<RomMFile> files)
        {
            var roots = new ObservableCollection<RomMFileTree>();

            foreach (var file in files)
            {
                // Remove the ROMs full path from the search and split the path into parts
                var parts = Path.GetRelativePath(rootPath, file.FullPath).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                ObservableCollection<RomMFileTree> currentLevel = roots;
                RomMFileTree? currentNode = null;

                string currentPath = ""; 

                // Loop through all the parts and create nodes for each folder and file
                for (int i = 0; i < parts.Length; i++)
                {
                    currentPath = string.IsNullOrEmpty(currentPath) ? parts[i] : Path.Combine(currentPath, parts[i]);
                    var isLastPart = i == parts.Length - 1;

                    // Check to see if folder/file already exists then skip creating a new node if it does 
                    var existing = currentLevel.FirstOrDefault(x => x.Name.Equals(parts[i], StringComparison.OrdinalIgnoreCase));

                    if (existing == null)
                    {
                        existing = new RomMFileTree
                        {
                            Name = parts[i],
                            FullPath = currentPath,
                            IsDirectory = !isLastPart,
                            Parent = currentNode,
                            File = isLastPart ? file : null
                        };

                        currentLevel.Add(existing);
                    }

                    currentNode = existing;
                    currentLevel = existing.Children;
                }
            }

            return roots;
        }

        public static List<RomMFileTree> FindMatchingRoots(IEnumerable<RomMFileTree> roots, IReadOnlyCollection<UpdateDLCShape> shapes)
        {
            var matches = new List<RomMFileTree>();

            foreach (var root in roots)
                root.FindMatchingRoots(shapes, matches);

            return matches;
        }

        private void FindMatchingRoots(IReadOnlyCollection<UpdateDLCShape> shapes, List<RomMFileTree> matches)
        {
            if (!IsDirectory)
                return;

            // Check to see if the current node matches the shape we are looking for
            if (shapes.Any(x => MatchesShape(x.paths)))
            {
                matches.Add(this);
                return;
            }

            // Check children node to see if they match the shape we are looking for
            foreach (var child in Children.Where(x => x.IsDirectory))
                child.FindMatchingRoots(shapes, matches);
        }

        private bool MatchesShape(IReadOnlyCollection<string> paths)
        {
            return paths.All(HasRelativePath);
        }

        private bool HasRelativePath(string relativePath)
        {
            // Check to see if the shape is looking for a folder name
            var expectsDirectory = relativePath.EndsWith('/');

            var parts = relativePath.Replace('\\', '/').Trim('/').Split('/',StringSplitOptions.RemoveEmptyEntries);

            RomMFileTree? current = this;

            foreach (var part in parts)
            {
                current = current.Children.FirstOrDefault(x => x.Name.Equals(part, StringComparison.OrdinalIgnoreCase));

                if (current == null)
                    return false;
            }

            return expectsDirectory ? current.IsDirectory : !current.IsDirectory;
        }

        public List<int> CollectFileIDs()
        {
            var ids = new List<int>();

            CollectFileIDs(this, ids);

            return ids;
        }

        private static void CollectFileIDs(RomMFileTree node, List<int> ids)
        {
            if (!node.IsDirectory)
            {
                if (node.File != null)
                    ids.Add(node.File.Id);

                return;
            }

            foreach (var child in node.Children)
                CollectFileIDs(child, ids);
        }
    }
}