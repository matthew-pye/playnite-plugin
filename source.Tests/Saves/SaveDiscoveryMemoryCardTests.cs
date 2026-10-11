using Graviton.Models.Saves;
using Graviton.Tests.Fakes.Saves;

using NSubstitute;

using Xunit;

namespace Graviton.Tests.Saves;

public sealed class SaveDiscoveryMemoryCardTests
{
    [Fact]
    public async Task DiscoverMemoryCards_WithNoMappings_ReturnsEmptyListWithoutServerRequest()
    {
        using var harness = new SaveDiscoveryTestHarness();

        var cards = await harness.Discovery.DiscoverMemoryCards();

        Assert.Empty(Assert.IsType<List<MemoryCardSave>>(cards));
        await harness.RomM.Server.DidNotReceive().GETAsync("/api/memory-cards", false);
    }

    [Fact]
    public async Task DiscoverMemoryCards_WithTrackedLocalCard_IncludesExistingCard()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping();
        var localCard = new MemoryCardSave
        {
            EmulatorMappingID = mapping.MappingId,
            MemoryCardID = 51,
            ContentHash = "same-hash",
            Status = SaveStatus.Synced
        };
        mapping.MemoryCardSave = localCard;
        harness.ConfigureMemoryCards();

        var cards = await harness.Discovery.DiscoverMemoryCards(mapping);

        Assert.Same(localCard, Assert.Single(Assert.IsType<List<MemoryCardSave>>(cards)));
        Assert.Equal(SaveStatus.Synced, localCard.Status);
    }

    [Fact]
    public async Task DiscoverMemoryCards_WhenRemoteCardHasNoVersions_SkipsItWithoutThrowing()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping(platformId: 7);
        harness.ConfigureMemoryCards(SaveDiscoveryTestHarness.MemoryCard(51, 7));
        harness.ConfigureMemoryCardVersions(51);

        var cards = await harness.Discovery.DiscoverMemoryCards(mapping);

        Assert.Empty(Assert.IsType<List<MemoryCardSave>>(cards));
    }

    [Fact]
    public async Task DiscoverMemoryCards_SkipsRemoteCardsWithoutMatchingPlatform()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping(platformId: 7);
        harness.ConfigureMemoryCards(SaveDiscoveryTestHarness.MemoryCard(51, 99));

        var cards = await harness.Discovery.DiscoverMemoryCards(mapping);

        Assert.Empty(Assert.IsType<List<MemoryCardSave>>(cards));
        await harness.RomM.Server.DidNotReceive().GETAsync("/api/memory-cards/51/versions", false);
    }

    [Fact]
    public async Task DiscoverMemoryCards_WhenTrackedServerCardIsNewer_MarksLocalCardRemoteNewer()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping(platformId: 7);
        var localCard = new MemoryCardSave
        {
            EmulatorMappingID = mapping.MappingId,
            MemoryCardID = 51,
            ContentHash = "local-hash",
            Status = SaveStatus.Synced,
            CreatedAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc)
        };
        mapping.MemoryCardSave = localCard;
        harness.ConfigureMemoryCards(SaveDiscoveryTestHarness.MemoryCard(51, 7));
        harness.ConfigureMemoryCardVersions(51,
            SaveDiscoveryTestHarness.MemoryCardVersion(503, 51, "remote-hash", "2026-10-10T12:00:00Z"));

        var cards = await harness.Discovery.DiscoverMemoryCards(mapping);

        Assert.Same(localCard, Assert.Single(Assert.IsType<List<MemoryCardSave>>(cards)));
        Assert.Equal(SaveStatus.RemoteNewer, localCard.Status);
        Assert.Empty(localCard.HistoricSaves!);
    }

    [Fact]
    public async Task DiscoverMemoryCards_WhenTrackedHashMatches_PreservesCurrentSyncStatus()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping(platformId: 7);
        var localCard = new MemoryCardSave
        {
            EmulatorMappingID = mapping.MappingId,
            MemoryCardID = 51,
            ContentHash = "same-hash",
            Status = SaveStatus.Synced,
            CreatedAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc)
        };
        mapping.MemoryCardSave = localCard;
        harness.ConfigureMemoryCards(SaveDiscoveryTestHarness.MemoryCard(51, 7));
        harness.ConfigureMemoryCardVersions(51,
            SaveDiscoveryTestHarness.MemoryCardVersion(503, 51, "same-hash", "2026-10-10T12:00:00Z"));

        var cards = await harness.Discovery.DiscoverMemoryCards(mapping);

        Assert.Same(localCard, Assert.Single(Assert.IsType<List<MemoryCardSave>>(cards)));
        Assert.Equal(SaveStatus.Synced, localCard.Status);
    }

    [Fact]
    public async Task DiscoverMemoryCards_WhenRemoteRequestFails_KeepsTrackedLocalCard()
    {
        using var harness = new SaveDiscoveryTestHarness();
        var mapping = harness.AddMapping();
        var localCard = new MemoryCardSave
        {
            EmulatorMappingID = mapping.MappingId,
            MemoryCardID = 51,
            ContentHash = "local-hash",
            Status = SaveStatus.LocalNewer
        };
        mapping.MemoryCardSave = localCard;
        harness.RomM.ConfigureJsonGETResponse("/api/memory-cards", null);

        var cards = await harness.Discovery.DiscoverMemoryCards(mapping);

        Assert.Same(localCard, Assert.Single(Assert.IsType<List<MemoryCardSave>>(cards)));
        Assert.Equal("local-hash", localCard.ContentHash);
        Assert.Equal(SaveStatus.LocalNewer, localCard.Status);
    }
}
