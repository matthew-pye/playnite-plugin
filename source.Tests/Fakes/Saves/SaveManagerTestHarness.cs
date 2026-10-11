using Graviton.Models;
using Graviton.Models.ROM;
using Graviton.Models.Saves;
using Graviton.Notifications;
using Graviton.Saves;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;
using Graviton.Tests.Fakes.RomM;

using Playnite;

using System.Collections.ObjectModel;
using System.IO;

namespace Graviton.Tests.Fakes.Saves;

internal sealed class SaveManagerTestHarness : IDisposable
{
    private readonly IPlayniteApi _previousLocalizationApi;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Graviton.Tests", Guid.NewGuid().ToString("N"));

    public GravitonContextFake Plugin { get; } = new();
    public PlayniteFake Playnite { get; } = new();
    public RomMServerFake RomM { get; } = new();
    public SaveManager Manager { get; }
    public Guid MappingID { get; } = Guid.NewGuid();
    public string SavesDirectory => Path.Combine(_root, "saves");
    public string TempDirectory => Path.Combine(_root, "temp");
    public RomMRomLocal ROM { get; }
    public GravitonSave Save { get; }

    public SaveManagerTestHarness()
    {
        Directory.CreateDirectory(SavesDirectory);
        Directory.CreateDirectory(TempDirectory);
        Directory.CreateDirectory(Path.Combine(_root, "Games"));

        Plugin.ConfigurePluginDataDirectory(_root);
        Plugin.Settings.AccountState.DeviceID = "test-device";
        Plugin.Settings.Mappings.Add(new EmulatorMapping
        {
            MappingId = MappingID,
            SavePath = SavesDirectory
        });

        Save = new GravitonSave
        {
            ROMID = 42,
            SaveID = 987,
            Filename = "autosave.sav",
            Slot = "autosave",
            Status = SaveStatus.RemoteNewer,
            SourceFilePaths = new ObservableCollection<string>
            {
                EmulatorMapping.SavePathToken + "/autosave.sav"
            },
            ServerLastUpdatedAt = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc),
            ContentHash = "previous-local-hash",
            LastSyncedContentHash = "previous-synced-hash"
        };

        ROM = new RomMRomLocal
        {
            Id = 42,
            Name = "Test Game",
            MappingID = MappingID,
            LocalSave = Save
        };

        Plugin.ImportedGames.TryAdd("42", ROM);

        _previousLocalizationApi = Loc.Api;
        Loc.Api = Playnite.Api;

        Manager = new SaveManager(
            Plugin.Context,
            Playnite.Api,
            new GravitonLogger(Plugin.Settings),
            RomM.Server);
    }

    public string DownloadPath => $"/api/saves/{Save.SaveID}/content?device_id={Plugin.Settings.AccountState.DeviceID}&optimistic=false";

    public string UploadPath(bool overwrite = false)
    {
        return $"/api/saves?rom_id={ROM.Id}&slot={Save.Slot}&autocleanup={Plugin.Settings.AutoCleanupSaves}&autocleanup_limit={Plugin.Settings.AutoCleanupSavesLimit}&device_id={Plugin.Settings.AccountState.DeviceID}&overwrite={overwrite}";
    }

    public string WriteLocalSave(byte[] bytes, string relativePath = "autosave.sav")
    {
        var path = Path.Combine(SavesDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
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
