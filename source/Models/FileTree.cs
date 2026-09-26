using CommunityToolkit.Mvvm.ComponentModel;

using System.Collections.ObjectModel;

namespace Graviton.Models
{
    public class FileTreeNode<TNode> : ObservableObject 
           where TNode : FileTreeNode<TNode>
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }

        public TNode? Parent { get; set; }

        public ObservableCollection<TNode> Children { get; } = new();
    }
}
