using Graviton.Models;
using Graviton.Models.Install;
using Graviton.Models.ROM;
using Graviton.Models.RomM.Rom;
using Graviton.Models.Saves;
using Graviton.Notifications;
using Graviton.Tests.Fakes.Graviton;

using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Models.ROM;

public sealed class RomMRomLocalImportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Graviton.Tests", Guid.NewGuid().ToString("N"));
    private readonly GravitonContextFake _plugin = new();
    private readonly Guid _mappingId = Guid.NewGuid();

    public RomMRomLocalImportTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Games"));
        _plugin.ConfigurePluginDataDirectory(_root);
    }

    [Fact]
    public void Build_SingleFile_PreservesServerMetadataAndLocalIdentity()
    {
        var rom = CreateRom();
        rom.SHA1 = "1234abcdef";
        rom.TitleID = "TITLE0001";
        rom.SaveTarget = "target.sav";

        var local = Build(rom);

        Assert.Equal(101, local.Id);
        Assert.Equal("Playnite-101", local.PlayniteID);
        Assert.Equal(_mappingId, local.MappingID);
        Assert.Equal("New Game", local.Name);
        Assert.Equal("1234abcdef", local.SHA1);
        Assert.Equal("TITLE0001", local.TitleID);
        Assert.Equal("target.sav", local.SaveTarget);
        Assert.False(local.HasMultipleFiles);
        Assert.Equal("game.z64", local.FileName);
        Assert.Equal("/api/roms/101/content/game.z64", local.DownloadURL);
        Assert.Equal(EmulatorMapping.InstallPathToken + "/game.z64", local.InstallPath);
    }

    [Theory]
    [InlineData("library/n64", "library/n64/Series/game.z64", "/Series/game.z64")]
    [InlineData("library/n64", "library/n64/Series/Disc 1/game.z64", "/Series/Disc 1/game.z64")]
    [InlineData("library/n64", "library/n64/game.z64", "/game.z64")]
    public void Build_UsesTheRomRelativeInstallPath(string fileSystemPath, string fullPath, string relativePath)
    {
        var rom = CreateRom();
        rom.FileSystemPath = fileSystemPath;
        rom.FullPath = fullPath;

        var local = Build(rom);

        Assert.Equal(EmulatorMapping.InstallPathToken + relativePath, local.InstallPath);
    }

    [Fact]
    public void Build_SingleFile_IgnoresUpdateAndDLCFilesWhenChoosingGame()
    {
        var rom = CreateRom();
        rom.Files =
        [
            CreateRomFile(10, "update.pkg", RomMCategory.Update, "library/n64/update.pkg"),
            CreateRomFile(11, "dlc.pkg", RomMCategory.DLC, "library/n64/dlc.pkg"),
            CreateRomFile(12, "game.z64", RomMCategory.Game, "library/n64/game.z64")
        ];

        var local = Build(rom);

        Assert.Equal("game.z64", local.FileName);
        Assert.Equal("/api/roms/101/content/game.z64", local.DownloadURL);
    }

    [Fact]
    public void Build_SingleFile_WithMultipleGameFiles_PrefersShallowerFile()
    {
        var rom = CreateRom();
        rom.Files =
        [
            CreateRomFile(10, "nested.z64", RomMCategory.Game, "library/n64/Collection/Subfolder/nested.z64"),
            CreateRomFile(11, "root.z64", RomMCategory.Game, "library/n64/root.z64"),
            CreateRomFile(12, "patch.pkg", RomMCategory.Update, "library/n64/patch.pkg")
        ];

        var local = Build(rom);

        Assert.Equal("root.z64", local.FileName);
        Assert.Equal("/api/roms/101/content/root.z64", local.DownloadURL);
    }

    [Theory]
    [InlineData("Game #1 (US).z64", "/api/roms/101/content/Game%20%231%20%28US%29.z64")]
    [InlineData("A&B + C.z64", "/api/roms/101/content/A%26B%20%2B%20C.z64")]
    public void Build_SingleFile_EncodesRomMContentFilename(string filename, string url)
    {
        var rom = CreateRom();
        rom.Files = [CreateRomFile(10, filename, RomMCategory.Game, "library/n64/" + filename)];

        var local = Build(rom);

        Assert.Equal(filename, local.FileName);
        Assert.Equal(url, local.DownloadURL);
    }

    [Fact]
    public void Build_MultipleFiles_UsesOnlyGameCategoryFileIDsInOriginalOrder()
    {
        var rom = CreateRom();
        rom.HasMultipleFiles = true;
        rom.FileName = "Disc Set.zip";
        rom.Files =
        [
            CreateRomFile(90, "patch.bin", RomMCategory.Update, "library/n64/patch.bin"),
            CreateRomFile(15, "disc2.bin", RomMCategory.Game, "library/n64/disc2.bin"),
            CreateRomFile(33, "bonus.pkg", RomMCategory.DLC, "library/n64/bonus.pkg"),
            CreateRomFile(8, "disc1.cue", RomMCategory.Game, "library/n64/disc1.cue")
        ];

        var local = Build(rom);

        Assert.True(local.HasMultipleFiles);
        Assert.Equal("Disc Set.zip", local.FileName);
        Assert.Equal("/api/roms/101/content/Disc%20Set.zip?file_ids=15,8", local.DownloadURL);
    }

    [Fact]
    public void Build_MultipleFiles_DoesNotUseTheIndividualGameFileName()
    {
        var rom = CreateRom();
        rom.HasMultipleFiles = true;
        rom.FileName = "Multi-disc #1.zip";
        rom.Files = [CreateRomFile(20, "disc1.cue", RomMCategory.Game, "library/n64/disc1.cue")];

        var local = Build(rom);

        Assert.Equal("Multi-disc #1.zip", local.FileName);
        Assert.Equal("/api/roms/101/content/Multi-disc%20%231.zip?file_ids=20", local.DownloadURL);
    }

    [Fact]
    public void Build_WhenNameIsMissing_UsesUnknownGameWithoutDroppingOtherFields()
    {
        var rom = CreateRom();
        rom.Name = null;

        var local = Build(rom);

        Assert.Equal("Unknown Game", local.Name);
        Assert.Equal("game.z64", local.FileName);
        Assert.Equal("Playnite-101", local.PlayniteID);
    }

    [Fact]
    public void Build_PersistsMetadataAndRegistersOnlyTheCurrentRom()
    {
        var existing = new RomMRomLocal { Id = 202, Name = "Existing ROM" };
        _plugin.ImportedGames["202"] = existing;
        var rom = CreateRom();

        var local = Build(rom);
        var saved = ReadSavedRom();

        Assert.Same(existing, _plugin.ImportedGames["202"]);
        Assert.Same(local, _plugin.ImportedGames["101"]);
        Assert.Equal(local.PlayniteID, saved.PlayniteID);
        Assert.Equal(local.MappingID, saved.MappingID);
        Assert.Equal(local.DownloadURL, saved.DownloadURL);
        Assert.False(File.Exists(Path.Combine(_root, "Games", "202.json")));
    }

    [Fact]
    public void Resync_UpdatesRemoteMetadataWithoutLosingLocalInstallOrSaveState()
    {
        var local = CreateTrackedLocal();
        local.LocalSave = new GravitonSave
        {
            Filename = "autosave.sav",
            ContentHash = "local-hash",
            SourceFilePaths = new ObservableCollection<string>(["Saves/autosave.sav"])
        };
        var save = local.LocalSave;
        var update = new UpdateDLCCandidate("Patch", "patch.pkg", RomMCategory.Update, [52]);
        local.UpdateCandidates.Add(update);
        var rom = CreateRom();
        rom.Name = "Renamed on RomM";
        rom.SHA1 = "new-hash";
        rom.TitleID = "new-title";
        rom.SaveTarget = "new-save-target";

        Resync(local, rom);

        Assert.Equal("Renamed on RomM", local.Name);
        Assert.Equal("new-hash", local.SHA1);
        Assert.Equal("new-title", local.TitleID);
        Assert.Equal("new-save-target", local.SaveTarget);
        Assert.Equal("C:/Installed/old.z64", local.InstalledPath);
        Assert.False(local.IsInstalledPathDirectory);
        Assert.Equal(_mappingId, local.MappingID);
        Assert.Equal("Playnite-original", local.PlayniteID);
        Assert.Same(save, local.LocalSave);
        Assert.Same(update, Assert.Single(local.UpdateCandidates));
        Assert.Equal("local-hash", local.LocalSave!.ContentHash);
    }

    [Fact]
    public void Resync_SingleFile_RefreshesFilenameAndDownloadLink()
    {
        var local = CreateTrackedLocal();
        var rom = CreateRom();
        rom.Files = [CreateRomFile(22, "new revision.z64", RomMCategory.Game, "library/n64/new revision.z64")];

        Resync(local, rom);

        Assert.Equal("new revision.z64", local.FileName);
        Assert.Equal("/api/roms/101/content/new%20revision.z64", local.DownloadURL);
        Assert.Equal("C:/Installed/old.z64", local.InstalledPath);
        Assert.Equal(local.DownloadURL, ReadSavedRom().DownloadURL);
    }

    [Fact]
    public void Resync_MultipleFiles_RefreshesGameOnlyDownloadIDs()
    {
        var local = CreateTrackedLocal();
        var rom = CreateRom();
        rom.HasMultipleFiles = true;
        rom.FileName = "Remastered Set.zip";
        rom.Files =
        [
            CreateRomFile(51, "disc1.cue", RomMCategory.Game, "library/n64/disc1.cue"),
            CreateRomFile(52, "update.pkg", RomMCategory.Update, "library/n64/update.pkg"),
            CreateRomFile(53, "disc2.bin", RomMCategory.Game, "library/n64/disc2.bin")
        ];

        Resync(local, rom);

        Assert.True(local.HasMultipleFiles);
        Assert.Equal("Remastered Set.zip", local.FileName);
        Assert.Equal("/api/roms/101/content/Remastered%20Set.zip?file_ids=51,53", local.DownloadURL);
        Assert.Equal(local.DownloadURL, ReadSavedRom().DownloadURL);
    }

    [Fact]
    public void Resync_WhenRemoteNameIsNull_UsesUnknownGame()
    {
        var local = CreateTrackedLocal();
        var rom = CreateRom();
        rom.Name = null;

        Resync(local, rom);

        Assert.Equal("Unknown Game", local.Name);
        Assert.Equal("Unknown Game", ReadSavedRom().Name);
    }

    [Fact]
    public void Resync_WhenSingleFileHasNoRomFiles_PreservesExistingDownloadInformation()
    {
        var local = CreateTrackedLocal();
        var rom = CreateRom();
        rom.Files.Clear();

        Resync(local, rom);

        Assert.Equal("old.z64", local.FileName);
        Assert.Equal("/api/roms/101/content/old.z64", local.DownloadURL);
        Assert.False(File.Exists(Path.Combine(_root, "Games", "101.json")));
    }

    [Fact]
    public void Resync_WhenMultipleFilesContainNoGameCategory_PreservesExistingDownloadInformation()
    {
        var local = CreateTrackedLocal();
        var rom = CreateRom();
        rom.HasMultipleFiles = true;
        rom.Files =
        [
            CreateRomFile(23, "patch.bin", RomMCategory.Update, "library/n64/patch.bin"),
            CreateRomFile(24, "dlc.bin", RomMCategory.DLC, "library/n64/dlc.bin")
        ];

        Resync(local, rom);

        Assert.Equal("old.z64", local.FileName);
        Assert.Equal("/api/roms/101/content/old.z64", local.DownloadURL);
        Assert.False(File.Exists(Path.Combine(_root, "Games", "101.json")));
    }

    [Fact]
    public void Save_NormalizesBackslashesInTrackedSavePathsAndPersistsThem()
    {
        var local = CreateTrackedLocal();
        local.LocalSave = new GravitonSave
        {
            SourceFilePaths = new ObservableCollection<string>(["Saves\\slot1.sav", "Saves/slot2.sav"])
        };

        local.Save(_plugin.Context);

        Assert.Equal(new[] { "Saves/slot1.sav", "Saves/slot2.sav" }, local.LocalSave.SourceFilePaths.ToArray());
        Assert.Equal(new[] { "Saves/slot1.sav", "Saves/slot2.sav" }, ReadSavedRom().LocalSave!.SourceFilePaths.ToArray());
    }

    [Fact]
    public void GameInstallInfoBuild_CopiesLocalInstallAndSaveConfiguration()
    {
        var local = CreateTrackedLocal();
        local.PatchFileId = 87;
        local.HasMultipleFiles = true;
        local.DownloadURL = "/content/game.zip?file_ids=11,12";
        local.SaveTarget = "memory.sav";
        local.TitleID = "TITLE123";
        var mapping = new EmulatorMapping { DestinationPath = "C:/Games" };

        var info = GameInstallInfo.Build(local, mapping);

        Assert.Equal(101, info.Id);
        Assert.Equal("Previously installed", info.GameName);
        Assert.Equal("old.z64", info.FileName);
        Assert.True(info.HasMultipleFiles);
        Assert.Equal("/content/game.zip?file_ids=11,12", info.DownloadURL);
        Assert.Equal("some/install/path", info.InstallPath);
        Assert.Equal(87, info.PatchFileID);
        Assert.Equal("memory.sav", info.SaveTarget);
        Assert.Equal("TITLE123", info.TitleID);
        Assert.Same(mapping, info.Mapping);
    }

    [Fact]
    public void GameInstallInfoBuild_UsesEmptyStringsForMissingLocalStrings()
    {
        var local = new RomMRomLocal
        {
            Id = 101,
            Name = null!,
            FileName = null,
            InstallPath = null!,
            DownloadURL = null!
        };
        var mapping = new EmulatorMapping();

        var info = GameInstallInfo.Build(local, mapping);

        Assert.Equal(string.Empty, info.GameName);
        Assert.Equal(string.Empty, info.FileName);
        Assert.Equal(string.Empty, info.InstallPath);
        Assert.Equal(string.Empty, info.DownloadURL);
    }

    private RomMRomLocal Build(RomMRom rom)
    {
        return Assert.IsType<RomMRomLocal>(RomMRomLocal.Build(_mappingId, rom, "Playnite-101", _plugin.Context));
    }

    private void Resync(RomMRomLocal local, RomMRom rom)
    {
        local.Resync(new GravitonLogger(_plugin.Settings), rom, _plugin.Context);
    }

    private RomMRomLocal ReadSavedRom()
    {
        return Assert.IsType<RomMRomLocal>(JsonSerializer.Deserialize<RomMRomLocal>(
            File.ReadAllText(Path.Combine(_root, "Games", "101.json"))));
    }

    private RomMRomLocal CreateTrackedLocal()
    {
        var local = new RomMRomLocal
        {
            Id = 101,
            Name = "Previously installed",
            FileName = "old.z64",
            PlayniteID = "Playnite-original",
            SHA1 = "old-hash",
            DownloadURL = "/api/roms/101/content/old.z64",
            InstallPath = "some/install/path",
            InstalledPath = "C:/Installed/old.z64",
            MappingID = _mappingId
        };
        _plugin.ImportedGames["101"] = local;
        return local;
    }

    private static RomMRom CreateRom()
    {
        return new RomMRom
        {
            Id = 101,
            Name = "New Game",
            FileName = "game.z64",
            FileSystemPath = "library/n64",
            FullPath = "library/n64/game.z64",
            Files = [CreateRomFile(10, "game.z64", RomMCategory.Game, "library/n64/game.z64")]
        };
    }

    private static RomMFile CreateRomFile(int id, string name, string category, string fullPath)
    {
        return new RomMFile { Id = id, FileName = name, Category = category, FullPath = fullPath };
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
