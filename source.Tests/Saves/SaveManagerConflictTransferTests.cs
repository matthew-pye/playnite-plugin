using Graviton.Models.RomM;
using Graviton.Models.Saves;
using Graviton.Notifications;
using Graviton.Saves;
using Graviton.Tests.Fakes.Saves;

using NSubstitute;

using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;

using Xunit;

namespace Graviton.Tests.Saves;

[Collection("SaveManagerTransferTests")]
public sealed class SaveManagerConflictTransferTests
{
    [Fact]
    public async Task Upload_WhenLocalContentMatchesPreviousSync_DownloadsRemoteCopyOnConflict()
    {
        using var test = new SaveManagerTestHarness();
        ConfigureController(test);
        var local = new byte[] { 0x01, 0x02, 0x03 };
        var remote = new byte[] { 0x04, 0x05, 0x06 };
        var path = test.WriteLocalSave(local);
        test.Save.LastSyncedContentHash = Hash(local);
        test.Save.Status = SaveStatus.LocalNewer;
        test.RomM.ConfigureRawPOSTResponse(test.UploadPath(), Array.Empty<byte>(), HttpStatusCode.Conflict);
        test.RomM.ConfigureRawGETResponse(test.DownloadPath, remote);
        test.RomM.ConfigureJsonPOSTResponse("/api/saves/987/downloaded");

        var result = await test.Manager.Upload(test.Save, operation: Operation(Hash(remote)));

        Assert.Same(test.Save, result);
        Assert.Equal(remote, File.ReadAllBytes(path));
        Assert.Equal(SaveStatus.Synced, result.Status);
        Assert.Equal(Hash(remote), result.LastSyncedContentHash);
        await test.RomM.Server.Received(1).RawPOSTAsync(test.UploadPath(), Arg.Any<HttpContent>(), false);
        await test.RomM.Server.Received(1).RawGETAsync(test.DownloadPath, false);
        await test.RomM.Server.Received(1).POSTAsync("/api/saves/987/downloaded", Arg.Any<object>(), false);
    }

    [Fact]
    public async Task Upload_WhenLocalContentMatchesServerHead_DownloadsWithoutOverwritingServer()
    {
        using var test = new SaveManagerTestHarness();
        ConfigureController(test);
        var bytes = new byte[] { 0x7A, 0x7B };
        var path = test.WriteLocalSave(bytes);
        test.Save.LastSyncedContentHash = "different-previous-hash";
        test.RomM.ConfigureRawPOSTResponse(test.UploadPath(), Array.Empty<byte>(), HttpStatusCode.Conflict);
        test.RomM.ConfigureRawGETResponse(test.DownloadPath, bytes);
        test.RomM.ConfigureJsonPOSTResponse("/api/saves/987/downloaded");

        await test.Manager.Upload(test.Save, operation: Operation(Hash(bytes)));

        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Equal(SaveStatus.Synced, test.Save.Status);
        await test.RomM.Server.DidNotReceive().RawPOSTAsync(test.UploadPath(overwrite: true), Arg.Any<HttpContent>(), false);
    }

    [Fact]
    public async Task Upload_WhenBothCopiesDifferAndConflictCannotBeResolved_PreservesLocalFile()
    {
        using var test = new SaveManagerTestHarness();
        ConfigureController(test);
        var bytes = new byte[] { 0x11, 0x22 };
        var path = test.WriteLocalSave(bytes);
        test.Save.LastSyncedContentHash = "previous-hash";
        test.Save.ServerLastUpdatedAt = null;
        test.RomM.ConfigureRawPOSTResponse(test.UploadPath(), Array.Empty<byte>(), HttpStatusCode.Conflict);
        var operation = Operation("unrelated-server-hash");
        operation.ServerUpdatedAt = null;

        var result = await test.Manager.Upload(test.Save, operation: operation);

        Assert.Same(test.Save, result);
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Equal(SaveStatus.Conflicted, result.Status);
        await test.RomM.Server.DidNotReceiveWithAnyArgs().RawGETAsync(default!, default);
        await test.RomM.Server.DidNotReceive().RawPOSTAsync(test.UploadPath(overwrite: true), Arg.Any<HttpContent>(), false);
    }

    private static RomMNegotiateOperations Operation(string serverHash)
    {
        return new RomMNegotiateOperations
        {
            ROMID = 42,
            Slot = "autosave",
            SaveID = 987,
            ServerContentHash = serverHash,
            ServerUpdatedAt = "2026-10-10T12:00:00Z"
        };
    }

    private static void ConfigureController(SaveManagerTestHarness test)
    {
        var controller = new SaveController(test.Plugin.Context, test.Playnite.Api, new GravitonLogger(test.Plugin.Settings), test.RomM.Server);
        test.Plugin.Context.SaveController.Returns(controller);
    }

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
    }
}
