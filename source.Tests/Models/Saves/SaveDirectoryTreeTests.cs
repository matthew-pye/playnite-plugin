using Graviton.Models.Saves;

using Xunit;

namespace Graviton.Tests.Models.Saves;

public sealed class SaveDirectoryTreeTests
{
    [Fact]
    public void IsChecked_WhenParentChecked_ChecksAllChildren()
    {
        var (root, first, second) = CreateTree();

        root.IsChecked = true;

        Assert.True(first.IsChecked);
        Assert.True(second.IsChecked);
    }

    [Fact]
    public void IsChecked_WhenParentUnchecked_UnchecksAllChildren()
    {
        var (root, first, second) = CreateTree();
        root.IsChecked = true;

        root.IsChecked = false;

        Assert.False(first.IsChecked);
        Assert.False(second.IsChecked);
    }

    [Fact]
    public void IsChecked_WhenOneChildChecked_ParentBecomesIndeterminate()
    {
        var (root, first, second) = CreateTree();

        first.IsChecked = true;

        Assert.Null(root.IsChecked);
        Assert.False(second.IsChecked);
    }

    [Fact]
    public void IsChecked_WhenAllChildrenChecked_ParentBecomesChecked()
    {
        var (root, first, second) = CreateTree();

        first.IsChecked = true;
        second.IsChecked = true;

        Assert.True(root.IsChecked);
    }

    [Fact]
    public void IsChecked_WhenAllChildrenUnchecked_ParentBecomesUnchecked()
    {
        var (root, first, second) = CreateTree();
        root.IsChecked = true;

        first.IsChecked = false;
        second.IsChecked = false;

        Assert.False(root.IsChecked);
    }

    [Fact]
    public void CollectSelectedPaths_WhenParentChecked_ReturnsOnlyParentPath()
    {
        var (root, _, _) = CreateTree();
        root.IsChecked = true;

        var selectedPaths = SaveDirectoryTree.CollectSelectedPaths([root]);

        Assert.Equal(new[] { "Saves" }, selectedPaths);
    }

    [Fact]
    public void CollectSelectedPaths_WhenSelectionIsMixed_ReturnsOnlySelectedChildPath()
    {
        var (root, first, _) = CreateTree();
        first.IsChecked = true;

        var selectedPaths = SaveDirectoryTree.CollectSelectedPaths([root]);

        Assert.Equal(new[] { "Saves/slot1.sav" }, selectedPaths);
    }

    [Fact]
    public void IsChecked_WhenParentChecked_ChecksNestedDescendants()
    {
        var (root, _, _) = CreateTree();
        var directory = new SaveDirectoryTree
        {
            Name = "Profiles",
            FullPath = "Saves/Profiles",
            IsDirectory = true,
            Parent = root
        };
        var nestedFile = new SaveDirectoryTree
        {
            Name = "profile.sav",
            FullPath = "Saves/Profiles/profile.sav",
            Parent = directory
        };
        directory.Children.Add(nestedFile);
        root.Children.Add(directory);

        root.IsChecked = true;

        Assert.True(directory.IsChecked);
        Assert.True(nestedFile.IsChecked);
    }

    private static (SaveDirectoryTree Root, SaveDirectoryTree First, SaveDirectoryTree Second) CreateTree()
    {
        var root = new SaveDirectoryTree
        {
            Name = "Saves",
            FullPath = "Saves",
            IsDirectory = true
        };
        var first = new SaveDirectoryTree
        {
            Name = "slot1.sav",
            FullPath = "Saves/slot1.sav",
            Parent = root
        };
        var second = new SaveDirectoryTree
        {
            Name = "slot2.sav",
            FullPath = "Saves/slot2.sav",
            Parent = root
        };

        root.Children.Add(first);
        root.Children.Add(second);

        return (root, first, second);
    }
}
