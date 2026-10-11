using Graviton.Models.Saves;
using Graviton.Models.RomM.Saves;
using Graviton.Notifications;
using Graviton.Saves;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;
using Graviton.Tests.Fakes.RomM;

using NSubstitute;

using Xunit;

namespace Graviton.Tests.Saves;

public sealed class SaveNegotiatorConflictGuardTests
{
    [Fact]
    public void ResolveConflict_WhenServerTimestampIsMissing_DoesNotOpenPlayniteWindow()
    {
        var playnite = new PlayniteFake();
        var negotiator = CreateNegotiator(playnite);
        var save = new GravitonSave
        {
            Status = SaveStatus.Conflicted,
            LastSyncedAt = new DateTime(2026, 10, 10, 11, 0, 0, DateTimeKind.Utc)
        };

        var result = negotiator.ResolveConflict(save);

        Assert.Equal(SaveSyncStatus.conflict, result);
        Assert.Equal(SaveStatus.Conflicted, save.Status);
        _ = playnite.Api.DidNotReceiveWithAnyArgs().CreateWindow(default!);
    }

    private static SaveNegotiator CreateNegotiator(PlayniteFake playnite)
    {
        var plugin = new GravitonContextFake();
        var romm = new RomMServerFake();
        var logger = new GravitonLogger(plugin.Settings);

        return new SaveNegotiator(plugin.Context, playnite.Api, logger, romm.Server);
    }
}
