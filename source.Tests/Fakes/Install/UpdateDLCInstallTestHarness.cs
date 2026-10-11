using Graviton.Install;
using Graviton.Install.Downloads;
using Graviton.Models;
using Graviton.Models.Install;
using Graviton.Models.ROM;
using Graviton.Models.RomM.Rom;
using Graviton.Notifications;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;
using Graviton.Tests.Fakes.RomM;

using Playnite;

using System.Collections.ObjectModel;
using System.IO;

namespace Graviton.Tests.Fakes.Install;

internal sealed class UpdateDLCInstallTestHarness : IDisposable
{
    private readonly IPlayniteApi _previousLocalizationApi;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "GravitonUpdateDLCTests", Guid.NewGuid().ToString("N"));

    public GravitonContextFake Plugin { get; } = new();
    public PlayniteFake Playnite { get; } = new();
    public RomMServerFake RomM { get; } = new();
    public EmulatorMapping Mapping { get; } = new();
    public GravitonInstallUpdateDLCController Controller { get; }
    public string Root => _root;
    public string UpdateDestination => Path.Combine(_root, "Updates");
    public string DLCDestination => Path.Combine(_root, "DLC");

    public UpdateDLCInstallTestHarness()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Games"));
        Plugin.ConfigurePluginDataDirectory(_root);

        _previousLocalizationApi = Loc.Api;
        Loc.Api = Playnite.Api;
        CLIInstallDefinitions.Initialize();
        TitleIDInstallDefinitions.Initialize();

        Mapping.UpdateInstallMode = InstallMode.All;
        Mapping.DLCInstallMode = InstallMode.All;
        Mapping.UpdateInstallStyle = InstallStyles.Folder;
        Mapping.DLCInstallStyle = InstallStyles.Folder;
        Mapping.UpdateInstallPath = UpdateDestination;
        Mapping.DLCInstallPath = DLCDestination;

        Controller = new GravitonInstallUpdateDLCController(
            plugin: Plugin.Context,
            playniteAPI: Playnite.Api,
            logger: new GravitonLogger(Plugin.Settings),
            server: RomM.Server);
    }

    public GameInstallInfo InstallInfo(EmulatorMapping? mapping = null, bool useDefault = true)
    {
        return new GameInstallInfo
        {
            Id = 42,
            GameName = "Example Game",
            FileName = "game.rom",
            Mapping = useDefault ? Mapping : mapping,
            TitleID = "TITLE123",
            SaveTarget = "SLOT123"
        };
    }

    public RomMRom RemoteROM(params string[] categories)
    {
        return new RomMRom
        {
            Id = 42,
            Files = categories.Select((category, index) =>
            {
                return new RomMFile
                {
                    Id = index + 100,
                    Category = category,
                    FileName = $"file{index}.pkg",
                    FullPath = $"romm/file{index}.pkg"
                };
            }).ToList()
        };
    }

    public static UpdateDLCCandidate Candidate(
        string name = "Patch One",
        string? category = null,
        int[]? ids = null,
        string fileName = "patch.pkg",
        long size = 256)
    {
        return new UpdateDLCCandidate(
            name: name,
            filename: fileName,
            category: category ?? RomMCategory.Update,
            fileIDs: ids ?? [100],
            size: size);
    }

    public async Task<DownloadRequest?> Build(
        ObservableCollection<UpdateDLCCandidate> candidates,
        string category = "update",
        GameInstallInfo? info = null,
        RomMRom? rom = null,
        DownloadRequest? previous = null)
    {
        return await Controller.BuildUpdateDLCRequests(
            GameData: info ?? InstallInfo(),
            ROM: rom ?? RemoteROM(category),
            candidates: candidates,
            category: category,
            previousInstall: previous);
    }

    public static ObservableCollection<UpdateDLCCandidate> Candidates(params UpdateDLCCandidate[] candidates)
    {
        return new ObservableCollection<UpdateDLCCandidate>(candidates);
    }

    public RomMRomLocal RegisterLocalROM()
    {
        var rom = new RomMRomLocal
        {
            Id = 42,
            Name = "Example Game"
        };
        Plugin.ImportedGames["42"] = rom;
        return rom;
    }

    public string WriteDownloadedFile(DownloadRequest request, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(request.DownloadPath)!);
        File.WriteAllBytes(request.DownloadPath, bytes);
        return request.DownloadPath;
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
