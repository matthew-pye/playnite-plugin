using Graviton.Models.RomM;
using Graviton.Models.RomM.Saves;
using Graviton.Models.Saves;
using Graviton.Saves;

using Xunit;

namespace Graviton.Tests.Saves;

public sealed class SaveNegotiatorStatusTests
{
    [Theory]
    [InlineData(SaveSyncStatus.upload, SaveStatus.LocalNewer)]
    [InlineData(SaveSyncStatus.download, SaveStatus.RemoteNewer)]
    [InlineData(SaveSyncStatus.no_op, SaveStatus.Synced)]
    [InlineData(SaveSyncStatus.conflict, SaveStatus.Conflicted)]
    [InlineData(SaveSyncStatus.delete, SaveStatus.ServerDeleted)]
    public void ApplySoftNegotiationOperation_MapsActionToLocalStatus(SaveSyncStatus action, SaveStatus expectedStatus)
    {
        var save = new GravitonSave();
        var operation = new RomMNegotiateOperations
        {
            Action = action
        };

        SaveNegotiator.ApplySoftNegotiationOperation(save, operation);

        Assert.Equal(expectedStatus, save.Status);
    }

    [Fact]
    public void ApplySoftNegotiationOperation_CopiesServerHashAndTimestamp()
    {
        var save = new GravitonSave();
        var operation = new RomMNegotiateOperations
        {
            Action = SaveSyncStatus.download,
            ServerContentHash = "server-hash",
            ServerUpdatedAt = "2026-10-10T12:00:00Z"
        };

        SaveNegotiator.ApplySoftNegotiationOperation(save, operation);

        Assert.Equal("server-hash", save.ServerHash);
        Assert.NotNull(save.ServerLastUpdatedAt);
        Assert.Equal(
            new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc),
            save.ServerLastUpdatedAt.Value.ToUniversalTime());
    }

    [Fact]
    public void ApplySoftNegotiationOperation_WithMissingOperation_ClearsPreviousServerDetails()
    {
        var save = new GravitonSave
        {
            Status = SaveStatus.Synced,
            ServerHash = "old-hash",
            ServerLastUpdatedAt = new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc)
        };

        SaveNegotiator.ApplySoftNegotiationOperation(save, null);

        Assert.Equal(SaveStatus.Unknown, save.Status);
        Assert.Null(save.ServerHash);
        Assert.Null(save.ServerLastUpdatedAt);
    }

    [Fact]
    public void ApplySoftNegotiationOperation_WithMissingActionOrInvalidTimestamp_UsesUnknownStatus()
    {
        var save = new GravitonSave
        {
            ServerLastUpdatedAt = new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc)
        };
        var operation = new RomMNegotiateOperations
        {
            Action = null,
            ServerContentHash = "server-hash",
            ServerUpdatedAt = "not-a-timestamp"
        };

        SaveNegotiator.ApplySoftNegotiationOperation(save, operation);

        Assert.Equal(SaveStatus.Unknown, save.Status);
        Assert.Equal("server-hash", save.ServerHash);
        Assert.Null(save.ServerLastUpdatedAt);
    }
}
