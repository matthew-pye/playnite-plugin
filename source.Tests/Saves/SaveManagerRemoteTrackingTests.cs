using Graviton.Models.Saves;
using Graviton.Tests.Fakes.Saves;

using NSubstitute;

using Playnite;

using System.Collections.ObjectModel;
using System.IO;
using System.Net;

using Xunit;

namespace Graviton.Tests.Saves;

[Collection("SaveManagerTransferTests")]
public sealed class SaveManagerRemoteTrackingTests
{
    [Fact]
    public async Task TrackNewRemoteSave_WhenROMIsUnknown_DoesNotFetchSave()
    {
        using var test = new SaveManagerTestHarness();
        test.Save.ROMID = 999;

        var result = await test.Manager.TrackNewRemoteSave(test.Save);

        Assert.Same(test.Save, result);
        await test.RomM.Server.DidNotReceiveWithAnyArgs().RawGETAsync(default!, default);
    }

    [Fact]
    public async Task TrackNewRemoteSave_WhenMappingIsMissing_DoesNotFetchSave()
    {
        using var test = new SaveManagerTestHarness();
        test.ROM.MappingID = Guid.NewGuid();

        var result = await test.Manager.TrackNewRemoteSave(test.Save);

        Assert.Same(test.Save, result);
        await test.RomM.Server.DidNotReceiveWithAnyArgs().RawGETAsync(default!, default);
    }

