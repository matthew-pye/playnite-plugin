using Graviton.Install;
using Graviton.Models.Install;
using Graviton.Models.RomM.Rom;
using Graviton.Tests.Fakes.Install;

using System.IO;
using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Install;

[Collection("GravitonUpdateDLC")]
public sealed class InstallUpdateDLCCandidateDiscoveryTests
{
    [Theory]
    [InlineData("update", "patch.pkg", 101)]
    [InlineData("dlc", "expansion.pkg", 102)]
    public async Task Refresh_FolderStyleWithSingleFile_CreatesCandidateForCorrectCategory(string category, string filename, int id)
    {
        using var test = new UpdateDLCInstallTestHarness();
        var local = test.RegisterLocalROM();
        var remote = Remote(File(id, category, filename, 4096));

        await InstallUpdateDLC.RefreshCandidates(test.Mapping, remote, local, test.Plugin.Context);

        var candidate = Assert.Single(CategoryCandidates(local, category));
        Assert.Equal(filename, candidate.Name);
        Assert.Equal(filename, candidate.FileName);
        Assert.Equal(category, candidate.Category);
        Assert.Equal(new[] { id }, candidate.FileIDs);
        Assert.Equal(4096, candidate.Size);
        Assert.Null(candidate.CandidateRoot);
        Assert.Null(candidate.SingleFileRelativePath);
    }

    [Theory]
    [InlineData("update", "Update Folder", "Example Game-Update.zip")]
    [InlineData("dlc", "DLC Folder", "Example Game-DLC.zip")]
    public async Task Refresh_FolderStyleWithMultipleFiles_GroupsFilesIntoSingleArchive(string category, string name, string archive)
    {
        using var test = new UpdateDLCInstallTestHarness();
        var local = test.RegisterLocalROM();
        var remote = Remote(File(10, category, "first.pkg", 100), File(11, category, "second.pkg", 300));

        await InstallUpdateDLC.RefreshCandidates(test.Mapping, remote, local, test.Plugin.Context);

        var candidate = Assert.Single(CategoryCandidates(local, category));
        Assert.Equal(name, candidate.Name);
        Assert.Equal(archive, candidate.FileName);
        Assert.Equal(new[] { 10, 11 }, candidate.FileIDs);
        Assert.Equal(400, candidate.Size);
    }

    [Fact]
    public async Task Refresh_MixedCategories_SeparatesUpdateAndDLCFiles()
    {
        using var test = new UpdateDLCInstallTestHarness();
        var local = test.RegisterLocalROM();
        var remote = Remote(
            File(1, RomMCategory.Game, "game.rom", 900),
            File(2, RomMCategory.Update, "patch.pkg", 100),
            File(3, RomMCategory.DLC, "expansion.pkg", 200),
            File(4, RomMCategory.Patch, "translation.ips", 300));

        await InstallUpdateDLC.RefreshCandidates(test.Mapping, remote, local, test.Plugin.Context);

        Assert.Equal(new[] { 2 }, Assert.Single(local.UpdateCandidates).FileIDs);
        Assert.Equal(new[] { 3 }, Assert.Single(local.DLCCandidates).FileIDs);
    }

    [Fact]
    public async Task Refresh_FolderStyleWithUnknownSizes_UsesZeroForMissingValues()
    {
        using var test = new UpdateDLCInstallTestHarness();
        var local = test.RegisterLocalROM();
        var remote = Remote(File(1, RomMCategory.Update, "one.pkg"), File(2, RomMCategory.Update, "two.pkg", 50));

        await InstallUpdateDLC.RefreshCandidates(test.Mapping, remote, local, test.Plugin.Context);

        Assert.Equal(50, Assert.Single(local.UpdateCandidates).Size);
    }

    [Fact]
    public async Task Refresh_WithoutRemoteUpdateOrDLCFiles_RemovesStaleCandidates()
    {
        using var test = new UpdateDLCInstallTestHarness();
        var local = test.RegisterLocalROM();
        local.UpdateCandidates.Add(UpdateDLCInstallTestHarness.Candidate(ids: [10]));
        local.DLCCandidates.Add(UpdateDLCInstallTestHarness.Candidate(category: RomMCategory.DLC, ids: [20]));

        await InstallUpdateDLC.RefreshCandidates(test.Mapping, Remote(File(1, RomMCategory.Game, "game.rom")), local, test.Plugin.Context);

        Assert.Empty(local.UpdateCandidates);
        Assert.Empty(local.DLCCandidates);
    }

