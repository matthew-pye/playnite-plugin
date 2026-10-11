using Graviton.Install;
using Graviton.Install.Downloads;
using Graviton.Models.Install;
using Graviton.Models.RomM.Rom;
using Graviton.Tests.Fakes.Install;

using System.IO;
using System.IO.Compression;

using Xunit;

namespace Graviton.Tests.Install;

[Collection("GravitonUpdateDLC")]
public sealed class GravitonInstallUpdateDLCMappedFolderTests
{
    [Theory]
    [InlineData("cemu.update", "0005000e")]
    [InlineData("xenia.tuupdate", "000B0000")]
    public async Task Complete_UpdateSingleFile_UsesEmulatorSpecificTitleIDDirectory(string definitionID, string specialDirectory)
    {
        using var harness = new UpdateDLCInstallTestHarness();
        harness.Mapping.UpdateInstallStyle = InstallStyles.MappedFolder;
        TitleIDInstallDefinitions.Initialize();
        harness.Mapping.UpdateTitleIDDefinitionID = definitionID;
        var candidate = UpdateDLCInstallTestHarness.Candidate();
        candidate.SingleFileRelativePath = "patch.pkg";
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate);
        byte[] payload = [0x12, 0x34, 0x56];
        harness.WriteDownloadedFile(request, payload);

        await request.OnDownloadComplete(new DownloadQueueItem(), request);