    [Fact]
    public async Task TrackNewRemoteSave_WhenUserDeclinesReplacement_KeepsPreviousTrackedSave()
    {
        using var test = new SaveManagerTestHarness();
        var path = test.WriteLocalSave(new byte[] { 0x10, 0x20 });
        var remote = new GravitonSave
        {
            ROMID = 42,
            SaveID = 1201,
            Filename = "incoming.sav",
            Slot = "autosave"
        };
        test.Playnite.Dialogs.EnqueueMessageResponse(MessageBoxResult.No);

        var result = await test.Manager.TrackNewRemoteSave(remote);

        Assert.Same(test.Save, result);
        Assert.Same(test.Save, test.ROM.LocalSave);
        Assert.Equal(new byte[] { 0x10, 0x20 }, File.ReadAllBytes(path));
        await test.RomM.Server.DidNotReceiveWithAnyArgs().RawGETAsync(default!, default);
        await test.RomM.Server.DidNotReceiveWithAnyArgs().POSTAsync(default!, default(object)!, default);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task TrackNewRemoteSave_WhenDownloadFails_DoesNotReplacePreviouslyTrackedSave(HttpStatusCode statusCode)
    {
        using var test = new SaveManagerTestHarness();
        var path = test.WriteLocalSave(new byte[] { 0x33 });
        test.ROM.LocalSave = null;
        test.RomM.ConfigureRawGETResponse(test.DownloadPath, Array.Empty<byte>(), statusCode);

        var result = await test.Manager.TrackNewRemoteSave(test.Save);

        Assert.Same(test.Save, result);
        Assert.Null(test.ROM.LocalSave);
        Assert.Equal(new byte[] { 0x33 }, File.ReadAllBytes(path));
        await test.RomM.Server.DidNotReceiveWithAnyArgs().POSTAsync(default!, default(object)!, default);
    }

    [Fact]
    public async Task TrackNewRemoteSave_WhenNoLocalCopyExists_WritesTheSelectedDestination()
    {
        using var test = new SaveManagerTestHarness();
        test.ROM.LocalSave = null;
        var bytes = new byte[] { 0x11, 0x22, 0x33 };
        test.Save.ServerHash = "remote-hash";
        test.RomM.ConfigureRawGETResponse(test.DownloadPath, bytes);
        test.RomM.ConfigureJsonPOSTResponse("/api/saves/987/downloaded");

        var result = await test.Manager.TrackNewRemoteSave(test.Save);

        Assert.Same(test.Save, result);
        Assert.Same(result, test.ROM.LocalSave);
        Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(test.SavesDirectory, "autosave.sav")));
        Assert.Equal(SaveStatus.Synced, result.Status);
        Assert.Equal("remote-hash", result.LastSyncedContentHash);
        Assert.Equal(result.ServerLastUpdatedAt, result.LastSyncedAt);
        await test.RomM.Server.Received(1).POSTAsync("/api/saves/987/downloaded", Arg.Any<object>(), false);
    }

    [Fact]
    public async Task TrackNewRemoteSave_WhenReplacingPreviousSave_UntracksOldRecordAndDownloadsNewOne()
    {
        using var test = new SaveManagerTestHarness();
        var path = test.WriteLocalSave(new byte[] { 0x01 });
        var incoming = new GravitonSave
        {
            ROMID = 42,
            SaveID = 1202,
            Filename = "autosave.sav",
            Slot = "autosave",
            ServerHash = "replacement-hash",
            ServerLastUpdatedAt = new DateTime(2026, 10, 11, 10, 0, 0, DateTimeKind.Utc),
            SourceFilePaths = new ObservableCollection<string>(test.Save.SourceFilePaths)
        };
        test.Playnite.Dialogs.EnqueueMessageResponse(MessageBoxResult.Yes);
        test.RomM.ConfigureJsonPOSTResponse("/api/saves/987/untrack");
        test.RomM.ConfigureRawGETResponse("/api/saves/1202/content?device_id=test-device&optimistic=false", new byte[] { 0x45, 0x46 });
        test.RomM.ConfigureJsonPOSTResponse("/api/saves/1202/downloaded");

        var result = await test.Manager.TrackNewRemoteSave(incoming);

        Assert.Same(incoming, result);
        Assert.Same(incoming, test.ROM.LocalSave);
        Assert.Equal(new byte[] { 0x45, 0x46 }, File.ReadAllBytes(path));
        Assert.Equal(SaveStatus.Synced, incoming.Status);
        await test.RomM.Server.Received(1).POSTAsync("/api/saves/987/untrack", Arg.Any<object>(), false);
        await test.RomM.Server.Received(1).POSTAsync("/api/saves/1202/downloaded", Arg.Any<object>(), false);
    }

    [Fact]
    public async Task DownloadArchivedSave_WhenROMIsUnknown_ReturnsNoSave()
    {
        using var test = new SaveManagerTestHarness();
        var archived = new Graviton.Models.RomM.Saves.RomMSave { ID = 1200, ROMID = 999 };

        var result = await test.Manager.DownloadArchivedSave(archived);

        Assert.Null(result);
        await test.RomM.Server.DidNotReceiveWithAnyArgs().RawGETAsync(default!, default);
    }

    [Fact]
    public async Task DownloadArchivedSave_WhenUserDeclinesReplacement_KeepsCurrentSave()
    {
        using var test = new SaveManagerTestHarness();
        var path = test.WriteLocalSave(new byte[] { 0x12 });
        var archived = new Graviton.Models.RomM.Saves.RomMSave { ID = 1200, ROMID = 42 };
        test.Playnite.Dialogs.EnqueueMessageResponse(MessageBoxResult.No);

        var result = await test.Manager.DownloadArchivedSave(archived);

        Assert.Null(result);
        Assert.Same(test.Save, test.ROM.LocalSave);
        Assert.Equal(new byte[] { 0x12 }, File.ReadAllBytes(path));
        await test.RomM.Server.DidNotReceiveWithAnyArgs().RawGETAsync(default!, default);
    }

    [Fact]
    public async Task DownloadArchivedSave_WhenMappingIsMissing_DoesNotFetchContent()
    {
        using var test = new SaveManagerTestHarness();
        test.ROM.LocalSave = null;
        test.ROM.MappingID = Guid.NewGuid();
        var archived = new Graviton.Models.RomM.Saves.RomMSave { ID = 1200, ROMID = 42 };

        var result = await test.Manager.DownloadArchivedSave(archived);

        Assert.Null(result);
        await test.RomM.Server.DidNotReceiveWithAnyArgs().RawGETAsync(default!, default);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task DownloadArchivedSave_WhenServerRejectsRequest_DoesNotChangeExistingFiles(HttpStatusCode statusCode)
    {
        using var test = new SaveManagerTestHarness();
        test.ROM.LocalSave = null;
        var path = test.WriteLocalSave(new byte[] { 0x7A });
        var archived = new Graviton.Models.RomM.Saves.RomMSave { ID = 1200, ROMID = 42, FileName = "autosave.sav" };
        test.RomM.ConfigureRawGETResponse("/api/saves/1200/content?device_id=test-device&optimistic=false", Array.Empty<byte>(), statusCode);

        var result = await test.Manager.DownloadArchivedSave(archived);

        Assert.Null(result);
        Assert.Equal(new byte[] { 0x7A }, File.ReadAllBytes(path));
        await test.RomM.Server.DidNotReceiveWithAnyArgs().POSTAsync(default!, default(object)!, default);
    }
}