    [Fact]
    public async Task Refresh_InstalledFolderCandidate_PreservesTrackedPathsAndInstalledState()
    {
        using var test = new UpdateDLCInstallTestHarness();
        var local = test.RegisterLocalROM();
        var old = UpdateDLCInstallTestHarness.Candidate(name: "patch.pkg", fileName: "patch.pkg", ids: [10]);
        old.InstalledFileIDs = [10];
        old.InstalledTopPaths = [Path.Combine(test.Root, "installed", "patch.pkg")];
        old.InstalledSize = 128;
        old.PreviousInstallStyle = InstallStyles.Folder;
        local.UpdateCandidates.Add(old);

        await InstallUpdateDLC.RefreshCandidates(test.Mapping, Remote(File(10, RomMCategory.Update, "patch.pkg", 256)), local, test.Plugin.Context);

        var candidate = Assert.Single(local.UpdateCandidates);
        Assert.NotSame(old, candidate);
        Assert.Equal(InstallStatus.Installed, candidate.Status);
        Assert.Equal(old.InstalledFileIDs, candidate.InstalledFileIDs);
        Assert.Equal(old.InstalledTopPaths, candidate.InstalledTopPaths);
        Assert.Equal(128, candidate.InstalledSize);
        Assert.Equal(InstallStyles.Folder, candidate.PreviousInstallStyle);
        Assert.Equal(256, candidate.Size);
    }

    [Fact]
    public async Task Refresh_ExpandedFolderCandidate_MarksPartialInstallAndRetainsInstalledFiles()
    {
        using var test = new UpdateDLCInstallTestHarness();
        var local = test.RegisterLocalROM();
        var old = UpdateDLCInstallTestHarness.Candidate(name: "Update Folder", ids: [10]);
        old.InstalledFileIDs = [10];
        old.InstalledSize = 100;
        local.UpdateCandidates.Add(old);

        await InstallUpdateDLC.RefreshCandidates(test.Mapping, Remote(
            File(10, RomMCategory.Update, "first.pkg", 100),
            File(11, RomMCategory.Update, "second.pkg", 200)), local, test.Plugin.Context);

        var candidate = Assert.Single(local.UpdateCandidates);
        Assert.Equal(InstallStatus.PartialInstalled, candidate.Status);
        Assert.Equal(new[] { 10 }, candidate.InstalledFileIDs);
        Assert.Equal(new[] { 10, 11 }, candidate.FileIDs);
        Assert.Equal(100, candidate.InstalledSize);
    }

    [Fact]
    public async Task Refresh_WithoutInstallStyle_DoesNotDiscoverAvailableFiles()
    {
        using var test = new UpdateDLCInstallTestHarness();
        test.Mapping.UpdateInstallStyle = InstallStyles.None;
        test.Mapping.DLCInstallStyle = InstallStyles.None;
        var local = test.RegisterLocalROM();

        await InstallUpdateDLC.RefreshCandidates(test.Mapping, Remote(
            File(10, RomMCategory.Update, "patch.pkg"),
            File(11, RomMCategory.DLC, "dlc.pkg")), local, test.Plugin.Context);

        Assert.Empty(local.UpdateCandidates);
        Assert.Empty(local.DLCCandidates);
    }

    [Theory]
    [InlineData("rpcs3.install-pkg", "patch.PKG")]
    [InlineData("vita3k.install-arc", "patch.VPK")]
    [InlineData("vita3k.install-arc", "patch.ZIP")]
    [InlineData("azahar.install-cia", "patch.CIA")]
    public async Task Refresh_CLIStyle_RecognizesSupportedExtensionsIgnoringCase(string definition, string filename)
    {
        using var test = new UpdateDLCInstallTestHarness();
        CLIInstallDefinitions.Initialize();
        test.Mapping.UpdateInstallStyle = InstallStyles.CLI;
        test.Mapping.UpdateCLIDefinitionID = definition;
        var local = test.RegisterLocalROM();

        await InstallUpdateDLC.RefreshCandidates(test.Mapping, Remote(File(12, RomMCategory.Update, filename, 400)), local, test.Plugin.Context);

        var candidate = Assert.Single(local.UpdateCandidates);
        Assert.Equal(Path.GetFileNameWithoutExtension(filename), candidate.Name);
        Assert.Equal(filename, candidate.FileName);
        Assert.Equal(new[] { 12 }, candidate.FileIDs);
        Assert.Equal(400, candidate.Size);
    }

    [Fact]
    public async Task Refresh_CLIStyle_ExcludesUnsupportedExtensionsAndOtherCategories()
    {
        using var test = new UpdateDLCInstallTestHarness();
        CLIInstallDefinitions.Initialize();
        test.Mapping.UpdateInstallStyle = InstallStyles.CLI;
        test.Mapping.UpdateCLIDefinitionID = "rpcs3.install-pkg";
        var local = test.RegisterLocalROM();

        await InstallUpdateDLC.RefreshCandidates(test.Mapping, Remote(
            File(1, RomMCategory.Update, "patch.pkg", 100),
            File(2, RomMCategory.Update, "patch.zip", 200),
            File(3, RomMCategory.Update, "patch.pkg.bak", 300),
            File(4, RomMCategory.DLC, "expansion.pkg", 400)), local, test.Plugin.Context);

        Assert.Equal(new[] { 1 }, Assert.Single(local.UpdateCandidates).FileIDs);
        Assert.Equal(new[] { 4 }, Assert.Single(local.DLCCandidates).FileIDs);
    }

