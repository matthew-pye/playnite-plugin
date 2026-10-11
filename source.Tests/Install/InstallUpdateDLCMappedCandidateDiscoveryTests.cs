using Graviton.Install;
using Graviton.Models.Install;
using Graviton.Models.RomM.Rom;
using Graviton.Tests.Fakes.Install;

using System.IO;

using Xunit;

namespace Graviton.Tests.Install;

[Collection("GravitonUpdateDLC")]
public sealed class InstallUpdateDLCMappedCandidateDiscoveryTests
{
    [Theory]
    [InlineData("update", "shadps4.update")]
    [InlineData("dlc", "shadps4.dlc")]
    public async Task Refresh_ShadhPS4SingleFile_TracksRelativePathWithoutCapturingSiblingFiles(string category, string definition)
    {
        using var test = new UpdateDLCInstallTestHarness();
        var root = Path.Combine(test.Root, "RomM", "42");
        Configure(test, category, definition);
        var local = test.RegisterLocalROM();
        var remote = Remote(root,
            DiskFile(root, 10, category, "Expansion/sce_sys/param.sfo", 100),
            DiskFile(root, 11, RomMCategory.Game, "game.iso", 200));

        await InstallUpdateDLC.RefreshCandidates(test.Mapping, remote, local, test.Plugin.Context);

        var candidate = Assert.Single(Candidates(local, category));
        Assert.Equal("Expansion", candidate.Name);
        Assert.Equal(new[] { 10 }, candidate.FileIDs);
        Assert.Equal("sce_sys/param.sfo", Normalized(candidate.SingleFileRelativePath));
        Assert.Null(candidate.CandidateRoot);
        Assert.Equal(100, candidate.Size);
    }

    [Theory]
    [InlineData("update", "cemu.update")]
    [InlineData("dlc", "cemu.dlc")]
    public async Task Refresh_CemuDirectoryShape_GroupsAllRequiredSubdirectories(string category, string definition)
    {
        using var test = new UpdateDLCInstallTestHarness();
        var root = Path.Combine(test.Root, "RomM", "42");
        Configure(test, category, definition);
        var local = test.RegisterLocalROM();
        var remote = Remote(root,
            DiskFile(root, 21, category, "Version 1/code/app.rpx", 100),
            DiskFile(root, 22, category, "Version 1/content/game.pack", 200),
            DiskFile(root, 23, category, "Version 1/meta/meta.xml", 300));

        await InstallUpdateDLC.RefreshCandidates(test.Mapping, remote, local, test.Plugin.Context);

        var candidate = Assert.Single(Candidates(local, category));
        Assert.Equal("Version 1", candidate.Name);
        Assert.Equal("Version 1.zip", candidate.FileName);
        Assert.Equal("Version 1", Normalized(candidate.CandidateRoot));
        Assert.Null(candidate.SingleFileRelativePath);
        Assert.Equal(new[] { 21, 22, 23 }, candidate.FileIDs);
        Assert.Equal(600, candidate.Size);
    }

    [Theory]
    [InlineData("code/app.rpx", "content/game.pack")]
    [InlineData("code/app.rpx", "meta/meta.xml")]
    [InlineData("content/game.pack", "meta/meta.xml")]
    public async Task Refresh_CemuIncompleteShape_DoesNotCreateInstallCandidate(string first, string second)
    {
        using var test = new UpdateDLCInstallTestHarness();
        var root = Path.Combine(test.Root, "RomM", "42");
        Configure(test, RomMCategory.Update, "cemu.update");
        var local = test.RegisterLocalROM();

        await InstallUpdateDLC.RefreshCandidates(test.Mapping, Remote(root,
            DiskFile(root, 1, RomMCategory.Update, $"Patch/{first}"),
            DiskFile(root, 2, RomMCategory.Update, $"Patch/{second}")), local, test.Plugin.Context);

        Assert.Empty(local.UpdateCandidates);
    }

    [Fact]
    public async Task Refresh_CemuSeparatePatchDirectories_AreDistinctCandidates()
    {
        using var test = new UpdateDLCInstallTestHarness();
        var root = Path.Combine(test.Root, "RomM", "42");
        Configure(test, RomMCategory.Update, "cemu.update");
        var local = test.RegisterLocalROM();
        var remote = Remote(root,
            DiskFile(root, 1, RomMCategory.Update, "Patch A/code/app.rpx"),
            DiskFile(root, 2, RomMCategory.Update, "Patch A/content/data.bin"),
            DiskFile(root, 3, RomMCategory.Update, "Patch A/meta/meta.xml"),
            DiskFile(root, 4, RomMCategory.Update, "Patch B/code/app.rpx"),
            DiskFile(root, 5, RomMCategory.Update, "Patch B/content/data.bin"),
            DiskFile(root, 6, RomMCategory.Update, "Patch B/meta/meta.xml"));

        await InstallUpdateDLC.RefreshCandidates(test.Mapping, remote, local, test.Plugin.Context);

        Assert.Equal(2, local.UpdateCandidates.Count);
        Assert.Equal(new[] { "Patch A", "Patch B" }, local.UpdateCandidates.Select(x => x.Name));
        Assert.Equal(new[] { 1, 2, 3 }, local.UpdateCandidates[0].FileIDs);
        Assert.Equal(new[] { 4, 5, 6 }, local.UpdateCandidates[1].FileIDs);
    }

