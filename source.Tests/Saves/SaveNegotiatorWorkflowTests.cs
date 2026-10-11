using Graviton.Models.RomM.Saves;
using Graviton.Models.Saves;
using Graviton.Notifications;
using Graviton.Saves;
using Graviton.Tests.Fakes.Saves;

using NSubstitute;

using Playnite;

using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Saves;

[Collection("SaveManagerTransferTests")]
public sealed class SaveNegotiatorWorkflowTests
{
    [Fact]
    public async Task NegotiateSave_WhenNoSaveIsTracked_DoesNotContactRomM()
    {
        using var test = new SaveManagerTestHarness();
        test.ROM.LocalSave = null;

        await CreateNegotiator(test).NegotiateSave(test.ROM);

        await test.RomM.Server.DidNotReceiveWithAnyArgs().POSTAsync(default!, default(object)!, default);
    }

    [Fact]
    public async Task NegotiateSave_WhenTheLocalFileIsMissing_DoesNotCreateASession()
    {
        using var test = new SaveManagerTestHarness();

        await CreateNegotiator(test).NegotiateSave(test.ROM);

        Assert.Equal(SaveStatus.MissingFiles, test.Save.Status);
        Assert.Single(test.Save.MissingFiles);
        await test.RomM.Server.DidNotReceiveWithAnyArgs().POSTAsync(default!, default(object)!, default);
    }

    [Fact]
    public async Task NegotiateSave_WhenRomMDoesNotRespond_LeavesTheSavePending()
    {
        using var test = new SaveManagerTestHarness();
        test.WriteLocalSave(new byte[] { 1, 2, 3 });
        test.Save.IsTempRestored = true;
        test.RomM.ConfigureJsonPOSTResponse("/api/sync/negotiate", null);

        await CreateNegotiator(test).NegotiateSave(test.ROM);

        Assert.Equal(SaveStatus.RemoteNewer, test.Save.Status);
        Assert.True(test.Save.IsTempRestored);
        await test.RomM.Server.DidNotReceive().POSTAsync(
            Arg.Is<string>(path => path.Contains("/complete")), Arg.Any<object>(), false);
    }

    [Fact]
    public async Task NegotiateSave_WhenResponseHasNoMatchingOperation_ReportsFailure()
    {
        using var test = new SaveManagerTestHarness();
        test.WriteLocalSave(new byte[] { 1, 2, 3 });
        test.Save.ServerHash = "old-server-hash";
        ConfigureSession(test, "no_op", romID: 999);

        await CreateNegotiator(test).NegotiateSave(test.ROM);

        Assert.Equal(SaveStatus.Unknown, test.Save.Status);
        Assert.Null(test.Save.ServerHash);
        await AssertCompletion(test, 0, 1);
    }

    [Fact]
    public async Task NegotiateSave_WhenRomMReportsNoOp_ClearsTemporaryRestoreAndCompletes()
    {
        using var test = new SaveManagerTestHarness();
        var original = new byte[] { 4, 5, 6 };
        var path = test.WriteLocalSave(original);
        test.Save.IsTempRestored = true;
        ConfigureSession(test, "no_op", serverHash: Hash(original));

        await CreateNegotiator(test).NegotiateSave(test.ROM);

        Assert.Equal(SaveStatus.Synced, test.Save.Status);
        Assert.False(test.Save.IsTempRestored);
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Equal(Hash(original), test.Save.ServerHash);
        Assert.True(File.Exists(Path.Combine(test.Plugin.Context.PluginDataPath, "Games", "42.json")));
        await AssertCompletion(test, 1, 0);
    }

