using CommunityToolkit.Mvvm.ComponentModel;

using Graviton.Models.RomM.Rom;

using System.Collections.ObjectModel;


namespace Graviton.Models.Install
{
    public partial class ArchiveMemberTree : FileTreeNode<ArchiveMemberTree>
    {
        [ObservableProperty]
        private RomMArchiveMembers? _member;

        public static ObservableCollection<ArchiveMemberTree> Build(IReadOnlyCollection<RomMArchiveMembers> members)
        {
            var roots = new ObservableCollection<ArchiveMemberTree>();

            foreach (var member in members)
            {
                var parts = member.Name?.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (parts == null)
                    continue;

                ObservableCollection<ArchiveMemberTree> currentLevel = roots;
                ArchiveMemberTree? currentNode = null;

                var currentPath = string.Empty;

                for (var i = 0; i < parts.Length; i++)
                {
                    currentPath = string.IsNullOrEmpty(currentPath) ? parts[i] : $"{currentPath}/{parts[i]}";

                    var isLastPart = i == parts.Length - 1;

                    var existing = currentLevel.FirstOrDefault(x => x.Name.Equals(parts[i], StringComparison.OrdinalIgnoreCase));

                    if (existing == null)
                    {
                        existing = new ArchiveMemberTree
                        {
                            Name = parts[i],
                            FullPath = currentPath,
                            IsDirectory = !isLastPart,
                            Parent = currentNode,
                            Member = isLastPart ? member : null
                        };

                        currentLevel.Add(existing);
                    }

                    currentNode = existing;
                    currentLevel = existing.Children;
                }
            }

            return roots;
        }

        public static List<ArchiveMemberTree> FindMatchingRoots(IEnumerable<ArchiveMemberTree> roots, IReadOnlyCollection<UpdateDLCShape> shapes)
        {
            var rootList = roots.ToList();
            var matches = new List<ArchiveMemberTree>();

            var archiveRoot = new ArchiveMemberTree
            {
                Name = string.Empty,
                FullPath = string.Empty,
                IsDirectory = true
            };

            foreach (var root in rootList)
                archiveRoot.Children.Add(root);

            // Check if the archive root itself matches the shape we are looking for
            if (shapes.Any(x => archiveRoot.MatchesShape(x.paths)))
            {
                matches.Add(archiveRoot);
                return matches;
            }

            foreach (var root in rootList)
                root.FindMatchingRoots(shapes, matches);

            return matches;
        }

        private void FindMatchingRoots(IReadOnlyCollection<UpdateDLCShape> shapes, List<ArchiveMemberTree> matches)
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
    }
}