    [Theory]
    [InlineData("TU_ABC1234_1234567890123.pkg", true)]
    [InlineData("TU_aBc1234_1234567890123.pkg", true)]
    [InlineData("TU_ABC123_1234567890123.pkg", false)]
    [InlineData("TU_ABC1234_123456789012.pkg", false)]
    [InlineData("regular_patch.pkg", false)]
    public async Task Refresh_XeniaTURecognisesFilenamePattern(string filename, bool expectedMatch)
    {
        using var test = new UpdateDLCInstallTestHarness();
        var root = Path.Combine(test.Root, "RomM", "42");
        Configure(test, RomMCategory.Update, "xenia.tuupdate");
        var local = test.RegisterLocalROM();

        await InstallUpdateDLC.RefreshCandidates(test.Mapping, Remote(root,
            DiskFile(root, 50, RomMCategory.Update, $"TitleUpdates/{filename}", 256)), local, test.Plugin.Context);

        if (expectedMatch)
        {
            var candidate = Assert.Single(local.UpdateCandidates);
            Assert.Equal("TitleUpdates", candidate.Name);
            Assert.Equal(new[] { 50 }, candidate.FileIDs);
            Assert.Equal(filename, Normalized(candidate.SingleFileRelativePath));
        }
        else
        {
            Assert.Empty(local.UpdateCandidates);
        }
    }

    [Theory]
    [InlineData("update", "shadps4.update")]
    [InlineData("dlc", "shadps4.dlc")]
    public async Task Refresh_ArchiveWithMatchingRoot_UsesWholeArchive(string category, string definition)
    {
        using var test = new UpdateDLCInstallTestHarness();
        var root = Path.Combine(test.Root, "RomM", "42");
        Configure(test, category, definition);
        var local = test.RegisterLocalROM();
        var archive = Archive(root, 80, category, "bundle.zip", 900,
            "sce_sys/param.sfo", "content/pack.bin");

        await InstallUpdateDLC.RefreshCandidates(test.Mapping, Remote(root, archive), local, test.Plugin.Context);

        var candidate = Assert.Single(Candidates(local, category));
        Assert.Equal("bundle", candidate.Name);
        Assert.Equal("bundle.zip", candidate.FileName);
        Assert.Equal(new[] { 80 }, candidate.FileIDs);
        Assert.Null(candidate.CandidateRoot);
        Assert.Null(candidate.SingleFileRelativePath);
        Assert.Equal(900, candidate.Size);
    }

    [Theory]
    [InlineData("update", "shadps4.update")]
    [InlineData("dlc", "shadps4.dlc")]
    public async Task Refresh_ArchiveWithMatchingNestedFolder_TracksOnlyThatRoot(string category, string definition)
    {
        using var test = new UpdateDLCInstallTestHarness();
        var root = Path.Combine(test.Root, "RomM", "42");
        Configure(test, category, definition);
        var local = test.RegisterLocalROM();

        await InstallUpdateDLC.RefreshCandidates(test.Mapping, Remote(root,
            Archive(root, 81, category, "bundle.zip", 600,
                "release/sce_sys/param.sfo", "release/content/data.bin", "unrelated/readme.txt")), local, test.Plugin.Context);

        var candidate = Assert.Single(Candidates(local, category));
        Assert.Equal("release", candidate.Name);
        Assert.Equal("release", Normalized(candidate.CandidateRoot));
        Assert.Equal("bundle.zip", candidate.FileName);
        Assert.Equal(new[] { 81 }, candidate.FileIDs);
    }

    [Fact]
    public async Task Refresh_ArchiveWithMultipleMatchingFolders_CreatesSeparateCandidates()
    {
        using var test = new UpdateDLCInstallTestHarness();
        var root = Path.Combine(test.Root, "RomM", "42");
        Configure(test, RomMCategory.DLC, "shadps4.dlc");
        var local = test.RegisterLocalROM();

        await InstallUpdateDLC.RefreshCandidates(test.Mapping, Remote(root,
            Archive(root, 82, RomMCategory.DLC, "costumes.zip", 450,
                "Costume A/sce_sys/param.sfo", "Costume B/sce_sys/param.sfo")), local, test.Plugin.Context);

        Assert.Equal(2, local.DLCCandidates.Count);
        Assert.Equal(new[] { "Costume A", "Costume B" }, local.DLCCandidates.Select(x => x.Name));
        Assert.All(local.DLCCandidates, candidate => Assert.Equal(new[] { 82 }, candidate.FileIDs));
        Assert.All(local.DLCCandidates, candidate => Assert.Equal(450, candidate.Size));
    }

