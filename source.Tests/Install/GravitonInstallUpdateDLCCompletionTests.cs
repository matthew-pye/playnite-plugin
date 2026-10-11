using Graviton.Install;
using Graviton.Install.Downloads;
using Graviton.Models.Install;
using Graviton.Models.RomM.Rom;
using Graviton.Tests.Fakes.Install;

using System.IO;
using System.IO.Compression;
using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Install;

[Collection("GravitonUpdateDLC")]
public sealed class GravitonInstallUpdateDLCCompletionTests
{
    [Fact]
    public async Task Complete_NoneStyle_MarksCandidateInstalledWithoutCreatingFiles()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        harness.Mapping.UpdateInstallStyle = InstallStyles.None;
        var candidate = UpdateDLCInstallTestHarness.Candidate(ids: [11, 12], size: 1024);
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate);

        await request.OnDownloadComplete(new DownloadQueueItem(), request);

        Assert.Equal(InstallStatus.Installed, candidate.Status);
        Assert.Equal(new[] { 11, 12 }, candidate.InstalledFileIDs);
        Assert.Equal(1024, candidate.InstalledSize);
        Assert.Equal(InstallStyles.None, candidate.PreviousInstallStyle);
        Assert.Empty(candidate.InstalledTopPaths);
        Assert.False(Directory.Exists(harness.UpdateDestination));
    }

    [Fact]
    public async Task Complete_DLCNoneStyle_MarksCandidateInstalled()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        harness.Mapping.DLCInstallStyle = InstallStyles.None;
        var candidate = UpdateDLCInstallTestHarness.Candidate(category: RomMCategory.DLC, ids: [31]);
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate, RomMCategory.DLC);

        await request.OnDownloadComplete(new DownloadQueueItem(), request);

        Assert.Equal(InstallStatus.Installed, candidate.Status);
        Assert.Equal(new[] { 31 }, candidate.InstalledFileIDs);
        Assert.Equal(InstallStyles.None, candidate.PreviousInstallStyle);
    }

    [Fact]
    public async Task Complete_WhenROMIsTracked_PersistsInstalledCandidateMetadata()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        harness.Mapping.UpdateInstallStyle = InstallStyles.None;
        var candidate = UpdateDLCInstallTestHarness.Candidate(ids: [301], size: 4096);
        var rom = harness.RegisterLocalROM();
        rom.UpdateCandidates.Add(candidate);
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate);

        await request.OnDownloadComplete(new DownloadQueueItem(), request);

        var savedPath = Path.Combine(harness.Root, "Games", "42.json");
        Assert.True(File.Exists(savedPath));
        using var document = JsonDocument.Parse(File.ReadAllText(savedPath));
        var serialized = document.RootElement.GetProperty("UpdateCandidates")[0];
        Assert.Equal((int)InstallStatus.Installed, serialized.GetProperty("Status").GetInt32());
        Assert.Equal(4096, serialized.GetProperty("InstalledSize").GetInt64());
        Assert.Equal(301, serialized.GetProperty("InstalledFileIDs")[0].GetInt32());
    }

    [Fact]
    public async Task Complete_WhenROMIsNotTracked_DoesNotCreateMetadataFile()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        harness.Mapping.UpdateInstallStyle = InstallStyles.None;
        var candidate = UpdateDLCInstallTestHarness.Candidate();
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate);

        await request.OnDownloadComplete(new DownloadQueueItem(), request);

        Assert.Equal(InstallStatus.Installed, candidate.Status);
        Assert.False(File.Exists(Path.Combine(harness.Root, "Games", "42.json")));
    }

    [Fact]
    public async Task Complete_FolderStyle_CopiesSingleUpdateFileToGameDirectory()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        var candidate = UpdateDLCInstallTestHarness.Candidate();
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate);
        byte[] bytes = [0, 42, 255, 7];
        harness.WriteDownloadedFile(request, bytes);

        await request.OnDownloadComplete(new DownloadQueueItem(), request);

        var installed = Path.Combine(harness.UpdateDestination, "Example Game", "patch.pkg");
        Assert.Equal(bytes, File.ReadAllBytes(installed));
        Assert.Equal(new[] { installed }, candidate.InstalledTopPaths);
        Assert.Equal(InstallStyles.Folder, candidate.PreviousInstallStyle);
    }

    [Fact]
    public async Task Complete_FolderStyle_CopiesDLCToItsOwnConfiguredDirectory()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        var candidate = UpdateDLCInstallTestHarness.Candidate(category: RomMCategory.DLC, fileName: "expansion.pkg");
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate, RomMCategory.DLC);
        byte[] bytes = [1, 2, 3];
        harness.WriteDownloadedFile(request, bytes);

        await request.OnDownloadComplete(new DownloadQueueItem(), request);

        var installed = Path.Combine(harness.DLCDestination, "Example Game", "expansion.pkg");
        Assert.Equal(bytes, File.ReadAllBytes(installed));
        Assert.Equal(new[] { installed }, candidate.InstalledTopPaths);
        Assert.False(Directory.Exists(harness.UpdateDestination));
    }

    [Fact]
    public async Task Complete_FolderStyle_OverwritesExistingCandidateFile()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        var candidate = UpdateDLCInstallTestHarness.Candidate();
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate);
        var installed = Path.Combine(harness.UpdateDestination, "Example Game", "patch.pkg");
        Directory.CreateDirectory(Path.GetDirectoryName(installed)!);
        File.WriteAllBytes(installed, [1, 1, 1]);
        harness.WriteDownloadedFile(request, [2, 2, 2, 2]);

        await request.OnDownloadComplete(new DownloadQueueItem(), request);

        Assert.Equal(new byte[] { 2, 2, 2, 2 }, File.ReadAllBytes(installed));
        Assert.Equal(InstallStatus.Installed, candidate.Status);
    }

    [Fact]
    public async Task Complete_FolderStyle_PreservesUnrelatedInstalledFiles()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        var candidate = UpdateDLCInstallTestHarness.Candidate();
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate);
        var unrelated = Path.Combine(harness.UpdateDestination, "Example Game", "existing.pkg");
        Directory.CreateDirectory(Path.GetDirectoryName(unrelated)!);
        File.WriteAllBytes(unrelated, [10, 20, 30]);
        harness.WriteDownloadedFile(request, [4, 5, 6]);

        await request.OnDownloadComplete(new DownloadQueueItem(), request);

        Assert.Equal(new byte[] { 10, 20, 30 }, File.ReadAllBytes(unrelated));
    }

    [Fact]
    public async Task Complete_FolderStyle_KeepsDownloadedSourceFile()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        var candidate = UpdateDLCInstallTestHarness.Candidate();
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate);
        byte[] bytes = [7, 8, 9];
        harness.WriteDownloadedFile(request, bytes);

        await request.OnDownloadComplete(new DownloadQueueItem(), request);

        Assert.Equal(bytes, File.ReadAllBytes(request.DownloadPath));
    }

    [Fact]
    public async Task Complete_FolderStyle_MissingInstallPathDoesNotMarkCandidateInstalled()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        harness.Mapping.UpdateInstallPath = null;
        var candidate = UpdateDLCInstallTestHarness.Candidate();
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate);
        harness.WriteDownloadedFile(request, [1, 2]);

        var exception = await Assert.ThrowsAsync<Exception>(async () =>
        {
            await request.OnDownloadComplete(new DownloadQueueItem(), request);
        });

        Assert.Contains("no install path", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(InstallStatus.NotInstalled, candidate.Status);
        Assert.Empty(candidate.InstalledFileIDs);
    }

    [Fact]
    public async Task Complete_FolderStyle_MissingDownloadedFileDoesNotMarkCandidateInstalled()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        var candidate = UpdateDLCInstallTestHarness.Candidate();
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate);
        Directory.CreateDirectory(Path.GetDirectoryName(request.DownloadPath)!);

        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
        {
            await request.OnDownloadComplete(new DownloadQueueItem(), request);
        });

        Assert.Equal(InstallStatus.NotInstalled, candidate.Status);
        Assert.Empty(candidate.InstalledFileIDs);
    }

    [Fact]
    public async Task Complete_FolderStyle_CancellationBeforeInstallPreservesExistingFile()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        var candidate = UpdateDLCInstallTestHarness.Candidate();
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate);
        var installed = Path.Combine(harness.UpdateDestination, "Example Game", "patch.pkg");
        Directory.CreateDirectory(Path.GetDirectoryName(installed)!);
        File.WriteAllBytes(installed, [9, 8, 7]);
        harness.WriteDownloadedFile(request, [1, 2, 3]);
        using var item = new CancellationTokenSource();
        item.Cancel();
        var queueItem = new DownloadQueueItem { Cts = item };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await request.OnDownloadComplete(queueItem, request);
        });

        Assert.Equal(new byte[] { 9, 8, 7 }, File.ReadAllBytes(installed));
        Assert.Equal(InstallStatus.NotInstalled, candidate.Status);
    }

    [Fact]
    public async Task Complete_FolderStyle_ExtractsMultiFileArchiveAndTracksTopPaths()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        var candidate = UpdateDLCInstallTestHarness.Candidate(ids: [10, 11], fileName: "bundle.zip");
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate);
        Directory.CreateDirectory(Path.GetDirectoryName(request.DownloadPath)!);
        using (var archive = ZipFile.Open(request.DownloadPath, ZipArchiveMode.Create))
        {
            var rootEntry = archive.CreateEntry("manifest.txt");
            using (var stream = rootEntry.Open())
            {
                stream.Write([1, 2]);
            }
            var nestedEntry = archive.CreateEntry("content/data.bin");
            using (var stream = nestedEntry.Open())
            {
                stream.Write([3, 4]);
            }
        }

        await request.OnDownloadComplete(new DownloadQueueItem(), request);

        var installPath = Path.Combine(harness.UpdateDestination, "Example Game");
        var manifest = Path.Combine(installPath, "manifest.txt");
        var directory = Path.Combine(installPath, "content");
        Assert.Equal(new byte[] { 1, 2 }, File.ReadAllBytes(manifest));
        Assert.Equal(new byte[] { 3, 4 }, File.ReadAllBytes(Path.Combine(directory, "data.bin")));
        Assert.Contains(manifest, candidate.InstalledTopPaths);
        Assert.Contains(directory, candidate.InstalledTopPaths);
        Assert.Equal(InstallStatus.Installed, candidate.Status);
    }

    [Fact]
    public async Task Complete_FolderStyle_InvalidArchiveDoesNotMarkCandidateInstalled()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        var candidate = UpdateDLCInstallTestHarness.Candidate(ids: [10, 11], fileName: "broken.zip");
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate);
        harness.WriteDownloadedFile(request, [1, 2, 3, 4]);

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await request.OnDownloadComplete(new DownloadQueueItem(), request);
        });

        Assert.Equal(InstallStatus.NotInstalled, candidate.Status);
        Assert.Empty(candidate.InstalledFileIDs);
    }

    [Fact]
    public async Task Complete_MappedFolderStyle_MissingDefinitionDoesNotMarkCandidateInstalled()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        harness.Mapping.UpdateInstallStyle = InstallStyles.MappedFolder;
        harness.Mapping.UpdateTitleIDDefinitionID = "unknown-update-definition";
        var candidate = UpdateDLCInstallTestHarness.Candidate();
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate);
        harness.WriteDownloadedFile(request, [1, 2]);

        var exception = await Assert.ThrowsAsync<Exception>(async () =>
        {
            await request.OnDownloadComplete(new DownloadQueueItem(), request);
        });

        Assert.Contains("definition", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(InstallStatus.NotInstalled, candidate.Status);
    }
}

internal static class UpdateDLCCompletionTestHelpers
{
    public static async Task<DownloadRequest> BuildRequest(
        UpdateDLCInstallTestHarness harness,
        UpdateDLCCandidate candidate,
        string category = "update")
    {
        var request = await harness.Build(
            UpdateDLCInstallTestHarness.Candidates(candidate),
            category: category);

        return Assert.IsType<DownloadRequest>(request);
    }
}
