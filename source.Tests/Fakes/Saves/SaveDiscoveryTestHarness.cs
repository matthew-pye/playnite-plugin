using Graviton.Models;
using Graviton.Models.ROM;
using Graviton.Models.RomM.Platform;
using Graviton.Models.RomM.Saves;
using Graviton.Models.Saves;
using Graviton.Notifications;
using Graviton.Saves;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;
using Graviton.Tests.Fakes.RomM;

using System.IO;
using System.Text;
using System.Text.Json;

namespace Graviton.Tests.Fakes.Saves;

internal sealed class SaveDiscoveryTestHarness : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Graviton.Tests", Guid.NewGuid().ToString("N"));

    public GravitonContextFake Plugin { get; } = new();
    public PlayniteFake Playnite { get; } = new();
    public RomMServerFake RomM { get; } = new();
    public SaveDiscovery Discovery { get; }
    public string Root => _root;

    public SaveDiscoveryTestHarness()
    {
        Directory.CreateDirectory(_root);
        Plugin.ConfigurePluginDataDirectory(_root);
        Discovery = new SaveDiscovery(
            Plugin.Context,
            Playnite.Api,
            new GravitonLogger(Plugin.Settings),
            RomM.Server);
    }

    public EmulatorMapping AddMapping(int platformId = 7, SaveLayoutStyle layout = SaveLayoutStyle.SingleFile, string extensions = "sav")
    {
        var mapping = new EmulatorMapping
        {
            MappingId = Guid.NewGuid(),
            RomMPlatform = new RomMPlatform { Id = platformId, Name = $"Platform {platformId}" },
            FindSaveLayout = layout,
            FindSaveFileExtensions = extensions,
            SavePath = Path.Combine(_root, $"saves-{Guid.NewGuid():N}")
        };
        Directory.CreateDirectory(mapping.SavePath);
        Plugin.Settings.Mappings.Add(mapping);
        return mapping;
    }

    public RomMRomLocal AddROM(EmulatorMapping mapping, int id, string filename = "Mario.nes", string? saveTarget = null, bool imported = true)
    {
        var rom = new RomMRomLocal
        {
            Id = id,
            Name = $"Game {id}",
            FileName = filename,
            SaveTarget = saveTarget,
            MappingID = mapping.MappingId
        };

        if (imported)
            Plugin.ImportedGames.TryAdd(id.ToString(), rom);

        return rom;
    }

    public string WriteSave(EmulatorMapping mapping, string relativePath, string contents)
    {
        var path = Path.Combine(mapping.SavePath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents, Encoding.UTF8);
        return path;
    }

    public void ConfigureMappingSaves(EmulatorMapping mapping, params RomMSave[] saves)
    {
        RomM.ConfigureJsonGETResponse($"/api/saves?platform_id={mapping.RomMPlatformId}", RomMSaveFixtures.CreateResponse(saves));
    }

    public void ConfigureAllSaves(params RomMSave[] saves)
    {
        RomM.ConfigureJsonGETResponse("/api/saves", RomMSaveFixtures.CreateResponse(saves));
    }

    public void ConfigureROMSaves(int romId, params RomMSave[] saves)
    {
        RomM.ConfigureJsonGETResponse($"/api/saves?rom_id={romId}", RomMSaveFixtures.CreateResponse(saves));
    }

    public void ConfigureMemoryCards(params object[] cards)
    {
        RomM.ConfigureJsonGETResponse("/api/memory-cards", JsonSerializer.Serialize(cards));
    }

    public void ConfigureMemoryCardVersions(int memoryCardId, params object[] versions)
    {
        RomM.ConfigureJsonGETResponse($"/api/memory-cards/{memoryCardId}/versions", JsonSerializer.Serialize(versions));
    }

    public static object MemoryCard(int id, int platformId)
    {
        return new
        {
            id,
            user_id = 7,
            platform_id = platformId,
            emulator = "RetroArch",
            name = $"Card {id}",
            slot = 0,
            is_public = false,
            created_at = "2026-10-01T12:00:00Z",
            updated_at = "2026-10-11T12:00:00Z"
        };
    }

    public static object MemoryCardVersion(int id, int cardId, string hash, string createdAt)
    {
        return new
        {
            id,
            memory_card_id = cardId,
            file_name = "memory.mcd",
            file_size_bytes = 1024,
            content_hash = hash,
            download_path = $"/api/memory-cards/versions/{id}/content",
            missing_from_fs = false,
            created_at = createdAt,
            updated_at = createdAt
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
