using Graviton.Models.RomM.Saves;
using Graviton.Models.Saves;
using Graviton.Notifications;
using Graviton.Saves;
using Graviton.Tests.Fakes.Saves;

using NSubstitute;

using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Saves;

[Collection("SaveManagerTransferTests")]
public sealed class SaveManagerRestoredSaveTests
{
    [Fact]
    public async Task CheckRestoredSaveNeedUploading_WithNoTrackedSave_DoesNotUpload()
    {
        using var test = new SaveManagerTestHarness();
        test.ROM.LocalSave = null;

        await test.Manager.CheckRestoredSaveNeedUploading(test.ROM);

        await test.RomM.Server.DidNotReceiveWithAnyArgs().RawPOSTAsync(default!, default(HttpContent)!, default);
    }

    [Fact]
    public async Task CheckRestoredSaveNeedUploading_WhenFileIsStillMissing_KeepsRestorePending()
    {
        using var test = new SaveManagerTestHarness();
        ConfigureController(test);
        test.Save.IsTempRestored = true;
        test.Save.Status = SaveStatus.MissingFiles;

        await test.Manager.CheckRestoredSaveNeedUploading(test.ROM);

        Assert.True(test.Save.IsTempRestored);
        Assert.Equal(SaveStatus.MissingFiles, test.Save.Status);
        await test.RomM.Server.DidNotReceiveWithAnyArgs().RawPOSTAsync(default!, default(HttpContent)!, default);
    }

    [Fact]
    public async Task CheckRestoredSaveNeedUploading_WhenContentMatchesSyncedHash_ClearsRestoreFlagWithoutUpload()
    {
        using var test = new SaveManagerTestHarness();
        ConfigureController(test);
        var bytes = new byte[] { 0x12, 0x34, 0x56 };
        test.WriteLocalSave(bytes);
        test.Save.IsTempRestored = true;
        test.Save.LastSyncedContentHash = Hash(bytes);

        await test.Manager.CheckRestoredSaveNeedUploading(test.ROM);

        Assert.False(test.Save.IsTempRestored);
        await test.RomM.Server.DidNotReceiveWithAnyArgs().RawPOSTAsync(default!, default(HttpContent)!, default);
    }

    [Fact]
    public async Task CheckRestoredSaveNeedUploading_WhenContentDiffers_UploadsAndClearsRestoreFlag()
    {
        using var test = new SaveManagerTestHarness();
        ConfigureController(test);
        test.WriteLocalSave(new byte[] { 0xAA, 0xBB });
        test.Save.IsTempRestored = true;
        test.Save.Status = SaveStatus.LocalNewer;
        test.Save.LastSyncedContentHash = "old-hash";
        test.RomM.ConfigureRawPOSTResponse(test.UploadPath(), JsonSerializer.SerializeToUtf8Bytes(new RomMSave
        {
            ID = 987,
            ROMID = 42,
            FileName = "autosave.sav",
            UpdatedAt = "2026-10-10T12:00:00Z",
            ContentHash = "new-hash",
            FileSize = 2
        }));

        await test.Manager.CheckRestoredSaveNeedUploading(test.ROM);

        Assert.False(test.Save.IsTempRestored);
        Assert.Equal(SaveStatus.Synced, test.Save.Status);
        Assert.Equal("new-hash", test.Save.LastSyncedContentHash);
        await test.RomM.Server.Received(1).RawPOSTAsync(test.UploadPath(), Arg.Any<HttpContent>(), false);
    }

    [Fact]
    public async Task CheckRestoredSaveNeedUploading_WhenUploadFails_KeepsRestoreFlag()
    {
        using var test = new SaveManagerTestHarness();
        ConfigureController(test);
        test.WriteLocalSave(new byte[] { 0xAA, 0xBB });
        test.Save.IsTempRestored = true;
        test.Save.Status = SaveStatus.LocalNewer;
        test.Save.LastSyncedContentHash = "old-hash";
        test.RomM.ConfigureRawPOSTResponse(test.UploadPath(), Array.Empty<byte>(), HttpStatusCode.ServiceUnavailable);

        await test.Manager.CheckRestoredSaveNeedUploading(test.ROM);

        Assert.True(test.Save.IsTempRestored);
        Assert.Equal(SaveStatus.LocalNewer, test.Save.Status);
        await test.RomM.Server.Received(1).RawPOSTAsync(test.UploadPath(), Arg.Any<HttpContent>(), false);
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
