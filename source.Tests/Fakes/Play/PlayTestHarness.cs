using Emunight;

using Graviton.Models;
using Graviton.Models.ROM;
using Graviton.Models.RomM.Platform;
using Graviton.Notifications;
using Graviton.Play;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;

using NSubstitute;

using Playnite;

using System.IO;

using static Playnite.Plugin;

namespace Graviton.Tests.Fakes.Play;

internal sealed class PlayTestHarness : IDisposable
{
    private readonly IPlayniteApi _previousLocalizationApi;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "GravitonPlayTests", Guid.NewGuid().ToString("N"));

    public GravitonContextFake Plugin { get; } = new();
    public PlayniteFake Playnite { get; } = new();
    public IEmunightAPI Emunight { get; } = Substitute.For<IEmunightAPI>();
    public GravitonPlayController Controller { get; }
    public GameSessionHandler Session { get; }
    public string Root => _root;

    public PlayTestHarness()
    {
        Directory.CreateDirectory(_root);
        Plugin.ConfigurePluginDataDirectory(_root);
        Plugin.Settings.SaveSyncEnabled = false;
        Plugin.Settings.KeepStatusSynced = false;
        Plugin.Settings.CaptureScreenshots = false;

        _previousLocalizationApi = Loc.Api;
        Loc.Api = Playnite.Api;

        var logger = new GravitonLogger(Plugin.Settings);
        Controller = new GravitonPlayController(Plugin.Context, Playnite.Api, logger, Emunight);
        Session = new GameSessionHandler(Plugin.Context, Playnite.Api, logger);

        Emunight.ExpandVariables(
            Arg.Any<string>(),
            Arg.Any<Game>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<bool>()).Returns(call =>
            {
                var arguments = call.ArgAt<string>(0);
                var romPath = call.ArgAt<string>(3);
                return $"{arguments} {Path.GetFileName(romPath)}";
            });
    }

    public Game AddGame(string gameID = "42", string? libraryID = null)
    {
        return new Game($"Game {gameID}")
        {
            LibraryId = libraryID ?? GravitonPlugin.Id,
            LibraryGameId = gameID,
            InstallState = InstallState.Installed
        };
    }

    public EmulatorMapping AddMapping(string platformSlug = "nes")
    {
        var mapping = new EmulatorMapping
        {
            MappingId = Guid.NewGuid(),
            RomMPlatform = new RomMPlatform { Id = 7, Slug = platformSlug },
            DestinationPath = _root
        };

        Plugin.Settings.Mappings.Add(mapping);
        return mapping;
    }

    public CustomEmulator ConfigureCustomEmulator(EmulatorMapping mapping, params string[] extensions)
    {
        var emulator = new CustomEmulator
        {
            StartupPath = Path.Combine(_root, "emulator.exe"),
            Arguments = "--load",
            FileTypes = new HashSet<string>(extensions)
        };

        mapping.Emulator = emulator;
        return emulator;
    }

    public RomMRomLocal AddROM(EmulatorMapping mapping, string gameID = "42", string? installedPath = null, bool isDirectory = false)
    {
        var rom = new RomMRomLocal
        {
            Id = int.Parse(gameID),
            MappingID = mapping.MappingId,
            InstalledPath = installedPath,
            IsInstalledPathDirectory = isDirectory
        };
        Plugin.ImportedGames.TryAdd(gameID, rom);
        return rom;
    }

    public string WriteGameFile(string relativePath, string contents = "rom")
    {
        var path = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return path;
    }

    public GetPlayActionsArgs GetPlayActions(Game game)
    {
        return new GetPlayActionsArgs(game);
    }

    public OnGameStartingEventArgs Starting(Game game, bool hasImagePath = true, bool hasEmulatorDir = true)
    {
        var controller = new AutomaticFilePlayController(new FileGameAction
        {
            Name = "Start",
            Path = Path.Combine(_root, "emulator.exe")
        });

        var args = new OnGameStartingEventArgs(game, controller);
        var properties = new Dictionary<string, object>();
        if (hasImagePath)
        {
            properties["ImagePath"] = Path.Combine(_root, "Game.nes");
        }
        if (hasEmulatorDir)
        {
            properties["EmulatorDir"] = _root;
        }

        typeof(OnGameStartingEventArgs).GetProperty(nameof(OnGameStartingEventArgs.StartProperties))!
            .SetValue(args, properties);

        return args;
    }

    public OnGameStartedEventArgs Started(OnGameStartingEventArgs args)
    {
        return new OnGameStartedEventArgs(args, new PlayController.GameStartedArgs(1234));
    }

    public OnGameStoppedEventArgs Stopped(OnGameStartingEventArgs args)
    {
        return new OnGameStoppedEventArgs(args, Started(args), new PlayController.GameStoppedArgs(10));
    }

    public void Dispose()
    {
        Loc.Api = _previousLocalizationApi;
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
