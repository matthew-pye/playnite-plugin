using Graviton.Tests.Fakes.Play;

using Playnite;

using System.IO;

using Xunit;

namespace Graviton.Tests.Play;

[CollectionDefinition("GravitonPlayLifecycle", DisableParallelization = true)]
public sealed class GravitonPlayLifecycleCollection
{
}

[Collection("GravitonPlayLifecycle")]
public sealed class GravitonPlayControllerActionTests
{
    [Fact]
    public async Task GetPlayActions_ForOtherLibrary_ReturnsNoActions()
    {
        using var test = new PlayTestHarness();
        var game = test.AddGame(libraryID: "some-other-library");
        var mapping = test.AddMapping();
        test.ConfigureCustomEmulator(mapping, "nes");
        test.AddROM(mapping, installedPath: test.WriteGameFile("Mario.nes"));

        var actions = await test.Controller.GetPlayActionsAsync(test.GetPlayActions(game));

        Assert.Empty(actions);
    }

    [Fact]
    public async Task GetPlayActions_WhenGameIsNotImported_ReturnsNoActions()
    {
        using var test = new PlayTestHarness();
        var game = test.AddGame();
        var mapping = test.AddMapping();
        test.ConfigureCustomEmulator(mapping, "nes");

        var actions = await test.Controller.GetPlayActionsAsync(test.GetPlayActions(game));

        Assert.Empty(actions);
    }

    [Fact]
    public async Task GetPlayActions_WhenMappingIsMissing_ReturnsNoActions()
    {
        using var test = new PlayTestHarness();
        var game = test.AddGame();
        test.AddROM(test.AddMapping());
        test.Plugin.Settings.Mappings.Clear();

        var actions = await test.Controller.GetPlayActionsAsync(test.GetPlayActions(game));

        Assert.Empty(actions);
    }

    [Fact]
    public async Task GetPlayActions_WhenEmulatorIsNotConfigured_ReturnsNoActions()
    {
        using var test = new PlayTestHarness();
        var game = test.AddGame();
        var mapping = test.AddMapping();
        test.AddROM(mapping, installedPath: test.WriteGameFile("Mario.nes"));

        var actions = await test.Controller.GetPlayActionsAsync(test.GetPlayActions(game));

        Assert.Empty(actions);
    }

    [Fact]
    public async Task GetPlayActions_WithCustomEmulator_UsesSelectedExecutableAndROM()
    {
        using var test = new PlayTestHarness();
        var game = test.AddGame();
        var mapping = test.AddMapping();
        var emulator = test.ConfigureCustomEmulator(mapping, "nes");
        var romPath = test.WriteGameFile("Mario.nes");
        test.AddROM(mapping, installedPath: romPath);

        var actions = await test.Controller.GetPlayActionsAsync(test.GetPlayActions(game));
        var action = Assert.IsType<AutomaticFilePlayController>(Assert.Single(actions));

        Assert.Equal(emulator.StartupPath, action.Action.Path);
        Assert.Equal("--load Mario.nes", action.Action.Arguments);
        Assert.Equal(romPath, action.StartProperties["ImagePath"]);
        Assert.Equal(Path.GetDirectoryName(emulator.StartupPath), action.StartProperties["EmulatorDir"]);
        Assert.Same(test.Playnite.Api, action.StartProperties["PlayniteApi"]);
        Assert.Same(emulator, action.StartProperties["Emulator"]);
    }

    [Theory]
    [InlineData("Mario.nes", "nes")]
    [InlineData("Mario.NES", "nes")]
    [InlineData("Mario.nes", ".nes")]
    [InlineData("Mario.nes", "NES")]
    public async Task GetPlayActions_RecognisesSupportedSingleFileExtensions(string fileName, string extension)
    {
        using var test = new PlayTestHarness();
        var game = test.AddGame();
        var mapping = test.AddMapping();
        test.ConfigureCustomEmulator(mapping, extension);
        var romPath = test.WriteGameFile(fileName);
        test.AddROM(mapping, installedPath: romPath);

        var actions = await test.Controller.GetPlayActionsAsync(test.GetPlayActions(game));

        Assert.Equal(romPath, Assert.IsType<AutomaticFilePlayController>(Assert.Single(actions)).StartProperties["ImagePath"]);
    }

    [Fact]
    public async Task GetPlayActions_WhenSingleFileExtensionIsUnsupported_ReturnsNoActions()
    {
        using var test = new PlayTestHarness();
        var game = test.AddGame();
        var mapping = test.AddMapping();
        test.ConfigureCustomEmulator(mapping, "gba");
        test.AddROM(mapping, installedPath: test.WriteGameFile("Mario.nes"));

        var actions = await test.Controller.GetPlayActionsAsync(test.GetPlayActions(game));

        Assert.Empty(actions);
    }

    [Fact]
    public async Task GetPlayActions_WhenGameFileIsMissing_MarksItUninstalled()
    {
        using var test = new PlayTestHarness();
        var game = test.AddGame();
        var mapping = test.AddMapping();
        test.ConfigureCustomEmulator(mapping, "nes");
        test.AddROM(mapping, installedPath: Path.Combine(test.Root, "missing.nes"));
        test.Playnite.Games.Seed(game);

        var actions = await test.Controller.GetPlayActionsAsync(test.GetPlayActions(game));

        Assert.Empty(actions);
        Assert.Equal(InstallState.Uninstalled, test.Playnite.Games.Api.Get(game.Id)!.InstallState);
    }

