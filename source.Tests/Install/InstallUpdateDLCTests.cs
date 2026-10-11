using Graviton.Install;
using Graviton.Models.Install;
using Graviton.Models.RomM.Rom;

using System.Collections.ObjectModel;

using Xunit;

namespace Graviton.Tests.Install;

public sealed class InstallUpdateDLCTests
{
    [Fact]
    public void MergeCandidates_WhenCandidateStillExists_PreservesInstalledInformation()
    {
        var existingCandidate = new UpdateDLCCandidate("Patch 1", "patch.zip", RomMCategory.Update, [10, 11], "Updates")
        {
            InstalledFileIDs = [10, 11],
            InstalledTopPaths = [@"C:\Games\Updates\Patch 1"],
            InstalledSize = 2048,
            PreviousInstallStyle = InstallStyles.MappedFolder
        };
        var existing = new ObservableCollection<UpdateDLCCandidate> { existingCandidate };
        var discoveredCandidate = new UpdateDLCCandidate("PATCH 1", "patch.zip", RomMCategory.Update, [10, 11], "updates");
        var discovered = new List<UpdateDLCCandidate> { discoveredCandidate };

        InstallUpdateDLC.MergeCandidates(existing, discovered, RomMCategory.Update);

        var result = Assert.Single(existing);
        Assert.Same(discoveredCandidate, result);
        Assert.Equal(InstallStatus.Installed, result.Status);
        Assert.Equal(new[] { 10, 11 }, result.InstalledFileIDs);
        Assert.Equal(new[] { @"C:\Games\Updates\Patch 1" }, result.InstalledTopPaths);
        Assert.Equal(2048, result.InstalledSize);
        Assert.Equal(InstallStyles.MappedFolder, result.PreviousInstallStyle);
    }

    [Fact]
    public void MergeCandidates_WhenNewFilesAreDiscovered_MarksPartialInstallation()
    {
        var previous = new UpdateDLCCandidate("Patch 1", "patch.zip", RomMCategory.Update, [10, 11])
        {
            InstalledFileIDs = [10, 11],
            InstalledSize = 2048
        };
        var existing = new ObservableCollection<UpdateDLCCandidate> { previous };
        var discoveredCandidate = new UpdateDLCCandidate("Patch 1", "patch.zip", RomMCategory.Update, [10, 11, 12]);

        InstallUpdateDLC.MergeCandidates(existing, [discoveredCandidate], RomMCategory.Update);

        var result = Assert.Single(existing);
        Assert.Equal(InstallStatus.PartialInstalled, result.Status);
        Assert.Equal(new[] { 10, 11 }, result.InstalledFileIDs);
        Assert.Equal(new[] { 10, 11, 12 }, result.FileIDs);
    }

    [Fact]
    public void MergeCandidates_WhenInstalledFilesNoLongerMatch_MarksNotInstalled()
    {
        var previous = new UpdateDLCCandidate("Patch 1", "patch.zip", RomMCategory.Update, [10])
        {
            InstalledFileIDs = [10]
        };
        var existing = new ObservableCollection<UpdateDLCCandidate> { previous };
        var discoveredCandidate = new UpdateDLCCandidate("Patch 1", "patch.zip", RomMCategory.Update, [12]);

        InstallUpdateDLC.MergeCandidates(existing, [discoveredCandidate], RomMCategory.Update);

        var result = Assert.Single(existing);
        Assert.Equal(InstallStatus.NotInstalled, result.Status);
        Assert.Equal(new[] { 10 }, result.InstalledFileIDs);
    }

    [Fact]
    public void MergeCandidates_WhenCandidateIsRenamed_MatchesByFileId()
    {
        var previous = new UpdateDLCCandidate("Original Name", "patch.zip", RomMCategory.Update, [10])
        {
            InstalledFileIDs = [10]
        };
        var existing = new ObservableCollection<UpdateDLCCandidate> { previous };
        var discoveredCandidate = new UpdateDLCCandidate("New Name", "patch.zip", RomMCategory.Update, [10]);

        InstallUpdateDLC.MergeCandidates(existing, [discoveredCandidate], RomMCategory.Update);

        var result = Assert.Single(existing);
        Assert.Equal("New Name", result.Name);
        Assert.Equal(InstallStatus.Installed, result.Status);
        Assert.Equal(new[] { 10 }, result.InstalledFileIDs);
    }

