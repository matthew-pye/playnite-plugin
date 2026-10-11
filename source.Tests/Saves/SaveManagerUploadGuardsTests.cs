using Graviton.Models;
using Graviton.Models.Saves;
using Graviton.Tests.Fakes.Saves;

using NSubstitute;

using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;

using Xunit;

namespace Graviton.Tests.Saves;

[Collection("SaveManagerTransferTests")]
public sealed class SaveManagerUploadGuardsTests
{
    [Fact]
    public async Task Upload_WhenROMIsNotImported_DoesNotContactServer()
    {
        using var test = new SaveManagerTestHarness();
        test.WriteLocalSave(new byte[] { 0x10 });
        test.Save.ROMID = 999;

        var result = await test.Manager.Upload(test.Save);

        Assert.Same(test.Save, result);
        Assert.Equal(987, result.SaveID);
        await test.RomM.Server.DidNotReceiveWithAnyArgs().RawPOSTAsync(default!, default(HttpContent)!, default);
    }

    [Fact]
    public async Task Upload_WhenMappingIsMissing_DoesNotContactServer()
    {
        using var test = new SaveManagerTestHarness();
        test.WriteLocalSave(new byte[] { 0x10 });
        test.ROM.MappingID = Guid.NewGuid();

        var result = await test.Manager.Upload(test.Save);

        Assert.Same(test.Save, result);
        await test.RomM.Server.DidNotReceiveWithAnyArgs().RawPOSTAsync(default!, default(HttpContent)!, default);
    }

    [Theory]
    [InlineData("autosave.sav")]
    [InlineData("nested/never-created.sav")]
    public async Task Upload_WhenExpectedSaveDoesNotExist_DoesNotCreateOrUploadIt(string relativePath)
    {
        using var test = new SaveManagerTestHarness();
        test.Save.Status = SaveStatus.UntrackedLocal;
        test.Save.SourceFilePaths = new ObservableCollection<string>
        {
            EmulatorMapping.SavePathToken + "/" + relativePath
        };

        var result = await test.Manager.Upload(test.Save);

        Assert.Same(test.Save, result);
        Assert.Equal(SaveStatus.UntrackedLocal, result.Status);
        Assert.False(File.Exists(Path.Combine(test.SavesDirectory, relativePath)));
        await test.RomM.Server.DidNotReceiveWithAnyArgs().RawPOSTAsync(default!, default(HttpContent)!, default);
    }

    [Fact]
    public async Task Upload_WhenSaveIsZeroBytes_DoesNotUploadOrChangeStatus()
    {
        using var test = new SaveManagerTestHarness();
        var path = test.WriteLocalSave(Array.Empty<byte>());
        test.Save.Status = SaveStatus.UntrackedLocal;

        var result = await test.Manager.Upload(test.Save);

        Assert.Same(test.Save, result);
        Assert.Equal(0, new FileInfo(path).Length);
        Assert.Equal(SaveStatus.UntrackedLocal, result.Status);
        Assert.Equal(987, result.SaveID);
        await test.RomM.Server.DidNotReceiveWithAnyArgs().RawPOSTAsync(default!, default(HttpContent)!, default);
    }

    [Fact]
    public async Task Upload_WhenASelectedFileHasDisappeared_DoesNotUploadPartialArchive()
    {
        using var test = new SaveManagerTestHarness();
        var existingPath = test.WriteLocalSave(new byte[] { 0x01, 0x02 });
        test.Save.Filename = "bundle.zip";
        test.Save.Status = SaveStatus.LocalNewer;
        test.Save.SourceFilePaths = new ObservableCollection<string>
        {
            EmulatorMapping.SavePathToken + "/autosave.sav",
            EmulatorMapping.SavePathToken + "/missing.sav"
        };

        var result = await test.Manager.Upload(test.Save);

        Assert.Same(test.Save, result);
        Assert.Equal(SaveStatus.LocalNewer, result.Status);
        Assert.Equal(new byte[] { 0x01, 0x02 }, File.ReadAllBytes(existingPath));
        await test.RomM.Server.DidNotReceiveWithAnyArgs().RawPOSTAsync(default!, default(HttpContent)!, default);
    }
}
