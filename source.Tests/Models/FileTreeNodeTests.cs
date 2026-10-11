using Graviton.Models.Install;
using Graviton.Models.RomM.Rom;

using Xunit;

namespace Graviton.Tests.Models;

public sealed class FileTreeNodeTests
{
    [Fact]
    public void MatchesShape_WithDifferentCasing_MatchesFile()
    {
        var root = CreateArchiveRoot("sce_sys/param.sfo");

        var matches = root.MatchesShape(["SCE_SYS/PARAM.SFO"]);

        Assert.True(matches);
    }

    [Fact]
    public void MatchesShape_WithAllRequiredPaths_ReturnsTrue()
    {
        var root = CreateArchiveRoot("sce_sys/param.sfo", "eboot.bin");

        var matches = root.MatchesShape(["sce_sys/param.sfo", "eboot.bin"]);

        Assert.True(matches);
    }

    [Fact]
    public void MatchesShape_WhenRequiredFileIsMissing_ReturnsFalse()
    {
        var root = CreateArchiveRoot("sce_sys/param.sfo");

        var matches = root.MatchesShape(["sce_sys/param.sfo", "eboot.bin"]);

        Assert.False(matches);
    }

    [Fact]
    public void MatchesShape_WithTrailingSlash_RequiresDirectory()
    {
        var root = CreateArchiveRoot("code/game.rpx");

        var directoryMatches = root.MatchesShape(["code/"]);
        var fileMatches = root.MatchesShape(["code"]);

        Assert.True(directoryMatches);
        Assert.False(fileMatches);
    }

    [Fact]
    public void MatchesShape_WhenDirectoryIsExpected_FileDoesNotMatch()
    {
        var root = CreateArchiveRoot("patch.bin");

        var matches = root.MatchesShape(["patch.bin/"]);

        Assert.False(matches);
    }

    [Fact]
    public void MatchesShape_WithRegex_MatchesFileName()
    {
        var root = CreateArchiveRoot("TU_ABC1234_1234567890123.zip");
        var pattern = TitleIDInstallDefinitions.RegexPrefix + @"^TU_[A-Z0-9]{7}_[0-9]{13}\.";

        var matches = root.MatchesShape([pattern]);

        Assert.True(matches);
    }

    [Fact]
    public void MatchesShape_WithRegex_RejectsNonmatchingFileName()
    {
        var root = CreateArchiveRoot("game.zip");
        var pattern = TitleIDInstallDefinitions.RegexPrefix + @"^TU_[A-Z0-9]{7}_[0-9]{13}\.";

        var matches = root.MatchesShape([pattern]);

        Assert.False(matches);
    }

    [Fact]
    public void MatchesShape_WithInvalidRegex_ReturnsFalse()
    {
        var root = CreateArchiveRoot("patch.bin");
        var pattern = TitleIDInstallDefinitions.RegexPrefix + "[";

        var matches = root.MatchesShape([pattern]);

        Assert.False(matches);
    }

    private static ArchiveMemberTree CreateArchiveRoot(params string[] paths)
    {
        var members = paths.Select(path => new RomMArchiveMembers
        {
            Name = path
        }).ToList();

        var roots = ArchiveMemberTree.Build(members);
        var archiveRoot = new ArchiveMemberTree
        {
            IsDirectory = true
        };

        foreach (var root in roots)
        {
            archiveRoot.Children.Add(root);
        }

        return archiveRoot;
    }
}
