using CommunityToolkit.Mvvm.ComponentModel;

using System.Collections.ObjectModel;

namespace Graviton.Models
{
    public class FileTreeNode<TNode> : ObservableObject 
                        where TNode  : FileTreeNode<TNode>
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }

        public TNode? Parent { get; set; }

        public ObservableCollection<TNode> Children { get; } = new();

        public bool MatchesShape(IReadOnlyCollection<string> shape)
        {
            return shape.All(HasRelativePath);
        }

        private bool HasRelativePath(string relativePath)
        {
            var expectsDirectory = relativePath.EndsWith('/');

            var parts = relativePath.Replace('\\', '/').Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

            FileTreeNode<TNode>? current = this;

            foreach (var part in parts)
            {
                current = current.Children.FirstOrDefault(x => x.Name.Equals(part, StringComparison.OrdinalIgnoreCase));

                if (current == null)
                    return false;
            }

            return expectsDirectory ? current.IsDirectory : !current.IsDirectory;
        }
    }
}
