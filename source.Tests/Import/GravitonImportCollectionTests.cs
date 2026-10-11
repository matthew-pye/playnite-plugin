using Graviton.Import;
using Graviton.Notifications;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;
using Graviton.Tests.Fakes.RomM;

using NSubstitute;

using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Import;

public sealed class GravitonImportCollectionTests
{
    [Theory]
    [InlineData(false, "/api/collections")]
    [InlineData(true, "/api/collections/smart")]
    public async Task FetchCollections_WhenServerReturnsEmptyArray_ReturnsEmptyList(bool smart, string path)
    {
        var (controller, romm) = CreateController();
        romm.ConfigureJsonGETResponse(path, "[]");

        var collections = smart
            ? await controller.FetchSmartCollections()
            : await controller.FetchManualCollections();

        Assert.Empty(collections);
        await romm.Server.Received(1).GETAsync(path, false);
    }

    [Theory]
    [InlineData(false, "/api/collections")]
    [InlineData(true, "/api/collections/smart")]
    public async Task FetchCollections_WhenServerReturnsNull_ReturnsEmptyList(bool smart, string path)
    {
        var (controller, romm) = CreateController();
        romm.ConfigureJsonGETResponse(path, null);

        var collections = smart
            ? await controller.FetchSmartCollections()
            : await controller.FetchManualCollections();

        Assert.Empty(collections);
    }

    [Theory]
    [InlineData(false, "/api/collections")]
    [InlineData(true, "/api/collections/smart")]
    public async Task FetchCollections_PreservesIdsNamesAndRomMembership(bool smart, string path)
    {
        var (controller, romm) = CreateController();
        var collectionsJson = smart
            ? JsonSerializer.Serialize(new[]
            {
                CreateSmartCollection(7, "Recent RPGs", [11, 12]),
                CreateSmartCollection(9, "Handheld", [23])
            })
            : JsonSerializer.Serialize(new[]
            {
                CreateManualCollection(7, "Recent RPGs", [11, 12]),
                CreateManualCollection(9, "Handheld", [23])
            });
        romm.ConfigureJsonGETResponse(path, collectionsJson);

        var collections = smart
            ? await controller.FetchSmartCollections()
            : await controller.FetchManualCollections();

        Assert.Equal(2, collections.Count);
        Assert.Equal(7, collections[0].Id);
        Assert.Equal("Recent RPGs", collections[0].Name);
        Assert.Equal(new[] { 11, 12 }, collections[0].RomIDs);
        Assert.Equal(9, collections[1].Id);
        Assert.Equal("Handheld", collections[1].Name);
        Assert.Equal(new[] { 23 }, collections[1].RomIDs);
    }

    [Fact]
    public async Task FetchManualCollections_PreservesFavouritesMarker()
    {
        var (controller, romm) = CreateController();
        var collection = CreateManualCollection(3, "Favorites", [42], true);
        romm.ConfigureJsonGETResponse("/api/collections", JsonSerializer.Serialize(new[] { collection }));

        var collections = await controller.FetchManualCollections();

        var favourite = Assert.Single(collections);
        Assert.True(favourite.IsFavorite);
        Assert.Equal(42, Assert.Single(favourite.RomIDs));
    }

    [Fact]
    public async Task FetchManualCollections_AcceptsAdditionalServerFields()
    {
        var (controller, romm) = CreateController();
        var json = JsonSerializer.Serialize(new[] { CreateManualCollection(3, "Favorites", [42]) });
        json = json.Replace("\"is_favorite\":false", "\"is_favorite\":false,\"future_field\":{\"enabled\":true}");
        romm.ConfigureJsonGETResponse("/api/collections", json);

        var collections = await controller.FetchManualCollections();

        Assert.Equal(3, Assert.Single(collections).Id);
    }

    [Fact]
    public async Task FetchManualAndSmartCollections_UseSeparateEndpoints()
    {
        var (controller, romm) = CreateController();
        romm.ConfigureJsonGETResponse("/api/collections", JsonSerializer.Serialize(new[]
        {
            CreateManualCollection(4, "Manual", [1])
        }));
        romm.ConfigureJsonGETResponse("/api/collections/smart", JsonSerializer.Serialize(new[]
        {
            CreateSmartCollection(5, "Smart", [2])
        }));

        var manual = await controller.FetchManualCollections();
        var smart = await controller.FetchSmartCollections();

        Assert.Equal("Manual", Assert.Single(manual).Name);
        Assert.Equal("Smart", Assert.Single(smart).Name);
        await romm.Server.Received(1).GETAsync("/api/collections", false);
        await romm.Server.Received(1).GETAsync("/api/collections/smart", false);
    }

    private static (GravitonImportController Controller, RomMServerFake RomM) CreateController()
    {
        var plugin = new GravitonContextFake();
        var playnite = new PlayniteFake();
        var romm = new RomMServerFake();
        var controller = new GravitonImportController(
            plugin: plugin.Context,
            playniteAPI: playnite.Api,
            logger: new GravitonLogger(plugin.Settings),
            server: romm.Server);

        return (controller, romm);
    }

    private static object CreateManualCollection(int id, string name, int[] romIds, bool favourite = false)
    {
        return new
        {
            id,
            name,
            description = "",
            rom_ids = romIds,
            path_cover_small = (string?)null,
            path_cover_large = (string?)null,
            path_covers_small = Array.Empty<string>(),
            path_covers_large = Array.Empty<string>(),
            is_public = false,
            is_favorite = favourite,
            is_virtual = false,
            is_smart = false,
            created_at = "2026-10-01T10:00:00Z",
            updated_at = "2026-10-01T10:00:00Z",
            rom_count = romIds.Length,
            url_cover = (string?)null,
            user_id = 1,
            owner_username = "tester"
        };
    }

    private static object CreateSmartCollection(int id, string name, int[] romIds)
    {
        return new
        {
            id,
            name,
            description = "",
            rom_ids = romIds,
            path_cover_small = (string?)null,
            path_cover_large = (string?)null,
            path_covers_small = Array.Empty<string>(),
            path_covers_large = Array.Empty<string>(),
            is_public = false,
            is_favorite = false,
            is_virtual = false,
            is_smart = true,
            created_at = "2026-10-01T10:00:00Z",
            updated_at = "2026-10-01T10:00:00Z",
            rom_count = romIds.Length,
            filter_criteria = new { },
            filter_summary = "",
            user_id = 1,
            owner_username = "tester"
        };
    }
}
