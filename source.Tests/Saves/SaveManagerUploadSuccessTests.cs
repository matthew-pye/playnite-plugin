using Graviton.Models;
using Graviton.Models.RomM.Saves;
using Graviton.Models.Saves;
using Graviton.Tests.Fakes.Saves;

using NSubstitute;

using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Saves;

[Collection("SaveManagerTransferTests")]
public sealed class SaveManagerUploadSuccessTests
{
    [Fact]
    public async Task Upload_WhenAccepted_UpdatesSyncMetadataAndKeepsPreviousVersion()
    {
        using var test = new SaveManagerTestHarness();
        var bytes = new byte[] { 0x00, 0xFF, 0x21, 0x42 };
        var path = test.WriteLocalSave(bytes);
        test.Save.Status = SaveStatus.LocalNewer;
        test.Save.MissingFiles = new List<string> { "old-missing.sav" };
        test.RomM.ConfigureRawPOSTResponse(test.UploadPath(), Response(1201, "server-hash", bytes.Length));
        test.RomM.ConfigureJsonPOSTResponse("/api/saves/987/untrack");

        var result = await test.Manager.Upload(test.Save);

        Assert.Same(test.Save, result);
        Assert.Same(result, test.ROM.LocalSave);
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Equal(1201, result.SaveID);
        Assert.Equal(SaveStatus.Synced, result.Status);
        Assert.True(result.IsCurrent);
        Assert.Equal("server-hash", result.ContentHash);
        Assert.Equal("server-hash", result.ServerHash);
        Assert.Equal("server-hash", result.LastSyncedContentHash);
        Assert.Equal(bytes.Length, result.FileSize);
        Assert.Equal(new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc), result.LastSyncedAt?.ToUniversalTime());
        Assert.Equal(result.LastSyncedAt, result.ServerLastUpdatedAt);
        Assert.Empty(result.MissingFiles);
        var previous = Assert.Single(Assert.IsType<ObservableCollection<GravitonSave>>(result.HistoricSaves));
        Assert.NotSame(result, previous);
        Assert.Equal(987, previous.SaveID);
        Assert.Equal("previous-synced-hash", previous.LastSyncedContentHash);
        Assert.True(previous.IsHistoric);
        Assert.False(previous.IsCurrent);
        await test.RomM.Server.Received(1).POSTAsync("/api/saves/987/untrack", Arg.Any<object>(), false);
    }

    [Fact]
    public async Task TrackNewLocalSave_WhenFirstFileAppears_UploadsWithoutAnExistingServerRecord()
    {
        using var test = new SaveManagerTestHarness();
        test.Save.SaveID = -1;
        test.Save.Status = SaveStatus.UntrackedLocal;

        var pendingResult = await test.Manager.TrackNewLocalSave(test.Save);

        Assert.Same(test.Save, pendingResult);
        Assert.Equal(-1, test.Save.SaveID);
        Assert.Equal(SaveStatus.UntrackedLocal, test.Save.Status);
        await test.RomM.Server.DidNotReceiveWithAnyArgs().RawPOSTAsync(default!, default(HttpContent)!, default);

        var path = test.WriteLocalSave(new byte[] { 0x40, 0x50, 0x60 });
        test.RomM.ConfigureRawPOSTResponse(test.UploadPath(), Response(1202, "first-save-hash", 3));

        var uploaded = await test.Manager.TrackNewLocalSave(test.Save);

        Assert.Same(test.Save, uploaded);
        Assert.Equal(1202, uploaded.SaveID);
        Assert.Equal(SaveStatus.Synced, uploaded.Status);
        Assert.Equal(new byte[] { 0x40, 0x50, 0x60 }, File.ReadAllBytes(path));
        await test.RomM.Server.Received(1).RawPOSTAsync(test.UploadPath(), Arg.Any<HttpContent>(), false);
        await test.RomM.Server.DidNotReceiveWithAnyArgs().POSTAsync(default!, default(object)!, default);
    }

    [Fact]
    public async Task Upload_WhenServerReusesSaveID_DoesNotUntrackTheCurrentRecord()
    {
        using var test = new SaveManagerTestHarness();
        test.WriteLocalSave(new byte[] { 0x30, 0x31 });
        test.RomM.ConfigureRawPOSTResponse(test.UploadPath(), Response(987, "same-id-hash", 2));

        await test.Manager.Upload(test.Save);

        Assert.Equal(987, test.Save.SaveID);
        Assert.Equal(SaveStatus.Synced, test.Save.Status);
        await test.RomM.Server.DidNotReceiveWithAnyArgs().POSTAsync(default!, default(object)!, default);
    }

    [Fact]
    public async Task Upload_WhenHistoricVersionAlreadyExists_DoesNotDuplicateThatVersion()
    {
        using var test = new SaveManagerTestHarness();
        test.WriteLocalSave(new byte[] { 0x10, 0x11 });
        var existing = new GravitonSave { SaveID = 987, IsHistoric = true };
        test.Save.HistoricSaves = new ObservableCollection<GravitonSave> { existing };
        test.RomM.ConfigureRawPOSTResponse(test.UploadPath(), Response(1203, "new-hash", 2));
        test.RomM.ConfigureJsonPOSTResponse("/api/saves/987/untrack");

        await test.Manager.Upload(test.Save);

        Assert.Same(existing, Assert.Single(test.Save.HistoricSaves!));
        Assert.Equal(1203, test.Save.SaveID);
    }

    [Fact]
    public async Task Upload_WhenHistoryIsNull_CreatesHistoryBeforeReplacingServerRecord()
    {
        using var test = new SaveManagerTestHarness();
        test.WriteLocalSave(new byte[] { 0x50 });
        test.Save.HistoricSaves = null;
        test.RomM.ConfigureRawPOSTResponse(test.UploadPath(), Response(1204, "new-hash", 1));
        test.RomM.ConfigureJsonPOSTResponse("/api/saves/987/untrack");

        await test.Manager.Upload(test.Save);

        Assert.Equal(987, Assert.Single(test.Save.HistoricSaves!).SaveID);
        Assert.Equal(SaveStatus.Synced, test.Save.Status);
    }

    [Fact]
    public async Task Upload_WithScreenshot_SendsImageToNewServerSave()
    {
        using var test = new SaveManagerTestHarness();
        test.WriteLocalSave(new byte[] { 0x02, 0x03 });
        test.RomM.ConfigureRawPOSTResponse(test.UploadPath(), Response(987, "hash", 2, "game [autosave].sav"));
        test.RomM.ConfigureJsonPUTResponse("/api/saves/987?device_id=test-device");

        await test.Manager.Upload(test.Save, screenshot: new byte[] { 0xFF, 0xD8, 0xFF });

        await test.RomM.Server.Received(1).PUTAsync(
            "/api/saves/987?device_id=test-device",
            Arg.Is<HttpContent>(content => content is MultipartFormDataContent),
            false);
        Assert.Equal(SaveStatus.Synced, test.Save.Status);
    }

    [Fact]
    public async Task Upload_WithoutScreenshot_DoesNotUpdateServerScreenshot()
    {
        using var test = new SaveManagerTestHarness();
        test.WriteLocalSave(new byte[] { 0x02 });
        test.RomM.ConfigureRawPOSTResponse(test.UploadPath(), Response(987, "hash", 1));

        await test.Manager.Upload(test.Save);

        await test.RomM.Server.DidNotReceiveWithAnyArgs().PUTAsync(default!, default(HttpContent)!, default);
    }

    [Fact]
    public async Task Upload_WithScreenshotAndNoServerFilename_DoesNotUpdateServerScreenshot()
    {
        using var test = new SaveManagerTestHarness();
        test.WriteLocalSave(new byte[] { 0x02 });
        test.RomM.ConfigureRawPOSTResponse(test.UploadPath(), Response(987, "hash", 1, null));

        await test.Manager.Upload(test.Save, screenshot: new byte[] { 0xFF, 0xD8 });

        await test.RomM.Server.DidNotReceiveWithAnyArgs().PUTAsync(default!, default(HttpContent)!, default);
        Assert.Equal(SaveStatus.Synced, test.Save.Status);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{invalid-json")]
    public async Task Upload_WithUnusableResponse_DoesNotReplaceTrackedSave(string response)
    {
        using var test = new SaveManagerTestHarness();
        var bytes = new byte[] { 0x90, 0x91 };
        var path = test.WriteLocalSave(bytes);
        test.Save.Status = SaveStatus.LocalNewer;
        test.RomM.ConfigureRawPOSTResponse(test.UploadPath(), Encoding.UTF8.GetBytes(response));

        var result = await test.Manager.Upload(test.Save);

        Assert.Same(test.Save, result);
        Assert.Equal(987, result.SaveID);
        Assert.Equal(SaveStatus.LocalNewer, result.Status);
        Assert.Equal("previous-synced-hash", result.LastSyncedContentHash);
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task Upload_WithInvalidServerTimestamp_UsesCurrentTimeForSyncMetadata()
    {
        using var test = new SaveManagerTestHarness();
        test.WriteLocalSave(new byte[] { 0x23 });
        test.RomM.ConfigureRawPOSTResponse(test.UploadPath(), Response(987, "hash", 1, updatedAt: "invalid"));
        var before = DateTime.UtcNow;

        await test.Manager.Upload(test.Save);

        var after = DateTime.UtcNow;
        Assert.InRange(test.Save.LastSyncedAt!.Value, before, after);
        Assert.InRange(test.Save.ServerLastUpdatedAt!.Value, before, after);
    }

    [Fact]
    public async Task Upload_WhenMultipleFilesAreSelected_PreservesSourcesAndDeletesTemporaryArchive()
    {
        using var test = new SaveManagerTestHarness();
        var first = test.WriteLocalSave(new byte[] { 0x10, 0x11 });
        var second = test.WriteLocalSave(new byte[] { 0x20, 0x21 }, "nested/extra.sav");
        test.Save.Filename = "bundle.zip";
        test.Save.SourceFilePaths = new ObservableCollection<string>
        {
            EmulatorMapping.SavePathToken + "/autosave.sav",
            EmulatorMapping.SavePathToken + "/nested/extra.sav"
        };
        test.RomM.ConfigureRawPOSTResponse(test.UploadPath(), Response(987, "packed-hash", 4, "bundle.zip"));

        await test.Manager.Upload(test.Save);

        Assert.Equal(SaveStatus.Synced, test.Save.Status);
        Assert.Equal(new byte[] { 0x10, 0x11 }, File.ReadAllBytes(first));
        Assert.Equal(new byte[] { 0x20, 0x21 }, File.ReadAllBytes(second));
        Assert.False(File.Exists(Path.Combine(test.TempDirectory, "bundle.zip")));
        await test.RomM.Server.Received(1).RawPOSTAsync(test.UploadPath(), Arg.Any<HttpContent>(), false);
    }

    private static byte[] Response(int id, string hash, long size, string? filename = "autosave.sav", string updatedAt = "2026-10-10T12:00:00Z")
    {
        var save = new RomMSave
        {
            ID = id,
            ROMID = 42,
            FileName = filename,
            ContentHash = hash,
            FileSize = size,
            UpdatedAt = updatedAt,
            Slot = "autosave"
        };
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(save));
    }
}