        var installed = definitionID == "xenia.tuupdate"
            ? Path.Combine(harness.UpdateDestination, "TITLE123", specialDirectory, "patch.pkg")
            : Path.Combine(harness.UpdateDestination, specialDirectory, "TITLE123", "patch.pkg");
        Assert.Equal(payload, File.ReadAllBytes(installed));
        Assert.Equal(new[] { installed }, candidate.InstalledTopPaths);
        Assert.Equal(InstallStatus.Installed, candidate.Status);
        Assert.Equal(InstallStyles.MappedFolder, candidate.PreviousInstallStyle);
    }

    [Fact]
    public async Task Complete_DefaultDefinition_UsesConfiguredFolderAndTitleID()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        harness.Mapping.UpdateInstallStyle = InstallStyles.MappedFolder;
        TitleIDInstallDefinitions.Initialize();
        harness.Mapping.UpdateTitleIDDefinitionID = null;
        var candidate = UpdateDLCInstallTestHarness.Candidate();
        candidate.SingleFileRelativePath = "patch.pkg";
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate);
        harness.WriteDownloadedFile(request, [7, 8, 9]);

        await request.OnDownloadComplete(new DownloadQueueItem(), request);

        Assert.Equal(new byte[] { 7, 8, 9 }, File.ReadAllBytes(Path.Combine(harness.UpdateDestination, "TITLE123", "patch.pkg")));
        Assert.Equal(InstallStatus.Installed, candidate.Status);
    }

    [Theory]
    [InlineData("cemu.dlc", "0005000c", "TITLE123")]
    [InlineData("shadps4.dlc", null, "TITLE123-Costume Pack")]
    public async Task Complete_DLCSingleFile_UsesDLCDefinitionAndDestination(string definitionID, string? specialDirectory, string leafDirectory)
    {
        using var harness = new UpdateDLCInstallTestHarness();
        harness.Mapping.DLCInstallStyle = InstallStyles.MappedFolder;
        TitleIDInstallDefinitions.Initialize();
        harness.Mapping.DLCTitleIDDefinitionID = definitionID;
        var candidate = UpdateDLCInstallTestHarness.Candidate(name: "Costume: Pack", category: RomMCategory.DLC);
        candidate.SingleFileRelativePath = "content/expansion.pkg";
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate, RomMCategory.DLC);
        byte[] payload = [19, 20, 21];
        harness.WriteDownloadedFile(request, payload);

        await request.OnDownloadComplete(new DownloadQueueItem(), request);

        var baseDirectory = specialDirectory == null
            ? harness.DLCDestination
            : Path.Combine(harness.DLCDestination, specialDirectory);
        var installed = Path.Combine(baseDirectory, leafDirectory, "content", "expansion.pkg");
        Assert.Equal(payload, File.ReadAllBytes(installed));
        Assert.Equal(new[] { installed.Replace('\\', '/') }, candidate.InstalledTopPaths.Select(path => path.Replace('\\', '/')));
        Assert.False(Directory.Exists(harness.UpdateDestination));
        Assert.Equal(InstallStatus.Installed, candidate.Status);
    }

    [Fact]
    public async Task Complete_SingleFile_CreatesNestedRelativeDirectories()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        harness.Mapping.UpdateInstallStyle = InstallStyles.MappedFolder;
        TitleIDInstallDefinitions.Initialize();
        harness.Mapping.UpdateTitleIDDefinitionID = "shadps4.update";
        var candidate = UpdateDLCInstallTestHarness.Candidate();
        candidate.SingleFileRelativePath = "sce_sys/metadata/param.sfo";
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate);
        harness.WriteDownloadedFile(request, [0x53, 0x46, 0x4F]);

        await request.OnDownloadComplete(new DownloadQueueItem(), request);

        var installed = Path.Combine(harness.UpdateDestination, "TITLE123-UPDATE", "sce_sys", "metadata", "param.sfo");
        Assert.Equal(new byte[] { 0x53, 0x46, 0x4F }, File.ReadAllBytes(installed));
        Assert.Equal(new[] { installed.Replace('\\', '/') }, candidate.InstalledTopPaths.Select(path => path.Replace('\\', '/')));
    }

    [Fact]
    public async Task Complete_SingleFile_OverwritesOnlyTheMatchingExistingFile()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        harness.Mapping.UpdateInstallStyle = InstallStyles.MappedFolder;
        TitleIDInstallDefinitions.Initialize();
        harness.Mapping.UpdateTitleIDDefinitionID = "shadps4.update";
        var candidate = UpdateDLCInstallTestHarness.Candidate();
        candidate.SingleFileRelativePath = "sce_sys/param.sfo";
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate);
        var directory = Path.Combine(harness.UpdateDestination, "TITLE123-UPDATE", "sce_sys");
        Directory.CreateDirectory(directory);
        var replaced = Path.Combine(directory, "param.sfo");
        var unrelated = Path.Combine(directory, "existing.sfo");
        File.WriteAllBytes(replaced, [1, 1]);
        File.WriteAllBytes(unrelated, [2, 2]);
        harness.WriteDownloadedFile(request, [3, 4, 5]);

        await request.OnDownloadComplete(new DownloadQueueItem(), request);

        Assert.Equal(new byte[] { 3, 4, 5 }, File.ReadAllBytes(replaced));
        Assert.Equal(new byte[] { 2, 2 }, File.ReadAllBytes(unrelated));
        Assert.Equal(new byte[] { 3, 4, 5 }, File.ReadAllBytes(request.DownloadPath));
    }

    [Fact]
    public async Task Complete_SingleFile_MissingDownloadedSourceDoesNotChangeExistingInstallation()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        harness.Mapping.UpdateInstallStyle = InstallStyles.MappedFolder;
        TitleIDInstallDefinitions.Initialize();
        harness.Mapping.UpdateTitleIDDefinitionID = "shadps4.update";
        var candidate = UpdateDLCInstallTestHarness.Candidate();
        candidate.SingleFileRelativePath = "sce_sys/param.sfo";
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate);
        var installed = Path.Combine(harness.UpdateDestination, "TITLE123-UPDATE", "sce_sys", "param.sfo");
        Directory.CreateDirectory(Path.GetDirectoryName(installed)!);
        File.WriteAllBytes(installed, [10, 11]);
        Directory.CreateDirectory(Path.GetDirectoryName(request.DownloadPath)!);

        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await request.OnDownloadComplete(new DownloadQueueItem(), request));

        Assert.Equal(new byte[] { 10, 11 }, File.ReadAllBytes(installed));
        Assert.Equal(InstallStatus.NotInstalled, candidate.Status);
        Assert.Empty(candidate.InstalledFileIDs);
        Assert.Empty(candidate.InstalledTopPaths);
    }

    [Fact]
    public async Task Complete_ArchiveWithCandidateRoot_CopiesOnlyChosenRoot()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        harness.Mapping.UpdateInstallStyle = InstallStyles.MappedFolder;
        TitleIDInstallDefinitions.Initialize();
        harness.Mapping.UpdateTitleIDDefinitionID = "shadps4.update";
        var candidate = UpdateDLCInstallTestHarness.Candidate(ids: [1, 2], fileName: "bundle.zip");
        candidate.CandidateRoot = "selected";
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate);
        WriteArchive(request.DownloadPath,
            ("selected/sce_sys/param.sfo", new byte[] { 1, 2 }),
            ("selected/content/pack.dat", new byte[] { 3, 4 }),
            ("unrelated/not-for-installation.bin", new byte[] { 5, 6 }));

        await request.OnDownloadComplete(new DownloadQueueItem(), request);

        var destination = Path.Combine(harness.UpdateDestination, "TITLE123-UPDATE");
        Assert.Equal(new byte[] { 1, 2 }, File.ReadAllBytes(Path.Combine(destination, "sce_sys", "param.sfo")));
        Assert.Equal(new byte[] { 3, 4 }, File.ReadAllBytes(Path.Combine(destination, "content", "pack.dat")));
        Assert.False(Directory.Exists(Path.Combine(destination, "unrelated")));
        Assert.Contains(Path.Combine(destination, "sce_sys"), candidate.InstalledTopPaths);
        Assert.Contains(Path.Combine(destination, "content"), candidate.InstalledTopPaths);
        Assert.Equal(2, candidate.InstalledTopPaths.Count);
        Assert.Equal(InstallStatus.Installed, candidate.Status);
    }

    [Fact]
    public async Task Complete_ArchiveWithoutCandidateRoot_PreservesTopLevelLayout()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        harness.Mapping.DLCInstallStyle = InstallStyles.MappedFolder;
        TitleIDInstallDefinitions.Initialize();
        harness.Mapping.DLCTitleIDDefinitionID = "cemu.dlc";
        var candidate = UpdateDLCInstallTestHarness.Candidate(category: RomMCategory.DLC, ids: [4, 5], fileName: "expansion.zip");
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate, RomMCategory.DLC);
        WriteArchive(request.DownloadPath,
            ("code/app.bin", new byte[] { 7 }),
            ("meta.xml", new byte[] { 8, 9 }));

        await request.OnDownloadComplete(new DownloadQueueItem(), request);

        var destination = Path.Combine(harness.DLCDestination, "0005000c", "TITLE123");
        Assert.Equal(new byte[] { 7 }, File.ReadAllBytes(Path.Combine(destination, "code", "app.bin")));
        Assert.Equal(new byte[] { 8, 9 }, File.ReadAllBytes(Path.Combine(destination, "meta.xml")));
        Assert.Contains(Path.Combine(destination, "code"), candidate.InstalledTopPaths);
        Assert.Contains(Path.Combine(destination, "meta.xml"), candidate.InstalledTopPaths);
        Assert.Equal(InstallStatus.Installed, candidate.Status);
    }

    [Fact]
    public async Task Complete_ArchiveWithMissingCandidateRoot_PreservesExistingInstallation()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        harness.Mapping.UpdateInstallStyle = InstallStyles.MappedFolder;
        TitleIDInstallDefinitions.Initialize();
        harness.Mapping.UpdateTitleIDDefinitionID = "shadps4.update";
        var candidate = UpdateDLCInstallTestHarness.Candidate(ids: [1, 2], fileName: "bundle.zip");
        candidate.CandidateRoot = "missing-root";
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate);
        var existing = Path.Combine(harness.UpdateDestination, "TITLE123-UPDATE", "keep.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
        File.WriteAllBytes(existing, [9, 9]);
        WriteArchive(request.DownloadPath, ("another-root/file.bin", new byte[] { 1, 2 }));

        await Assert.ThrowsAsync<DirectoryNotFoundException>(async () =>
            await request.OnDownloadComplete(new DownloadQueueItem(), request));

        Assert.Equal(new byte[] { 9, 9 }, File.ReadAllBytes(existing));
        Assert.False(File.Exists(Path.Combine(harness.UpdateDestination, "TITLE123-UPDATE", "file.bin")));
        Assert.Equal(InstallStatus.NotInstalled, candidate.Status);
        Assert.Empty(candidate.InstalledTopPaths);
    }

    [Fact]
    public async Task Complete_ArchiveWithInvalidContents_DoesNotMarkCandidateInstalled()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        harness.Mapping.UpdateInstallStyle = InstallStyles.MappedFolder;
        TitleIDInstallDefinitions.Initialize();
        harness.Mapping.UpdateTitleIDDefinitionID = "shadps4.update";
        var candidate = UpdateDLCInstallTestHarness.Candidate(ids: [1, 2], fileName: "invalid.zip");
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate);
        harness.WriteDownloadedFile(request, [1, 2, 3, 4]);

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await request.OnDownloadComplete(new DownloadQueueItem(), request));

        Assert.Equal(InstallStatus.NotInstalled, candidate.Status);
        Assert.Empty(candidate.InstalledFileIDs);
        Assert.Empty(candidate.InstalledTopPaths);
        Assert.False(Directory.Exists(Path.Combine(harness.UpdateDestination, "TITLE123-UPDATE")));
    }

    [Theory]
    [InlineData("update")]
    [InlineData("dlc")]
    public async Task Complete_MissingConfiguredInstallPath_DoesNotCreateOutput(string category)
    {
        using var harness = new UpdateDLCInstallTestHarness();
        TitleIDInstallDefinitions.Initialize();
        if (category == RomMCategory.Update)
        {
            harness.Mapping.UpdateInstallStyle = InstallStyles.MappedFolder;
            harness.Mapping.UpdateTitleIDDefinitionID = "shadps4.update";
            harness.Mapping.UpdateInstallPath = null;
        }
        else
        {
            harness.Mapping.DLCInstallStyle = InstallStyles.MappedFolder;
            harness.Mapping.DLCTitleIDDefinitionID = "shadps4.dlc";
            harness.Mapping.DLCInstallPath = null;
        }
        var candidate = UpdateDLCInstallTestHarness.Candidate(category: category);
        candidate.SingleFileRelativePath = "sce_sys/param.sfo";
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate, category);
        harness.WriteDownloadedFile(request, [1, 2]);

        var exception = await Assert.ThrowsAsync<Exception>(async () =>
            await request.OnDownloadComplete(new DownloadQueueItem(), request));

        Assert.Contains("install path", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(InstallStatus.NotInstalled, candidate.Status);
        Assert.Empty(candidate.InstalledFileIDs);
        Assert.False(Directory.Exists(harness.UpdateDestination));
        Assert.False(Directory.Exists(harness.DLCDestination));
    }

    [Fact]
    public async Task Complete_MappedDLCWithUnknownDefinition_DoesNotMarkCandidateInstalled()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        harness.Mapping.DLCInstallStyle = InstallStyles.MappedFolder;
        TitleIDInstallDefinitions.Initialize();
        harness.Mapping.DLCTitleIDDefinitionID = "unknown-dlc-layout";
        var candidate = UpdateDLCInstallTestHarness.Candidate(category: RomMCategory.DLC);
        candidate.SingleFileRelativePath = "content/expansion.pkg";
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate, RomMCategory.DLC);
        harness.WriteDownloadedFile(request, [1, 2]);

        var exception = await Assert.ThrowsAsync<Exception>(async () =>
            await request.OnDownloadComplete(new DownloadQueueItem(), request));

        Assert.Contains("definition", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(InstallStatus.NotInstalled, candidate.Status);
        Assert.False(Directory.Exists(harness.DLCDestination));
    }

    [Fact]
    public async Task Complete_CancelledMappedFolderInstall_PreservesExistingFile()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        harness.Mapping.UpdateInstallStyle = InstallStyles.MappedFolder;
        TitleIDInstallDefinitions.Initialize();
        harness.Mapping.UpdateTitleIDDefinitionID = "shadps4.update";
        var candidate = UpdateDLCInstallTestHarness.Candidate();
        candidate.SingleFileRelativePath = "sce_sys/param.sfo";
        var request = await UpdateDLCCompletionTestHelpers.BuildRequest(harness, candidate);
        var existing = Path.Combine(harness.UpdateDestination, "TITLE123-UPDATE", "sce_sys", "param.sfo");
        Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
        File.WriteAllBytes(existing, [8, 8]);
        harness.WriteDownloadedFile(request, [1, 2]);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var item = new DownloadQueueItem { Cts = cts };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await request.OnDownloadComplete(item, request));

        Assert.Equal(new byte[] { 8, 8 }, File.ReadAllBytes(existing));
        Assert.Equal(InstallStatus.NotInstalled, candidate.Status);
        Assert.Empty(candidate.InstalledTopPaths);
    }

    private static void WriteArchive(string path, params (string Path, byte[] Contents)[] entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var entry in entries)
        {
            using var stream = archive.CreateEntry(entry.Path).Open();
            stream.Write(entry.Contents);
        }
    }
}