    [Fact]
    public async Task GetPlayActions_WhenCustomEmulatorHasNoStartupPath_ReturnsNoActions()
    {
        using var test = new PlayTestHarness();
        var game = test.AddGame();
        var mapping = test.AddMapping();
        var emulator = test.ConfigureCustomEmulator(mapping, "nes");
        emulator.StartupPath = string.Empty;
        test.AddROM(mapping, installedPath: test.WriteGameFile("Mario.nes"));

        var actions = await test.Controller.GetPlayActionsAsync(test.GetPlayActions(game));

        Assert.Empty(actions);
    }

    [Fact]
    public async Task GetPlayActions_WhenCustomEmulatorHasNoArguments_ReturnsNoActions()
    {
        using var test = new PlayTestHarness();
        var game = test.AddGame();
        var mapping = test.AddMapping();
        var emulator = test.ConfigureCustomEmulator(mapping, "nes");
        emulator.Arguments = string.Empty;
        test.AddROM(mapping, installedPath: test.WriteGameFile("Mario.nes"));

        var actions = await test.Controller.GetPlayActionsAsync(test.GetPlayActions(game));

        Assert.Empty(actions);
    }

    [Fact]
    public async Task GetPlayActions_InDirectory_CreatesActionForEachSupportedFile()
    {
        using var test = new PlayTestHarness();
        var game = test.AddGame();
        var mapping = test.AddMapping();
        test.ConfigureCustomEmulator(mapping, "nes");
        var first = test.WriteGameFile("Install/Disc1.nes");
        var second = test.WriteGameFile("Install/Nested/Disc2.nes");
        test.WriteGameFile("Install/Readme.txt");
        test.AddROM(mapping, installedPath: Path.Combine(test.Root, "Install"), isDirectory: true);

        var actions = await test.Controller.GetPlayActionsAsync(test.GetPlayActions(game));
        var paths = actions.Select(action =>
        {
            return (string)Assert.IsType<AutomaticFilePlayController>(action).StartProperties["ImagePath"];
        }).ToList();

        Assert.Equal(2, paths.Count);
        Assert.Contains(first, paths);
        Assert.Contains(second, paths);
        Assert.All(actions, action =>
        {
            Assert.Contains(".nes", action.Name, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task GetPlayActions_InDirectoryWithNoMatchingFiles_ReturnsNoActions()
    {
        using var test = new PlayTestHarness();
        var game = test.AddGame();
        var mapping = test.AddMapping();
        test.ConfigureCustomEmulator(mapping, "nes");
        test.WriteGameFile("Install/Readme.txt");
        test.AddROM(mapping, installedPath: Path.Combine(test.Root, "Install"), isDirectory: true);

        var actions = await test.Controller.GetPlayActionsAsync(test.GetPlayActions(game));

        Assert.Empty(actions);
    }

    [Fact]
    public async Task GetPlayActions_ForExtensionlessFile_AcceptsNoneFileType()
    {
        using var test = new PlayTestHarness();
        var game = test.AddGame();
        var mapping = test.AddMapping();
        test.ConfigureCustomEmulator(mapping, "<none>");
        var romPath = test.WriteGameFile("Install/GameFile");
        test.AddROM(mapping, installedPath: romPath);

        var actions = await test.Controller.GetPlayActionsAsync(test.GetPlayActions(game));

        Assert.Equal(romPath, Assert.IsType<AutomaticFilePlayController>(Assert.Single(actions)).StartProperties["ImagePath"]);
    }

    [Fact]
    public async Task GetPlayActions_ForPS3Directory_SelectsPS3GameEbootOnly()
    {
        using var test = new PlayTestHarness();
        var game = test.AddGame();
        var mapping = test.AddMapping("ps3");
        test.ConfigureCustomEmulator(mapping, "bin");
        var wanted = test.WriteGameFile("Install/PS3_GAME/USRDIR/EBOOT.BIN");
        test.WriteGameFile("Install/Other/EBOOT.BIN");
        test.AddROM(mapping, installedPath: Path.Combine(test.Root, "Install"), isDirectory: true);

        var actions = await test.Controller.GetPlayActionsAsync(test.GetPlayActions(game));

        Assert.Equal(wanted, Assert.IsType<AutomaticFilePlayController>(Assert.Single(actions)).StartProperties["ImagePath"]);
    }

    [Fact]
    public async Task GetPlayActions_ForPS3CaseInsensitiveSlug_UsesEboot()
    {
        using var test = new PlayTestHarness();
        var game = test.AddGame();
        var mapping = test.AddMapping("PS3");
        test.ConfigureCustomEmulator(mapping, "bin");
        var wanted = test.WriteGameFile("Install/PS3_GAME/USRDIR/EBOOT.BIN");
        test.AddROM(mapping, installedPath: Path.Combine(test.Root, "Install"), isDirectory: true);

        var actions = await test.Controller.GetPlayActionsAsync(test.GetPlayActions(game));

        Assert.Equal(wanted, Assert.IsType<AutomaticFilePlayController>(Assert.Single(actions)).StartProperties["ImagePath"]);
    }
}
