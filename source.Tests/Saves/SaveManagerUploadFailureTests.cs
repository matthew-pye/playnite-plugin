using Graviton.Models;
using Graviton.Models.Saves;
using Graviton.Tests.Fakes.Saves;

using NSubstitute;

using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Http;

using Xunit;

namespace Graviton.Tests.Saves;

[Collection("SaveManagerTransferTests")]
public sealed class SaveManagerUploadFailureTests
{
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task Upload_WhenRomMRejectsRequest_PreservesLocalSaveAndSyncState(HttpStatusCode statusCode)
    {
        using var test = new SaveManagerTestHarness();
        var original = new byte[] { 0x00, 0xEF, 0x55, 0x8A };
        var path = test.WriteLocalSave(original);
        test.Save.Status = SaveStatus.LocalNewer;
        test.RomM.ConfigureRawPOSTResponse(test.UploadPath(), Array.Empty<byte>(), statusCode);

        var result = await test.Manager.Upload(test.Save);

        Assert.Same(test.Save, result);
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Equal(SaveStatus.LocalNewer, test.Save.Status);
        Assert.Equal(987, test.Save.SaveID);
        Assert.Equal("previous-synced-hash", test.Save.LastSyncedContentHash);
        Assert.Null(test.Save.LastSyncedAt);

        await test.RomM.Server.Received(1).RawPOSTAsync(
            APIPath: test.UploadPath(),
            content: Arg.Is<HttpContent>(content => content is MultipartFormDataContent),
            PublicEndpoint: false);
    }

    [Fact]
    public async Task Upload_WithOverwrite_UsesOverwriteFlagWithoutChangingFileOnServerError()
    {
        using var test = new SaveManagerTestHarness();
        var original = new byte[] { 0x10, 0x11, 0xF2 };
        var path = test.WriteLocalSave(original);
        test.RomM.ConfigureRawPOSTResponse(test.UploadPath(overwrite: true), Array.Empty<byte>(), HttpStatusCode.ServiceUnavailable);

        await test.Manager.Upload(test.Save, overwrite: true);

        Assert.Equal(original, File.ReadAllBytes(path));
        await test.RomM.Server.Received(1).RawPOSTAsync(
            APIPath: test.UploadPath(overwrite: true),
            content: Arg.Any<HttpContent>(),
            PublicEndpoint: false);
    }

    [Fact]
    public async Task Upload_WithMultipleFiles_WhenRomMRejectsRequest_PreservesSourcesAndRemovesTemporaryArchive()
    {
        using var test = new SaveManagerTestHarness();
        var first = new byte[] { 0x01, 0x02, 0xFF };
        var second = new byte[] { 0x80, 0x81, 0x82 };
        var firstPath = test.WriteLocalSave(first);
        var secondPath = test.WriteLocalSave(second, "nested/second.sav");
        test.Save.Filename = "archive.zip";
        test.Save.SourceFilePaths = new ObservableCollection<string>
        {
            EmulatorMapping.SavePathToken + "/autosave.sav",
            EmulatorMapping.SavePathToken + "/nested/second.sav"
        };
        test.Save.Status = SaveStatus.LocalNewer;
        test.RomM.ConfigureRawPOSTResponse(test.UploadPath(), Array.Empty<byte>(), HttpStatusCode.ServiceUnavailable);

        var result = await test.Manager.Upload(test.Save);

        Assert.Same(test.Save, result);
        Assert.Equal(first, File.ReadAllBytes(firstPath));
        Assert.Equal(second, File.ReadAllBytes(secondPath));
        Assert.Equal(SaveStatus.LocalNewer, test.Save.Status);
        Assert.False(File.Exists(Path.Combine(test.TempDirectory, "archive.zip")));
    }
}
