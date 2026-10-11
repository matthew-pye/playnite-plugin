using Graviton.Models.Saves;
using Graviton.Tests.Fakes.Saves;

using NSubstitute;

using System.IO;
using System.Net;
using System.Security.Cryptography;

using Xunit;

namespace Graviton.Tests.Saves;

[CollectionDefinition("SaveManagerTransferTests", DisableParallelization = true)]
public sealed class SaveManagerTransferCollection
{
}

[Collection("SaveManagerTransferTests")]
public sealed class SaveManagerDownloadTransferTests
{
    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Download_WhenServerRejectsRequest_PreservesExistingSave(HttpStatusCode statusCode)
    {
        using var test = new SaveManagerTestHarness();
        var original = new byte[] { 0x00, 0xFF, 0x31, 0x80 };
        var path = test.WriteLocalSave(original);
        test.RomM.ConfigureRawGETResponse(test.DownloadPath, new byte[] { 0x42 }, statusCode);

        var result = await test.Manager.Download(test.Save);

        Assert.Same(test.Save, result);
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Equal(SaveStatus.RemoteNewer, test.Save.Status);
        Assert.Equal("previous-local-hash", test.Save.ContentHash);
        Assert.Equal("previous-synced-hash", test.Save.LastSyncedContentHash);
        Assert.False(File.Exists(Path.Combine(test.TempDirectory, test.Save.Filename)));

        await test.RomM.Server.DidNotReceiveWithAnyArgs().POSTAsync(default!, default(object)!, default);
    }

    [Fact]
    public async Task Download_WhenServerHashDoesNotMatch_PreservesExistingSaveAndSyncState()
    {
        using var test = new SaveManagerTestHarness();
        var original = new byte[] { 0x01, 0x02, 0xFF, 0x00 };
        var path = test.WriteLocalSave(original);
        test.Save.ServerHash = new string('a', 32);
        test.RomM.ConfigureRawGETResponse(test.DownloadPath, new byte[] { 0x99, 0x98, 0x97 });

        var result = await test.Manager.Download(test.Save);

        Assert.Same(test.Save, result);
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Equal(SaveStatus.RemoteNewer, test.Save.Status);
        Assert.Equal("previous-local-hash", test.Save.ContentHash);
        Assert.Equal("previous-synced-hash", test.Save.LastSyncedContentHash);
        Assert.Null(test.Save.LastSyncedAt);

        await test.RomM.Server.DidNotReceiveWithAnyArgs().POSTAsync(default!, default(object)!, default);
    }

    [Fact]
    public async Task Download_WhenEmptyContentHashDoesNotMatch_PreservesExistingFile()
    {
        using var test = new SaveManagerTestHarness();
        var path = test.WriteLocalSave(new byte[] { 0x12, 0x34 });
        test.Save.ServerHash = new string('b', 32);
        test.RomM.ConfigureRawGETResponse(test.DownloadPath, Array.Empty<byte>());

        await test.Manager.Download(test.Save);

        Assert.Equal(new byte[] { 0x12, 0x34 }, File.ReadAllBytes(path));
        Assert.Equal(SaveStatus.RemoteNewer, test.Save.Status);
    }

    [Fact]
    public async Task Download_WhenContentIsValid_ReplacesOnlyTheTrackedFile()
    {
        using var test = new SaveManagerTestHarness();
        var downloaded = new byte[] { 0x00, 0xFF, 0x42, 0x80, 0x31 };
        var path = test.WriteLocalSave(new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x60 });
        var unrelatedPath = test.WriteLocalSave(new byte[] { 0xAC, 0xBD }, "other.sav");
        test.Save.ServerHash = Hash(downloaded);
        test.RomM.ConfigureRawGETResponse(test.DownloadPath, downloaded);
        test.RomM.ConfigureJsonPOSTResponse($"/api/saves/{test.Save.SaveID}/downloaded");

        var result = await test.Manager.Download(test.Save, skipSavingROM: true);

        Assert.Same(test.Save, result);
        Assert.Equal(downloaded, File.ReadAllBytes(path));
        Assert.Equal(new byte[] { 0xAC, 0xBD }, File.ReadAllBytes(unrelatedPath));
        Assert.Equal(SaveStatus.Synced, test.Save.Status);
        Assert.Equal(test.Save.ServerHash, test.Save.ContentHash);
        Assert.Equal(test.Save.ServerHash, test.Save.LastSyncedContentHash);
        Assert.Equal(test.Save.ServerLastUpdatedAt, test.Save.LastSyncedAt);
        Assert.Equal(downloaded.Length, test.Save.FileSize);
        Assert.False(File.Exists(Path.Combine(test.TempDirectory, test.Save.Filename)));

        await test.RomM.Server.Received(1).POSTAsync(
            APIPath: $"/api/saves/{test.Save.SaveID}/downloaded",
            json: Arg.Any<object>(),
            PublicEndpoint: false);
    }

    [Fact]
    public async Task Download_WhenLocalSaveDoesNotExist_WritesDownloadedBytes()
    {
        using var test = new SaveManagerTestHarness();
        var downloaded = new byte[] { 0x34, 0x56, 0x78 };
        test.Save.ServerHash = Hash(downloaded);
        test.RomM.ConfigureRawGETResponse(test.DownloadPath, downloaded);
        test.RomM.ConfigureJsonPOSTResponse($"/api/saves/{test.Save.SaveID}/downloaded");

        await test.Manager.Download(test.Save, skipSavingROM: true);

        Assert.Equal(downloaded, File.ReadAllBytes(Path.Combine(test.SavesDirectory, "autosave.sav")));
        Assert.Equal(SaveStatus.Synced, test.Save.Status);
    }

    [Fact]
    public async Task Download_UsesConfiguredDeviceIDAndAcknowledgesSuccessfulDownload()
    {
        using var test = new SaveManagerTestHarness();
        var downloaded = new byte[] { 0xA0, 0xB1 };
        test.Plugin.Settings.AccountState.DeviceID = "handheld-01";
        test.Save.ServerHash = Hash(downloaded);
        test.RomM.ConfigureRawGETResponse(test.DownloadPath, downloaded);
        test.RomM.ConfigureJsonPOSTResponse($"/api/saves/{test.Save.SaveID}/downloaded");

        await test.Manager.Download(test.Save, skipSavingROM: true);

        await test.RomM.Server.Received(1).RawGETAsync(
            APIPath: $"/api/saves/{test.Save.SaveID}/content?device_id=handheld-01&optimistic=false",
            PublicEndpoint: false);
        await test.RomM.Server.Received(1).POSTAsync(
            APIPath: $"/api/saves/{test.Save.SaveID}/downloaded",
            json: Arg.Is<object>(value => HasDeviceID(value, "handheld-01")),
            PublicEndpoint: false);
    }

    private static bool HasDeviceID(object value, string deviceID)
    {
        var json = System.Text.Json.JsonSerializer.SerializeToElement(value);
        return json.GetProperty("device_id").GetString() == deviceID;
    }

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
    }
}