    [Fact]
    public async Task Refresh_ArchiveWithoutMatchingShape_IsNotInstallable()
    {
        using var test = new UpdateDLCInstallTestHarness();
        var root = Path.Combine(test.Root, "RomM", "42");
        Configure(test, RomMCategory.Update, "shadps4.update");
        var local = test.RegisterLocalROM();

        await InstallUpdateDLC.RefreshCandidates(test.Mapping, Remote(root,
            Archive(root, 83, RomMCategory.Update, "patch.zip", 100,
                "sce_sys/readme.txt", "content/data.bin")), local, test.Plugin.Context);

        Assert.Empty(local.UpdateCandidates);
    }

    [Fact]
    public async Task Refresh_MixedDiskAndArchiveMatches_AreBothDiscoverable()
    {
        using var test = new UpdateDLCInstallTestHarness();
        var root = Path.Combine(test.Root, "RomM", "42");
        Configure(test, RomMCategory.Update, "shadps4.update");
        var local = test.RegisterLocalROM();
        var remote = Remote(root,
            DiskFile(root, 100, RomMCategory.Update, "DiskPatch/sce_sys/param.sfo", 100),
            Archive(root, 101, RomMCategory.Update, "archive.zip", 500, "ArchivePatch/sce_sys/param.sfo"));

        await InstallUpdateDLC.RefreshCandidates(test.Mapping, remote, local, test.Plugin.Context);

        Assert.Equal(2, local.UpdateCandidates.Count);
        Assert.Equal(new[] { "DiskPatch", "ArchivePatch" }, local.UpdateCandidates.Select(x => x.Name));
        Assert.Equal(new[] { 100, 101 }, local.UpdateCandidates.SelectMany(x => x.FileIDs));
        Assert.Equal(new long[] { 100, 500 }, local.UpdateCandidates.Select(x => x.Size));
    }

    [Fact]
    public async Task Refresh_CemuArchiveWithWindowsSeparators_MatchesRequiredDirectories()
    {
        using var test = new UpdateDLCInstallTestHarness();
        var root = Path.Combine(test.Root, "RomM", "42");
        Configure(test, RomMCategory.Update, "cemu.update");
        var local = test.RegisterLocalROM();

        await InstallUpdateDLC.RefreshCandidates(test.Mapping, Remote(root,
            Archive(root, 102, RomMCategory.Update, "cemu.zip", 700,
                @"code\app.rpx", @"content\data.bin", @"meta\meta.xml")), local, test.Plugin.Context);

        var candidate = Assert.Single(local.UpdateCandidates);
        Assert.Equal("cemu", candidate.Name);
        Assert.Null(candidate.CandidateRoot);
        Assert.Equal(new[] { 102 }, candidate.FileIDs);
    }

    [Fact]
    public async Task Refresh_ArchiveWithMatchingRoot_DoesNotAlsoProduceNestedCandidates()
    {
        using var test = new UpdateDLCInstallTestHarness();
        var root = Path.Combine(test.Root, "RomM", "42");
        Configure(test, RomMCategory.Update, "shadps4.update");
        var local = test.RegisterLocalROM();

        await InstallUpdateDLC.RefreshCandidates(test.Mapping, Remote(root,
            Archive(root, 103, RomMCategory.Update, "patch.zip", 800,
                "sce_sys/param.sfo", "other/sce_sys/param.sfo")), local, test.Plugin.Context);

        var candidate = Assert.Single(local.UpdateCandidates);
        Assert.Null(candidate.CandidateRoot);
    }

    private static void Configure(UpdateDLCInstallTestHarness test, string category, string definition)
    {
        TitleIDInstallDefinitions.Initialize();
        if (category == RomMCategory.Update)
        {
            test.Mapping.UpdateInstallStyle = InstallStyles.MappedFolder;
            test.Mapping.UpdateTitleIDDefinitionID = definition;
        }
        else
        {
            test.Mapping.DLCInstallStyle = InstallStyles.MappedFolder;
            test.Mapping.DLCTitleIDDefinitionID = definition;
        }
    }

    private static RomMRom Remote(string root, params RomMFile[] files) => new()
    {
        Id = 42,
        Name = "Example Game",
        FullPath = root,
        Files = [.. files]
    };

    private static RomMFile DiskFile(string root, int id, string category, string relativePath, long? size = null) => new()
    {
        Id = id,
        Category = category,
        FileName = Path.GetFileName(relativePath),
        FullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)),
        FileSize = size
    };

    private static RomMFile Archive(string root, int id, string category, string filename, long size, params string[] members) => new()
    {
        Id = id,
        Category = category,
        FileName = filename,
        FullPath = Path.Combine(root, filename),
        FileSize = size,
        ArchiveMembers = members.Select(name => new RomMArchiveMembers { Name = name }).ToList()
    };

    private static IEnumerable<UpdateDLCCandidate> Candidates(Graviton.Models.ROM.RomMRomLocal local, string category) =>
        category == RomMCategory.Update ? local.UpdateCandidates : local.DLCCandidates;

    private static string? Normalized(string? path) => path?.Replace('\\', '/');
}