    [Fact]
    public async Task NegotiateSave_WhenUploadSucceeds_ReportsCompletionAndPreservesLocalFile()
    {
        using var test = new SaveManagerTestHarness();
        var original = new byte[] { 7, 8, 9, 10 };
        var path = test.WriteLocalSave(original);
        test.Save.IsTempRestored = true;
        ConfigureSession(test, "upload");
        test.RomM.ConfigureRawPOSTResponse(test.UploadPath(), Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new RomMSave
        {
            ID = 987,
            ROMID = 42,
            Slot = "autosave",
            FileName = "autosave.sav",
            ContentHash = Hash(original),
            FileSize = original.Length,
            UpdatedAt = "2026-10-10T13:00:00Z"
        })));

        await CreateNegotiator(test).NegotiateSave(test.ROM);

        Assert.Equal(SaveStatus.Synced, test.Save.Status);
        Assert.False(test.Save.IsTempRestored);
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Equal(Hash(original), test.Save.LastSyncedContentHash);
        await test.RomM.Server.Received(1).RawPOSTAsync(test.UploadPath(), Arg.Any<System.Net.Http.HttpContent>(), false);
        await AssertCompletion(test, 1, 0);
    }

    [Fact]
    public async Task NegotiateSave_WhenUploadIsRejected_ReportsFailureAndRetainsTemporaryRestore()
    {
        using var test = new SaveManagerTestHarness();
        var original = new byte[] { 11, 12, 13 };
        var path = test.WriteLocalSave(original);
        test.Save.IsTempRestored = true;
        ConfigureSession(test, "upload");
        test.RomM.ConfigureRawPOSTResponse(test.UploadPath(), Array.Empty<byte>(), HttpStatusCode.BadRequest);

        await CreateNegotiator(test).NegotiateSave(test.ROM);

        Assert.Equal(SaveStatus.LocalNewer, test.Save.Status);
        Assert.True(test.Save.IsTempRestored);
        Assert.Equal(original, File.ReadAllBytes(path));
        await AssertCompletion(test, 0, 1);
    }

    [Fact]
    public async Task NegotiateSave_WhenDownloadSucceeds_ReportsCompletionAndReplacesOnlyTrackedFile()
    {
        using var test = new SaveManagerTestHarness();
        var original = new byte[] { 14, 15 };
        var downloaded = new byte[] { 16, 17, 18, 19 };
        var path = test.WriteLocalSave(original);
        var unrelatedPath = test.WriteLocalSave(new byte[] { 99 }, "unrelated.sav");
        test.Save.IsTempRestored = true;
        ConfigureSession(test, "download", saveID: 912, serverHash: Hash(downloaded));
        test.RomM.ConfigureRawGETResponse("/api/saves/912/content?device_id=test-device&optimistic=false", downloaded);
        test.RomM.ConfigureJsonPOSTResponse("/api/saves/912/downloaded");

        await CreateNegotiator(test).NegotiateSave(test.ROM);

        Assert.Equal(912, test.Save.SaveID);
        Assert.Equal(SaveStatus.Synced, test.Save.Status);
        Assert.False(test.Save.IsTempRestored);
        Assert.Equal(Hash(downloaded), test.Save.LastSyncedContentHash);
        Assert.Equal(downloaded, File.ReadAllBytes(path));
        Assert.Equal(new byte[] { 99 }, File.ReadAllBytes(unrelatedPath));
        await AssertCompletion(test, 1, 0);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task NegotiateSave_WhenDownloadIsRejected_DoesNotCountItAsCompleted(HttpStatusCode status)
    {
        using var test = new SaveManagerTestHarness();
        var original = new byte[] { 20, 21, 22 };
        var path = test.WriteLocalSave(original);
        test.Save.IsTempRestored = true;
        ConfigureSession(test, "download", saveID: 913, serverHash: "new-server-hash");
        test.RomM.ConfigureRawGETResponse("/api/saves/913/content?device_id=test-device&optimistic=false", new byte[] { 23 }, status);

        await CreateNegotiator(test).NegotiateSave(test.ROM);

        Assert.Equal(SaveStatus.RemoteNewer, test.Save.Status);
        Assert.True(test.Save.IsTempRestored);
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Equal("previous-synced-hash", test.Save.LastSyncedContentHash);
        await test.RomM.Server.DidNotReceive().POSTAsync("/api/saves/913/downloaded", Arg.Any<object>(), false);
        await AssertCompletion(test, 0, 1);
    }

    [Fact]
    public async Task NegotiateSave_WhenDownloadHashDoesNotMatch_DoesNotCountItAsCompleted()
    {
        using var test = new SaveManagerTestHarness();
        var original = new byte[] { 24, 25 };
        var path = test.WriteLocalSave(original);
        test.Save.IsTempRestored = true;
        ConfigureSession(test, "download", saveID: 914, serverHash: new string('a', 32));
        test.RomM.ConfigureRawGETResponse("/api/saves/914/content?device_id=test-device&optimistic=false", new byte[] { 26, 27 });

        await CreateNegotiator(test).NegotiateSave(test.ROM);

        Assert.Equal(SaveStatus.RemoteNewer, test.Save.Status);
        Assert.True(test.Save.IsTempRestored);
        Assert.Equal(original, File.ReadAllBytes(path));
        await AssertCompletion(test, 0, 1);
    }

    [Fact]
    public async Task NegotiateSave_WhenConflictHasNoServerTimestamp_DoesNotOpenAWindow()
    {
        using var test = new SaveManagerTestHarness();
        test.WriteLocalSave(new byte[] { 28, 29 });
        ConfigureSession(test, "conflict", serverHash: "conflicting-hash", serverUpdatedAt: null);

        await CreateNegotiator(test).NegotiateSave(test.ROM);

        Assert.Equal(SaveStatus.Conflicted, test.Save.Status);
        _ = test.Playnite.Api.DidNotReceiveWithAnyArgs().CreateWindow(default!);
        await AssertCompletion(test, 0, 1);
    }

    [Fact]
    public async Task NegotiateSave_WhenServerDeletedSaveIsCancelled_PreservesLocalData()
    {
        using var test = new SaveManagerTestHarness();
        var original = new byte[] { 30, 31 };
        var path = test.WriteLocalSave(original);
        test.Playnite.Dialogs.EnqueueCustomMessageResponse(new MessageBoxResponse("Cancel", isCancel: true));
        ConfigureSession(test, "delete");

        await CreateNegotiator(test).NegotiateSave(test.ROM);

        Assert.Equal(SaveStatus.ServerDeleted, test.Save.Status);
        Assert.Equal(original, File.ReadAllBytes(path));
        await test.RomM.Server.DidNotReceiveWithAnyArgs().RawPOSTAsync(default!, default(System.Net.Http.HttpContent)!, default);
        await AssertCompletion(test, 0, 1);
    }

    [Fact]
    public async Task SoftNegotiateSaves_WhenThereIsNoTrackedSave_DoesNotContactRomM()
    {
        using var test = new SaveManagerTestHarness();
        test.ROM.LocalSave = null;

        var result = await CreateNegotiator(test).SoftNegotiateSaves(new() { test.ROM });

        Assert.Null(result);
        await test.RomM.Server.DidNotReceiveWithAnyArgs().POSTAsync(default!, default(object)!, default);
    }

    [Fact]
    public async Task SoftNegotiateSaves_WhenServerDoesNotRespond_MarksStatusUnknown()
    {
        using var test = new SaveManagerTestHarness();
        test.WriteLocalSave(new byte[] { 32, 33 });
        test.Save.ServerHash = "old-server-hash";
        test.RomM.ConfigureJsonPOSTResponse("/api/sync/negotiate", null);

        var result = await CreateNegotiator(test).SoftNegotiateSaves(new() { test.ROM });

        Assert.NotNull(result);
        Assert.Same(test.ROM, Assert.Single(result));
        Assert.Equal(SaveStatus.Unknown, test.Save.Status);
        Assert.Null(test.Save.ServerHash);
    }

    [Fact]
    public async Task SoftNegotiateSaves_WhenTheServerReportsNewerData_UpdatesStatusWithoutDownloading()
    {
        using var test = new SaveManagerTestHarness();
        var original = new byte[] { 34, 35 };
        var path = test.WriteLocalSave(original);
        ConfigureSession(test, "download", saveID: 915, serverHash: "server-newer-hash");

        var result = await CreateNegotiator(test).SoftNegotiateSaves(new() { test.ROM });

        Assert.NotNull(result);
        Assert.Equal(SaveStatus.RemoteNewer, test.Save.Status);
        Assert.Equal("server-newer-hash", test.Save.ServerHash);
        Assert.Equal(original, File.ReadAllBytes(path));
        await test.RomM.Server.DidNotReceiveWithAnyArgs().RawGETAsync(default!, default);
        await test.RomM.Server.DidNotReceive().POSTAsync("/api/sync/sessions/81/complete", Arg.Any<object>(), false);
    }

    [Fact]
    public async Task SoftNegotiateSaves_WhenTheResponseOnlyContainsAnotherROM_DoesNotUseItsOperation()
    {
        using var test = new SaveManagerTestHarness();
        test.WriteLocalSave(new byte[] { 36, 37 });
        test.Save.ServerHash = "old-server-hash";
        ConfigureSession(test, "no_op", romID: 999, serverHash: "unrelated-hash");

        var result = await CreateNegotiator(test).SoftNegotiateSaves(new() { test.ROM });

        Assert.NotNull(result);
        Assert.Equal(SaveStatus.Unknown, test.Save.Status);
        Assert.Null(test.Save.ServerHash);
    }

    private static SaveNegotiator CreateNegotiator(SaveManagerTestHarness test)
    {
        var controller = new SaveController(test.Plugin.Context, test.Playnite.Api,
            new GravitonLogger(test.Plugin.Settings), test.RomM.Server);
        test.Plugin.Context.SaveController.Returns(controller);
        return controller.Negotiator;
    }

    private static void ConfigureSession(SaveManagerTestHarness test, string action, int romID = 42,
        int? saveID = 987, string? serverHash = null, string? serverUpdatedAt = "2026-10-10T12:00:00Z")
    {
        var response = JsonSerializer.Serialize(new
        {
            session_id = 81,
            operations = new[]
            {
                new
                {
                    action,
                    rom_id = romID,
                    save_id = saveID,
                    slot = "autosave",
                    file_name = "autosave.sav",
                    server_content_hash = serverHash,
                    server_updated_at = serverUpdatedAt
                }
            }
        });
        test.RomM.ConfigureJsonPOSTResponse("/api/sync/negotiate", response);
        test.RomM.ConfigureJsonPOSTResponse("/api/sync/sessions/81/complete", "{}");
    }

    private static async Task AssertCompletion(SaveManagerTestHarness test, int completed, int failed)
    {
        await test.RomM.Server.Received(1).POSTAsync(
            "/api/sync/sessions/81/complete",
            Arg.Is<object>(value => HasCompletionCount(value, completed, failed)),
            false);
    }

    private static bool HasCompletionCount(object value, int completed, int failed)
    {
        var json = JsonSerializer.SerializeToElement(value);
        return json.GetProperty("operations_completed").GetInt32() == completed &&
               json.GetProperty("operations_failed").GetInt32() == failed;
    }

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
    }
}
