using Graviton.Models.Install;
using Graviton.Models.RomM.Rom;

using Xunit;

namespace Graviton.Tests.Models.Install;

public sealed class ArchiveMemberTreeTests
{
    [Fact]
    public void Build_WithNoMembers_ReturnsEmptyTree()
    {
        var members = new List<RomMArchiveMembers>();

        var roots = ArchiveMemberTree.Build(members);

        Assert.Empty(roots);
    }

    [Fact]
    public void Build_WithMissingMemberNames_SkipsThem()
    {
        var members = new List<RomMArchiveMembers>
        {
            new() { Name = null },
            new() { Name = string.Empty }
        };

        var roots = ArchiveMemberTree.Build(members);

        Assert.Empty(roots);
    }

    [Fact]
    public void Build_WithMixedSeparators_CreatesNestedTree()
    {
        var members = new List<RomMArchiveMembers>
        {
            new() { Name = @"Game\Updates\patch.bin" },
            new() { Name = "Game/Updates/manifest.json" }
        };

        var roots = ArchiveMemberTree.Build(members);
        var gameFolder = Assert.Single(roots);
        var updatesFolder = Assert.Single(gameFolder.Children);

        Assert.Equal("Game", gameFolder.Name);
        Assert.Equal("Updates", updatesFolder.Name);
        Assert.Equal("Game/Updates", updatesFolder.FullPath);
        Assert.Same(gameFolder, updatesFolder.Parent);
        Assert.Equal(2, updatesFolder.Children.Count);

        Assert.All(updatesFolder.Children, child =>
        {
            Assert.False(child.IsDirectory);
            Assert.Same(updatesFolder, child.Parent);
            Assert.NotNull(child.Member);
        });
    }

    [Fact]
    public void Build_WithDifferentFolderCasing_UsesOneFolder()
    {
        var members = new List<RomMArchiveMembers>
        {
            new() { Name = "Updates/patch.bin" },
            new() { Name = "updates/manifest.json" }
        };

        var roots = ArchiveMemberTree.Build(members);
        var updatesFolder = Assert.Single(roots);

        Assert.Equal(2, updatesFolder.Children.Count);
    }

    [Fact]
    public void FindMatchingRoots_WhenArchiveRootMatches_ReturnsVirtualRoot()
    {
        var members = new List<RomMArchiveMembers>
        {
            new() { Name = "patch.bin" },
            new() { Name = "manifest.json" }
        };
        var shapes = new List<UpdateDLCShape>
        {
            new(new[] { "patch.bin", "manifest.json" })
        };
        var roots = ArchiveMemberTree.Build(members);

        var matches = ArchiveMemberTree.FindMatchingRoots(roots, shapes);
        var match = Assert.Single(matches);

        Assert.True(match.IsDirectory);
        Assert.Equal(string.Empty, match.FullPath);
        Assert.Equal(2, match.Children.Count);
    }

    [Fact]
    public void FindMatchingRoots_WhenNestedFolderMatches_ReturnsNestedFolder()
    {
        var members = new List<RomMArchiveMembers>
        {
            new() { Name = "Game/Updates/patch.bin" },
            new() { Name = "Game/Updates/manifest.json" }
        };
        var shapes = new List<UpdateDLCShape>
        {
            new(new[] { "patch.bin", "manifest.json" })
        };
        var roots = ArchiveMemberTree.Build(members);

        var matches = ArchiveMemberTree.FindMatchingRoots(roots, shapes);
        var match = Assert.Single(matches);

        Assert.Equal("Updates", match.Name);
        Assert.Equal("Game/Updates", match.FullPath);
        Assert.True(match.IsDirectory);
    }

    [Fact]
    public void FindMatchingRoots_WhenRequiredFileIsMissing_ReturnsNoMatches()
    {
        var members = new List<RomMArchiveMembers>
        {
            new() { Name = "Game/Updates/patch.bin" }
        };
        var shapes = new List<UpdateDLCShape>
        {
            new(new[] { "patch.bin", "manifest.json" })
        };
        var roots = ArchiveMemberTree.Build(members);

        var matches = ArchiveMemberTree.FindMatchingRoots(roots, shapes);

        Assert.Empty(matches);
    }
}
