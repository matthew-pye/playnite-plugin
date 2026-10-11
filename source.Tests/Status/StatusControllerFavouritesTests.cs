using Graviton.Models.RomM.Collection;
using Graviton.Notifications;
using Graviton.Status;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;
using Graviton.Tests.Fakes.RomM;

using NSubstitute;

using System.Net.Http;
using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Status;

public sealed class StatusControllerFavouritesTests
{
    private const string CollectionPath = "/api/collections";
    private const string CreatePath = "/api/collections?is_favorite=true&is_public=false";

    [Fact]
    public async Task PullFavourites_WhenCollectionExists_ReturnsFavouritesNotOtherCollections()
    {
        var (controller, romm) = CreateController();
        var collections = new[]
        {
            CreateCollection(4, "Currently Playing", 12),
            CreateCollection(9, "Favorites", 42, 84)
        };
        romm.ConfigureJsonGETResponse(CollectionPath, JsonSerializer.Serialize(collections));

        var favourites = await controller.PullFavourites();

        Assert.NotNull(favourites);
        Assert.Equal(9, favourites.Id);
        Assert.Equal(new[] { 42, 84 }, favourites.RomIDs);
        await romm.Server.DidNotReceive().POSTAsync(
            APIPath: CreatePath,
            content: Arg.Any<HttpContent?>(),
            PublicEndpoint: false);
    }

    [Fact]
    public async Task PullFavourites_WhenCollectionIsMissing_CreatesFavourites()
    {
        var (controller, romm) = CreateController();
        romm.ConfigureJsonGETResponse(CollectionPath, JsonSerializer.Serialize(new[] { CreateCollection(4, "Other") }));
        romm.ConfigureJsonPOSTResponse(CreatePath, JsonSerializer.Serialize(CreateCollection(9, "Favorites")));

        var favourites = await controller.PullFavourites();

        Assert.NotNull(favourites);
        Assert.Equal("Favorites", favourites.Name);
        Assert.Equal(9, favourites.Id);
        await romm.Server.Received(1).POSTAsync(
            APIPath: CreatePath,
            content: Arg.Is<HttpContent?>(content => content is MultipartFormDataContent),
            PublicEndpoint: false);
    }

    [Fact]
    public async Task PullFavourites_WhenCollectionRequestFails_DoesNotAttemptCreation()
    {
        var (controller, romm) = CreateController();
        romm.ConfigureJsonGETResponse(CollectionPath, null);

        var favourites = await controller.PullFavourites();

        Assert.Null(favourites);
        await romm.Server.DidNotReceive().POSTAsync(
            APIPath: CreatePath,
            content: Arg.Any<HttpContent?>(),
            PublicEndpoint: false);
    }

    [Fact]
    public async Task PullFavourites_WhenCreationFails_ReturnsNull()
    {
        var (controller, romm) = CreateController();
        romm.ConfigureJsonGETResponse(CollectionPath, "[]");
        romm.ConfigureJsonPOSTResponse(CreatePath, null);

        var favourites = await controller.PullFavourites();

        Assert.Null(favourites);
        await romm.Server.Received(1).POSTAsync(
            APIPath: CreatePath,
            content: Arg.Any<HttpContent?>(),
            PublicEndpoint: false);
    }

    [Fact]
    public async Task UpdateFavorites_SendsMultipartRequestToCorrectCollection()
    {
        var (controller, romm) = CreateController();
        romm.ConfigureJsonPUTResponse("/api/collections/9");
        var favourites = new RomMCollection
        {
            Id = 9,
            Name = "Favorites",
            RomIDs = [42, 84]
        };

        await controller.UpdateFavorites(favourites);

        await romm.Server.Received(1).PUTAsync(
            APIPath: "/api/collections/9",
            content: Arg.Is<HttpContent?>(content => content is MultipartFormDataContent),
            PublicEndpoint: false);
    }

    private static (StatusController Controller, RomMServerFake RomM) CreateController()
    {
        var plugin = new GravitonContextFake();
        var playnite = new PlayniteFake();
        var romm = new RomMServerFake();

        var controller = new StatusController(
            plugin: plugin.Context,
            playniteAPI: playnite.Api,
            logger: new GravitonLogger(plugin.Settings),
            romMServer: romm.Server);

        return (controller, romm);
    }

    private static object CreateCollection(int id, string name, params int[] romIDs)
    {
        return new
        {
            id,
            name,
            description = "",
            rom_ids = romIDs,
            path_cover_small = (string?)null,
            path_cover_large = (string?)null,
            path_covers_small = Array.Empty<string>(),
            path_covers_large = Array.Empty<string>(),
            is_public = false,
            is_favorite = name == "Favorites",
            is_virtual = false,
            is_smart = false,
            created_at = "2026-10-10T10:00:00Z",
            updated_at = "2026-10-10T10:00:00Z",
            rom_count = romIDs.Length,
            url_cover = (string?)null,
            user_id = 7,
            owner_username = "tester"
        };
    }
}