    [Fact]
    public void MergeCandidates_WhenOldCandidateDisappears_ReplacesItWithDiscoveredCandidates()
    {
        var previous = new UpdateDLCCandidate("Old DLC", "old.zip", RomMCategory.DLC, [10])
        {
            InstalledFileIDs = [10]
        };
        var existing = new ObservableCollection<UpdateDLCCandidate> { previous };
        var discoveredCandidate = new UpdateDLCCandidate("New DLC", "new.zip", RomMCategory.DLC, [20]);

        InstallUpdateDLC.MergeCandidates(existing, [discoveredCandidate], RomMCategory.DLC);

        var result = Assert.Single(existing);
        Assert.Same(discoveredCandidate, result);
        Assert.Equal(InstallStatus.NotInstalled, result.Status);
        Assert.Empty(result.InstalledFileIDs);
    }

    [Fact]
    public void MergeCandidates_WhenRemoteFilesAreRemoved_KeepsRemainingCandidateInstalled()
    {
        var previous = new UpdateDLCCandidate("Patch 1", "patch.zip", RomMCategory.Update, [10, 11])
        {
            InstalledFileIDs = [10, 11],
            InstalledTopPaths = [@"C:\Games\Updates\Patch 1"],
            InstalledSize = 2048
        };
        var existing = new ObservableCollection<UpdateDLCCandidate> { previous };
        var discoveredCandidate = new UpdateDLCCandidate("Patch 1", "patch.zip", RomMCategory.Update, [10]);

        InstallUpdateDLC.MergeCandidates(existing, [discoveredCandidate], RomMCategory.Update);

        var result = Assert.Single(existing);
        Assert.Equal(InstallStatus.Installed, result.Status);
        Assert.Equal(new[] { 10, 11 }, result.InstalledFileIDs);
        Assert.Equal(new[] { @"C:\Games\Updates\Patch 1" }, result.InstalledTopPaths);
    }

    [Fact]
    public void MergeCandidates_WhenRootsDifferAndFilesDoNotOverlap_DoesNotReuseInstallation()
    {
        var previous = new UpdateDLCCandidate("Patch 1", "patch.zip", RomMCategory.Update, [10], "Updates/Version1")
        {
            InstalledFileIDs = [10],
            InstalledTopPaths = [@"C:\Games\Updates\Version1"],
            InstalledSize = 1024
        };
        var existing = new ObservableCollection<UpdateDLCCandidate> { previous };
        var discoveredCandidate = new UpdateDLCCandidate("Patch 1", "patch.zip", RomMCategory.Update, [20], "Updates/Version2");

        InstallUpdateDLC.MergeCandidates(existing, [discoveredCandidate], RomMCategory.Update);

        var result = Assert.Single(existing);
        Assert.Same(discoveredCandidate, result);
        Assert.Equal(InstallStatus.NotInstalled, result.Status);
        Assert.Empty(result.InstalledFileIDs);
        Assert.Empty(result.InstalledTopPaths);
        Assert.Equal(0, result.InstalledSize);
    }

    [Fact]
    public void MergeCandidates_WhenNoCandidatesAreDiscovered_ClearsExistingCandidates()
    {
        var existing = new ObservableCollection<UpdateDLCCandidate>
        {
            new UpdateDLCCandidate("Patch 1", "patch.zip", RomMCategory.Update, [10]),
            new UpdateDLCCandidate("Patch 2", "patch2.zip", RomMCategory.Update, [11])
        };

        InstallUpdateDLC.MergeCandidates(existing, [], RomMCategory.Update);

        Assert.Empty(existing);
    }
}