    [Fact]
    public async Task Refresh_CLIStyle_CreatesOneCandidatePerSupportedFile()
    {
        using var test = new UpdateDLCInstallTestHarness();
        CLIInstallDefinitions.Initialize();
        test.Mapping.DLCInstallStyle = InstallStyles.CLI;
        test.Mapping.DLCCLIDefinitionID = "vita3k.install-arc";
        var local = test.RegisterLocalROM();

        await InstallUpdateDLC.RefreshCandidates(test.Mapping, Remote(
            File(20, RomMCategory.DLC, "costume.vpk", 200),
            File(21, RomMCategory.DLC, "extra.zip", 300),
            File(22, RomMCategory.DLC, "unsupported.cia", 400)), local, test.Plugin.Context);

        Assert.Equal(2, local.DLCCandidates.Count);
        Assert.Equal(new[] { 20, 21 }, local.DLCCandidates.SelectMany(x => x.FileIDs));
        Assert.Equal(new[] { "costume", "extra" }, local.DLCCandidates.Select(x => x.Name));
        Assert.Equal(new long[] { 200, 300 }, local.DLCCandidates.Select(x => x.Size));
    }

    [Fact]
    public async Task Refresh_CLIStyle_WithoutMatchingExtensions_LeavesCategoryEmpty()
    {
        using var test = new UpdateDLCInstallTestHarness();
        CLIInstallDefinitions.Initialize();
        test.Mapping.UpdateInstallStyle = InstallStyles.CLI;
        test.Mapping.UpdateCLIDefinitionID = "rpcs3.install-pkg";
        var local = test.RegisterLocalROM();

        await InstallUpdateDLC.RefreshCandidates(test.Mapping, Remote(File(10, RomMCategory.Update, "readme.txt")), local, test.Plugin.Context);

        Assert.Empty(local.UpdateCandidates);
    }

    [Fact]
    public async Task Refresh_CLIStyle_UsesRemoteFullPathForExtensionFiltering()
    {
        using var test = new UpdateDLCInstallTestHarness();
        CLIInstallDefinitions.Initialize();
        test.Mapping.UpdateInstallStyle = InstallStyles.CLI;
        test.Mapping.UpdateCLIDefinitionID = "rpcs3.install-pkg";
        var local = test.RegisterLocalROM();
        var file = File(10, RomMCategory.Update, "patch.pkg");
        file.FullPath = "/roms/42/patch.pkg.backup";

        await InstallUpdateDLC.RefreshCandidates(test.Mapping, Remote(file), local, test.Plugin.Context);

        Assert.Empty(local.UpdateCandidates);
    }

    [Fact]
    public async Task Refresh_CLIStyle_PersistsDiscoveredCandidatesWithTheLocalROM()
    {
        using var test = new UpdateDLCInstallTestHarness();
        CLIInstallDefinitions.Initialize();
        test.Mapping.UpdateInstallStyle = InstallStyles.CLI;
        test.Mapping.UpdateCLIDefinitionID = "rpcs3.install-pkg";
        var local = test.RegisterLocalROM();

        await InstallUpdateDLC.RefreshCandidates(test.Mapping, Remote(File(77, RomMCategory.Update, "patch.pkg", 65536)), local, test.Plugin.Context);

        var savedPath = Path.Combine(test.Root, "Games", "42.json");
        Assert.True(System.IO.File.Exists(savedPath));
        using var json = JsonDocument.Parse(System.IO.File.ReadAllText(savedPath));
        var candidate = json.RootElement.GetProperty("UpdateCandidates")[0];
        Assert.Equal(77, candidate.GetProperty("FileIDs")[0].GetInt32());
        Assert.Equal(65536, candidate.GetProperty("Size").GetInt64());
        Assert.Same(local, test.Plugin.ImportedGames["42"]);
    }

    private static RomMRom Remote(params RomMFile[] files) => new()
    {
        Id = 42,
        Name = "Example Game",
        FullPath = "/roms/42",
        Files = [.. files]
    };

    private static RomMFile File(int id, string category, string filename, long? size = null) => new()
    {
        Id = id,
        Category = category,
        FileName = filename,
        FullPath = $"/roms/42/{filename}",
        FileSize = size
    };

    private static IEnumerable<UpdateDLCCandidate> CategoryCandidates(Graviton.Models.ROM.RomMRomLocal local, string category) =>
        category == RomMCategory.Update ? local.UpdateCandidates : local.DLCCandidates;
}
