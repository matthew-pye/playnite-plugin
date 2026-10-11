using Graviton.Import;
using Graviton.Notifications;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;
using Graviton.Tests.Fakes.RomM;

using NSubstitute;

using System.IO;
using System.Text;
using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Import;

public sealed class GravitonImportPlatformTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "Graviton.Tests",
        Guid.NewGuid().ToString("N"));

    public GravitonImportPlatformTests()
    {
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task FetchPlatforms_WhenServerReturnsEmptyList_ReturnsEmptyListAndCreatesIconDirectory()
    {
        var (controller, _, romm) = CreateController();
        romm.ConfigureJsonGETResponse("/api/platforms", "[]");

        var platforms = await controller.FetchPlatforms();

        Assert.NotNull(platforms);
        Assert.Empty(platforms);
        Assert.True(Directory.Exists(Path.Combine(_testDirectory, "Platforms")));
        await romm.Server.Received(1).GETAsync("/api/platforms", false);
        await romm.Server.DidNotReceive().RawGETAsync(Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task FetchPlatforms_WhenServerReturnsNull_ClearsAuthenticationTimestamp()
    {
        var (controller, plugin, romm) = CreateController();
        plugin.Settings.AccountState.LastAuthenticated = DateTime.UtcNow;
        romm.ConfigureJsonGETResponse("/api/platforms", null);

        var platforms = await controller.FetchPlatforms();

        Assert.Null(platforms);
        Assert.Null(plugin.Settings.AccountState.LastAuthenticated);
        Assert.False(Directory.Exists(Path.Combine(_testDirectory, "Platforms")));
    }

    [Fact]
    public async Task FetchPlatforms_WhenServerReturnsData_PreservesAuthenticationTimestamp()
    {
        var (controller, plugin, romm) = CreateController();
        var authenticated = DateTime.UtcNow;
        plugin.Settings.AccountState.LastAuthenticated = authenticated;
        romm.ConfigureJsonGETResponse("/api/platforms", "[]");

        await controller.FetchPlatforms();

        Assert.Equal(authenticated, plugin.Settings.AccountState.LastAuthenticated);
    }

    [Fact]
    public async Task FetchPlatforms_DeserializesPlatformsWithoutRequestingUnsafeSlugs()
    {
        var (controller, _, romm) = CreateController();
        romm.ConfigureJsonGETResponse("/api/platforms", JsonSerializer.Serialize(new[]
        {
            CreatePlatform(1, "Nintendo 64", "n64/../../other", 100),
            CreatePlatform(2, "PlayStation", "psx?other", 200)
        }));

        var platforms = await controller.FetchPlatforms();

        Assert.NotNull(platforms);
        Assert.Equal(2, platforms.Count);
        Assert.Equal(1, platforms[0].Id);
        Assert.Equal("Nintendo 64", platforms[0].Name);
        Assert.Equal(100, platforms[0].RomCount);
        Assert.Equal(2, platforms[1].Id);
        await romm.Server.DidNotReceive().RawGETAsync(Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task FetchPlatforms_WhenIconRequestFails_StillReturnsPlatformData()
    {
        var (controller, _, romm) = CreateController();
        romm.ConfigureJsonGETResponse("/api/platforms", JsonSerializer.Serialize(new[]
        {
            CreatePlatform(3, "Game Boy", "gb", 75)
        }));

        var platforms = await controller.FetchPlatforms();

        var platform = Assert.Single(Assert.IsType<List<Graviton.Models.RomM.Platform.RomMPlatform>>(platforms));
        Assert.Equal(3, platform.Id);
        await romm.Server.Received(1).RawGETAsync("/assets/platforms/gb.svg", false);
        Assert.False(File.Exists(Path.Combine(_testDirectory, "Platforms", "gb.png")));
    }

    [Fact]
    public async Task FetchPlatforms_WithValidSvgIcon_SavesPngForPlatform()
    {
        var (controller, _, romm) = CreateController();
        romm.ConfigureJsonGETResponse("/api/platforms", JsonSerializer.Serialize(new[]
        {
            CreatePlatform(3, "Game Boy", "gb", 75)
        }));
        var svg = """
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 16 16"><rect width="16" height="16" fill="#ffffff" /></svg>
            """;
        romm.ConfigureRawGETResponse("/assets/platforms/gb.svg", Encoding.UTF8.GetBytes(svg));

        var platforms = await controller.FetchPlatforms();

        Assert.Single(Assert.IsType<List<Graviton.Models.RomM.Platform.RomMPlatform>>(platforms));
        var imagePath = Path.Combine(_testDirectory, "Platforms", "gb.png");
        Assert.True(File.Exists(imagePath));
        var bytes = File.ReadAllBytes(imagePath);
        Assert.True(bytes.Length > 8);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes.Take(8));
    }

    private (GravitonImportController Controller, GravitonContextFake Plugin, RomMServerFake RomM) CreateController()
    {
        var plugin = new GravitonContextFake();
        plugin.ConfigurePluginDataDirectory(_testDirectory);
        var playnite = new PlayniteFake();
        var romm = new RomMServerFake();
        var controller = new GravitonImportController(
            plugin: plugin.Context,
            playniteAPI: playnite.Api,
            logger: new GravitonLogger(plugin.Settings),
            server: romm.Server);

        return (controller, plugin, romm);
    }

    private static object CreatePlatform(int id, string name, string slug, int romCount)
    {
        return new
        {
            id,
            name,
            slug,
            fs_slug = slug,
            rom_count = romCount,
            display_name = name,
            igdb_slug = (string?)null,
            moby_slug = (string?)null,
            hltb_slug = (string?)null,
            libretro_slug = (string?)null,
            created_at = "2026-10-01T10:00:00Z",
            updated_at = "2026-10-01T10:00:00Z",
            fs_size_bytes = 0,
            custom_name = (string?)null,
            description = (string?)null,
            igdb_id = (int?)null,
            sgdb_id = (int?)null,
            moby_id = (int?)null,
            launchbox_id = (int?)null,
            ss_id = (int?)null,
            ra_id = (int?)null,
            hasheous_id = (int?)null,
            tgdb_id = (int?)null,
            flashpoint_id = (int?)null,
            category = (string?)null,
            generation = (int?)null,
            family_name = (string?)null,
            family_slug = (string?)null,
            url = (string?)null,
            url_logo = (string?)null,
            is_unidentified = false,
            is_identified = true,
            missing_from_fs = false,
            firmware = Array.Empty<object>(),
            abbreviation = slug.ToUpperInvariant(),
            alternative_names = Array.Empty<string>(),
            firmware_count = 0
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
