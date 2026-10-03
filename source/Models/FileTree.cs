using CommunityToolkit.Mvvm.ComponentModel;

using Graviton.Models.Install;

using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

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
                current = current.Children.FirstOrDefault(x => NameMatches(x.Name, part));

                if (current == null)
                    return false;
            }

            return expectsDirectory ? current.IsDirectory : !current.IsDirectory;
        }

        private static bool NameMatches(string actual, string pattern)
        {
            if (pattern.StartsWith(TitleIDInstallDefinitions.RegexPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var regex = pattern.Substring(TitleIDInstallDefinitions.RegexPrefix.Length);
                
                if (string.IsNullOrEmpty(regex))
                    return false;

                try
                {
                    return Regex.IsMatch(actual, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }

            return actual.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }
    }
}
