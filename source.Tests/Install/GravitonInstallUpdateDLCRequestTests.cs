using Graviton.Install;
using Graviton.Install.Downloads;
using Graviton.Models.Install;
using Graviton.Models.RomM.Rom;
using Graviton.Tests.Fakes.Install;

using System.Collections.ObjectModel;
using System.IO;

using Xunit;

namespace Graviton.Tests.Install;

[CollectionDefinition("GravitonUpdateDLC", DisableParallelization = true)]
public sealed class GravitonUpdateDLCCollection
{
}

[Collection("GravitonUpdateDLC")]
public sealed class GravitonInstallUpdateDLCRequestTests
{
    [Fact]
    public async Task Build_WhenRemoteROMHasNoUpdates_PreservesPreviousRequest()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        var previous = new DownloadRequest { Id = "previous" };
        var candidate = UpdateDLCInstallTestHarness.Candidate();

        var result = await harness.Build(
            UpdateDLCInstallTestHarness.Candidates(candidate),
            rom: harness.RemoteROM(RomMCategory.Game),
            previous: previous);

        Assert.Same(previous, result);
        Assert.False(candidate.IsSelected);
    }

    [Fact]
    public async Task Build_WhenRemoteROMHasNoDLC_PreservesPreviousRequest()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        var previous = new DownloadRequest { Id = "previous" };
        var candidate = UpdateDLCInstallTestHarness.Candidate(category: RomMCategory.DLC);

        var result = await harness.Build(
            UpdateDLCInstallTestHarness.Candidates(candidate),
            category: RomMCategory.DLC,
            rom: harness.RemoteROM(RomMCategory.Update),
            previous: previous);

        Assert.Same(previous, result);
        Assert.False(candidate.IsSelected);
    }

    [Fact]
    public async Task Build_WhenMappingIsMissing_DoesNotCreateRequest()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        var info = harness.InstallInfo(useDefault: false);
        var candidate = UpdateDLCInstallTestHarness.Candidate();

        var result = await harness.Build(
            UpdateDLCInstallTestHarness.Candidates(candidate),
            info: info);

        Assert.Null(result);
        Assert.False(candidate.IsSelected);
    }

    [Fact]
    public async Task Build_WhenMappingIsMissing_PreservesPreviousRequest()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        var previous = new DownloadRequest { Id = "previous" };

        var result = await harness.Build(
            UpdateDLCInstallTestHarness.Candidates(UpdateDLCInstallTestHarness.Candidate()),
            info: harness.InstallInfo(useDefault: false),
            previous: previous);

        Assert.Same(previous, result);
    }

    [Fact]
    public async Task Build_WhenCandidatesAreEmpty_DoesNotCreateRequest()
    {
        using var harness = new UpdateDLCInstallTestHarness();

        var result = await harness.Build(new ObservableCollection<UpdateDLCCandidate>());

        Assert.Null(result);
    }

    [Fact]
    public async Task Build_WhenCandidatesAreEmpty_PreservesPreviousRequest()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        var previous = new DownloadRequest { Id = "previous" };

        var result = await harness.Build(new ObservableCollection<UpdateDLCCandidate>(), previous: previous);

        Assert.Same(previous, result);
    }

    [Fact]
    public async Task Build_AllMode_SelectsEveryUpdateCandidate()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        var first = UpdateDLCInstallTestHarness.Candidate(name: "First", ids: [10]);
        var second = UpdateDLCInstallTestHarness.Candidate(name: "Second", ids: [11]);

        var request = await harness.Build(UpdateDLCInstallTestHarness.Candidates(first, second));

        Assert.NotNull(request);
        Assert.True(first.IsSelected);
        Assert.True(second.IsSelected);
        Assert.Contains("Second", request.DisplayName);
    }

    [Fact]
    public async Task Build_AllMode_SelectsEveryDLCCandidate()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        var first = UpdateDLCInstallTestHarness.Candidate(name: "Expansion A", category: RomMCategory.DLC);
        var second = UpdateDLCInstallTestHarness.Candidate(name: "Expansion B", category: RomMCategory.DLC);

        var request = await harness.Build(
            UpdateDLCInstallTestHarness.Candidates(first, second),
            category: RomMCategory.DLC);

        Assert.NotNull(request);
        Assert.True(first.IsSelected);
        Assert.True(second.IsSelected);
    }

    [Fact]
    public async Task Build_InstalledCandidateIsSkipped()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        var installed = UpdateDLCInstallTestHarness.Candidate();
        installed.Status = InstallStatus.Installed;

        var request = await harness.Build(UpdateDLCInstallTestHarness.Candidates(installed));

        Assert.Null(request);
        Assert.True(installed.IsSelected);
    }

    [Fact]
    public async Task Build_WhenFirstCandidateIsInstalled_BuildsUninstalledCandidate()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        var installed = UpdateDLCInstallTestHarness.Candidate(name: "Old Patch");
        installed.Status = InstallStatus.Installed;
        var pending = UpdateDLCInstallTestHarness.Candidate(name: "New Patch");

        var request = await harness.Build(UpdateDLCInstallTestHarness.Candidates(installed, pending));

        Assert.NotNull(request);
        Assert.Contains("New Patch", request.DisplayName);
        Assert.True(installed.IsSelected);
        Assert.True(pending.IsSelected);
    }

    [Fact]
    public async Task Build_WhenLastCandidateIsInstalled_ReturnsLastPendingRequest()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        var pending = UpdateDLCInstallTestHarness.Candidate(name: "New Patch");
        var installed = UpdateDLCInstallTestHarness.Candidate(name: "Old Patch");
        installed.Status = InstallStatus.Installed;

        var request = await harness.Build(UpdateDLCInstallTestHarness.Candidates(pending, installed));

        Assert.NotNull(request);
        Assert.Contains("New Patch", request.DisplayName);
    }

    [Fact]
    public async Task Build_RequestUsesEncodedCandidateNameAndOrderedFileIDs()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        var candidate = UpdateDLCInstallTestHarness.Candidate(name: "Patch #1 & extras", ids: [6, 3, 9]);

        var request = await harness.Build(UpdateDLCInstallTestHarness.Candidates(candidate));

        Assert.NotNull(request);
        Assert.Equal("/api/roms/42/content/Patch%20%231%20%26%20extras?file_ids=6,3,9", request.DownloadUrl);
    }

    [Fact]
    public async Task Build_DLCRequestUsesSameRomMContentEndpoint()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        var candidate = UpdateDLCInstallTestHarness.Candidate(name: "Expansion", category: RomMCategory.DLC, ids: [91, 92]);

        var request = await harness.Build(
            UpdateDLCInstallTestHarness.Candidates(candidate),
            category: RomMCategory.DLC);

        Assert.NotNull(request);
        Assert.Equal("/api/roms/42/content/Expansion?file_ids=91,92", request.DownloadUrl);
    }

    [Fact]
    public async Task Build_RequestUsesSanitizedCandidateFileName()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        var candidate = UpdateDLCInstallTestHarness.Candidate(fileName: "patch.zip");

        var request = await harness.Build(UpdateDLCInstallTestHarness.Candidates(candidate));

        Assert.NotNull(request);
        Assert.Equal("patch.zip", Path.GetFileName(request.DownloadPath));
        Assert.StartsWith(Path.Combine(harness.Root, "temp"), request.DownloadPath);
    }

    [Fact]
    public async Task Build_RequestIdMatchesItsDownloadDirectory()
    {
        using var harness = new UpdateDLCInstallTestHarness();

        var request = await harness.Build(
            UpdateDLCInstallTestHarness.Candidates(UpdateDLCInstallTestHarness.Candidate()));

        Assert.NotNull(request);
        Assert.True(Guid.TryParse(request.Id, out _));
        Assert.Equal(request.Id, Path.GetFileName(Path.GetDirectoryName(request.DownloadPath)));
    }

    [Fact]
    public async Task Build_RequestDisplayNameIncludesGameAndCandidate()
    {
        using var harness = new UpdateDLCInstallTestHarness();

        var request = await harness.Build(
            UpdateDLCInstallTestHarness.Candidates(UpdateDLCInstallTestHarness.Candidate(name: "Patch Two")));

        Assert.NotNull(request);
        Assert.Equal("Example Game - Patch Two", request.DisplayName);
    }

    [Fact]
    public async Task Build_FolderStyleDoesNotWaitForPreviousInstall()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        var previous = new DownloadRequest { Id = "previous" };

        var request = await harness.Build(
            UpdateDLCInstallTestHarness.Candidates(UpdateDLCInstallTestHarness.Candidate()),
            previous: previous);

        Assert.NotNull(request);
        Assert.Null(request.WaitForInstall);
        Assert.NotSame(previous, request);
    }

    [Fact]
    public async Task Build_CLIStyleWaitsForPreviousInstall()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        harness.Mapping.UpdateInstallStyle = InstallStyles.CLI;
        var previous = new DownloadRequest { Id = "previous" };

        var request = await harness.Build(
            UpdateDLCInstallTestHarness.Candidates(UpdateDLCInstallTestHarness.Candidate()),
            previous: previous);

        Assert.NotNull(request);
        Assert.Same(previous.InstallCompletion.Task, request.WaitForInstall);
    }

    [Fact]
    public async Task Build_DLCFolderStyleDoesNotWaitForPreviousInstall()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        var previous = new DownloadRequest { Id = "previous" };

        var request = await harness.Build(
            UpdateDLCInstallTestHarness.Candidates(UpdateDLCInstallTestHarness.Candidate(category: RomMCategory.DLC)),
            category: RomMCategory.DLC,
            previous: previous);

        Assert.NotNull(request);
        Assert.Null(request.WaitForInstall);
    }

    [Fact]
    public async Task Build_DLCCLIStyleWaitsForPreviousInstall()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        harness.Mapping.DLCInstallStyle = InstallStyles.CLI;
        var previous = new DownloadRequest { Id = "previous" };

        var request = await harness.Build(
            UpdateDLCInstallTestHarness.Candidates(UpdateDLCInstallTestHarness.Candidate(category: RomMCategory.DLC)),
            category: RomMCategory.DLC,
            previous: previous);

        Assert.NotNull(request);
        Assert.Same(previous.InstallCompletion.Task, request.WaitForInstall);
    }
}
