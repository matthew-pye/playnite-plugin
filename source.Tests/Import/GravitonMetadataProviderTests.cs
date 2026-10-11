using Graviton.Import;
using Graviton.Notifications;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;
using Graviton.Tests.Fakes.RomM;

using NSubstitute;

using Playnite;


using Xunit;

namespace Graviton.Tests.Import;

public sealed class GravitonMetadataProviderTests
{
    [Fact]
    public async Task PullRomData_ForOtherLibraries_DoesNotContactRomM()
    {
        var (session, _, romm) = CreateSession("42", "another.library");

        var pulled = await session.PullRomData();

        Assert.False(pulled);
        await romm.Server.DidNotReceive().GETAsync(Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task PullRomData_WithMalformedRomId_DoesNotContactRomM()
    {
        var (session, _, romm) = CreateSession("not-an-id");

        var pulled = await session.PullRomData();

        Assert.False(pulled);
        await romm.Server.DidNotReceive().GETAsync(Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task PullRomData_WhenRomMReturnsNull_ReturnsFalse()
    {
        var (session, _, romm) = CreateSession();
        romm.ConfigureJsonGETResponse("/api/roms/42", null);

        var pulled = await session.PullRomData();

        Assert.False(pulled);
        await romm.Server.Received(1).GETAsync("/api/roms/42", false);
    }

    [Theory]
    [InlineData(BuiltInGameDataId.Name, "Example Game")]
    [InlineData(BuiltInGameDataId.Description, "An example summary")]
    [InlineData(BuiltInGameDataId.Platforms, "Nintendo 64")]
    public async Task GetDataAsync_MapsBasicRomMetadata(string dataId, string expected)
    {
        var (session, _, romm) = CreateSession();
        ConfigureRomData(romm);

        Assert.True(await session.PullRomData());

        var value = await session.GetDataAsync(new MetadataProviderGameSession.GetDataArgs(
            dataId,
            TestContext.Current.CancellationToken));

        Assert.Equal(expected, Assert.IsType<string>(value));
    }

    [Fact]
    public async Task PullRomData_WithSuffixInLibraryGameId_RequestsTheRomIdOnly()
    {
        var (session, _, romm) = CreateSession("42:alternate");
        ConfigureRomData(romm);

        Assert.True(await session.PullRomData());

        await romm.Server.Received(1).GETAsync("/api/roms/42", false);
    }

    [Fact]
    public async Task GetDataAsync_MapsEstimatedInstallSize()
    {
        var (session, _, romm) = CreateSession();
        ConfigureRomData(romm);

        Assert.True(await session.PullRomData());

        var value = await session.GetDataAsync(new MetadataProviderGameSession.GetDataArgs(
            BuiltInGameDataId.EstimatedInstallSize,
            TestContext.Current.CancellationToken));

        Assert.Equal(4096UL, Assert.IsType<ulong>(value));
    }

    [Fact]
    public async Task GetDataAsync_IncludesMetadataCollectionsWhenEnabled()
    {
        var (session, _, romm) = CreateSession();
        ConfigureRomData(romm);

        Assert.True(await session.PullRomData());

        var value = await session.GetDataAsync(new MetadataProviderGameSession.GetDataArgs(
            BuiltInGameDataId.Categories,
            TestContext.Current.CancellationToken));

        Assert.Contains("RPGs", Assert.IsType<List<string>>(value));
    }

    [Fact]
    public async Task GetDataAsync_ExcludesMetadataCollectionsWhenDisabled()
    {
        var (session, plugin, romm) = CreateSession();
        plugin.Settings.AddMetadataProviderCollections = false;
        ConfigureRomData(romm);

        Assert.True(await session.PullRomData());

        var value = await session.GetDataAsync(new MetadataProviderGameSession.GetDataArgs(
            BuiltInGameDataId.Categories,
            TestContext.Current.CancellationToken));

        Assert.Empty(Assert.IsType<List<string>>(value));
    }

    [Fact]
    public async Task GetDataAsync_MapsUserFlagsAndFavouriteCollection()
    {
        var (session, _, romm) = CreateSession();
        ConfigureRomData(romm);

        Assert.True(await session.PullRomData());

        var favourite = await session.GetDataAsync(new MetadataProviderGameSession.GetDataArgs(
            BuiltInGameDataId.Favorite,
            TestContext.Current.CancellationToken));
        var hidden = await session.GetDataAsync(new MetadataProviderGameSession.GetDataArgs(
            BuiltInGameDataId.Hidden,
            TestContext.Current.CancellationToken));

        Assert.True(Assert.IsType<bool>(favourite));
        Assert.True(Assert.IsType<bool>(hidden));
    }

    [Fact]
    public async Task GetDataAsync_UnknownDataId_ReturnsNull()
    {
        var (session, _, romm) = CreateSession();
        ConfigureRomData(romm);

        Assert.True(await session.PullRomData());

        var value = await session.GetDataAsync(new MetadataProviderGameSession.GetDataArgs(
            "Graviton.UnknownMetadata",
            TestContext.Current.CancellationToken));

        Assert.Null(value);
    }

    private static (GravitonMetadataProviderGameSession Session, GravitonContextFake Plugin, RomMServerFake RomM) CreateSession(
        string libraryGameId = "42",
        string? libraryId = null)
    {
        var plugin = new GravitonContextFake();
        plugin.Settings.AddCollectiontoPlayniteCategory = false;
        plugin.Settings.AddSmartCollectiontoPlayniteCategory = false;
        var playnite = new PlayniteFake();
        var romm = new RomMServerFake();
        var game = new Game("Example Game")
        {
            LibraryId = libraryId ?? GravitonPlugin.Id,
            LibraryGameId = libraryGameId
        };
        var session = new GravitonMetadataProviderGameSession(
            plugin: plugin.Context,
            playniteAPI: playnite.Api,
            logger: new GravitonLogger(plugin.Settings),
            romMServer: romm.Server,
            game: game);

        return (session, plugin, romm);
    }

    private static void ConfigureRomData(RomMServerFake romm)
    {
        var rom = RomMRomFixtures.CreateDetailedRom();

        romm.ConfigureJsonGETResponse("/api/roms/42", rom.ToJsonString());
        romm.ConfigureJsonGETResponse("/api/users/me", RomMRomFixtures.CreateUserResponse());
    }
}
