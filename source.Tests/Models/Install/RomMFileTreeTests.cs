using Graviton.Models.Install;
using Graviton.Models.RomM.Rom;

using System.IO;
using Xunit;

namespace Graviton.Tests.Models.Install;

public sealed class RomMFileTreeTests
{
    [Fact]
    public void Build_WithNoFiles_ReturnsEmptyTree()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "GravitonTests", "Roms");
        var files = new List<RomMFile>();

        var roots = RomMFileTree.Build(rootPath, files);

        Assert.Empty(roots);
    }

    [Fact]
    public void Build_WithFilesInSameFolder_GroupsThemTogether()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "GravitonTests", "Roms");
        var files = new List<RomMFile>
        {
            new()
            {
                Id = 101,
                FileName = "game.rom",
                FullPath = Path.Combine(rootPath, "Super Mario World", "game.rom")
            },
            new()
            {
                Id = 102,
                FileName = "manual.pdf",
                FullPath = Path.Combine(rootPath, "Super Mario World", "manual.pdf")
            }
        };

        var roots = RomMFileTree.Build(rootPath, files);
        var folder = Assert.Single(roots);

        Assert.Equal("Super Mario World", folder.Name);
        Assert.True(folder.IsDirectory);
        Assert.Equal(2, folder.Children.Count);
        Assert.Equal(new[] { 101, 102 }, folder.CollectFileIDs());
    }

    [Fact]
    public void Build_WithNestedFolders_SetsParentAndRelativePaths()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "GravitonTests", "Roms");
        var files = new List<RomMFile>
        {
            new()
            {
                Id = 201,
                FileName = "patch.bin",
                FullPath = Path.Combine(rootPath, "Update", "Data", "patch.bin")
            }
        };

        var roots = RomMFileTree.Build(rootPath, files);
        var updateFolder = Assert.Single(roots);
        var dataFolder = Assert.Single(updateFolder.Children);
        var file = Assert.Single(dataFolder.Children);

        Assert.Same(updateFolder, dataFolder.Parent);
        Assert.Same(dataFolder, file.Parent);
        Assert.Equal(Path.Combine("Update", "Data", "patch.bin"), file.FullPath);
        Assert.False(file.IsDirectory);
        Assert.Equal(201, Assert.Single(file.CollectFileIDs()));
    }

    [Fact]
    public void Build_WithDifferentFolderCasing_UsesOneFolder()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "GravitonTests", "Roms");
        var files = new List<RomMFile>
        {
            new()
            {
                Id = 301,
                FileName = "part1.bin",
                FullPath = Path.Combine(rootPath, "Updates", "part1.bin")
            },
            new()
            {
                Id = 302,
                FileName = "part2.bin",
                FullPath = Path.Combine(rootPath, "updates", "part2.bin")
            }
        };

        var roots = RomMFileTree.Build(rootPath, files);
        var folder = Assert.Single(roots);

        Assert.Equal(2, folder.Children.Count);
        Assert.Equal(new[] { 301, 302 }, folder.CollectFileIDs());
    }
}
