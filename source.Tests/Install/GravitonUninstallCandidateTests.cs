using Graviton.Install;
using Graviton.Install.Downloads;
using Graviton.Models.Install;
using Graviton.Models.RomM.Rom;

using System.IO;

using Xunit;

namespace Graviton.Tests.Install;

public sealed class GravitonUninstallCandidateTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "Graviton.Tests",
        Guid.NewGuid().ToString("N"));

    public GravitonUninstallCandidateTests()
    {
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task UninstallCandidate_WithTrackedFile_PreservesUnrelatedFiles()
    {
        var installDirectory = Path.Combine(_testDirectory, "Installed");
        Directory.CreateDirectory(installDirectory);

        var updatePath = Path.Combine(installDirectory, "patch.bin");
        var savePath = Path.Combine(installDirectory, "save.dat");
        var saveBytes = new byte[] { 0x00, 0xFF, 0x42, 0x80 };

        File.WriteAllBytes(updatePath, [1, 2, 3]);
        File.WriteAllBytes(savePath, saveBytes);

        var candidate = CreateInstalledCandidate(updatePath);

        await GravitonUninstallController.UninstallCandidate(candidate);

        Assert.False(File.Exists(updatePath));
        Assert.Equal(saveBytes, File.ReadAllBytes(savePath));
        Assert.True(Directory.Exists(installDirectory));
        AssertCandidateUninstalled(candidate);
    }

    [Fact]
    public async Task UninstallCandidate_WithTrackedDirectory_RemovesNestedFilesButPreservesSiblingDirectory()
    {
        var updateDirectory = Path.Combine(_testDirectory, "Updates", "Patch 1");
        var saveDirectory = Path.Combine(_testDirectory, "Updates", "Saves");
        Directory.CreateDirectory(Path.Combine(updateDirectory, "Nested"));
        Directory.CreateDirectory(saveDirectory);

        File.WriteAllBytes(Path.Combine(updateDirectory, "Nested", "patch.bin"), [1, 2, 3]);
        var savePath = Path.Combine(saveDirectory, "save.dat");
        File.WriteAllBytes(savePath, [4, 5, 6]);

        var candidate = CreateInstalledCandidate(updateDirectory);

        await GravitonUninstallController.UninstallCandidate(candidate);

        Assert.False(Directory.Exists(updateDirectory));
        Assert.Equal(new byte[] { 4, 5, 6 }, File.ReadAllBytes(savePath));
        AssertCandidateUninstalled(candidate);
    }

    [Fact]
    public async Task UninstallCandidate_WithMultipleTrackedPaths_RemovesAllOfThem()
    {
        var firstPath = Path.Combine(_testDirectory, "patch.bin");
        var secondPath = Path.Combine(_testDirectory, "metadata.bin");
        var unrelatedPath = Path.Combine(_testDirectory, "game.rom");

        File.WriteAllBytes(firstPath, [1]);
        File.WriteAllBytes(secondPath, [2]);
        File.WriteAllBytes(unrelatedPath, [3]);

        var candidate = CreateInstalledCandidate(firstPath, secondPath);

        await GravitonUninstallController.UninstallCandidate(candidate);

        Assert.False(File.Exists(firstPath));
        Assert.False(File.Exists(secondPath));
        Assert.Equal(new byte[] { 3 }, File.ReadAllBytes(unrelatedPath));
        AssertCandidateUninstalled(candidate);
    }

    [Fact]
    public async Task UninstallCandidate_WhenTrackedPathIsMissing_ClearsInstallationMetadata()
    {
        var missingPath = Path.Combine(_testDirectory, "missing-patch.bin");
        var candidate = CreateInstalledCandidate(missingPath);

        await GravitonUninstallController.UninstallCandidate(candidate);

        Assert.False(File.Exists(missingPath));
        AssertCandidateUninstalled(candidate);
    }

    private static UpdateDLCCandidate CreateInstalledCandidate(params string[] installedPaths)
    {
        return new UpdateDLCCandidate("Patch 1", "patch.zip", RomMCategory.Update, [10, 11])
        {
            InstalledTopPaths = [.. installedPaths],
            InstalledFileIDs = [10, 11],
            InstalledSize = 1024,
            Status = InstallStatus.Installed
        };
    }

    private static void AssertCandidateUninstalled(UpdateDLCCandidate candidate)
    {
        Assert.Empty(candidate.InstalledTopPaths);
        Assert.Empty(candidate.InstalledFileIDs);
        Assert.Equal(0, candidate.InstalledSize);
        Assert.Equal(InstallStatus.NotInstalled, candidate.Status);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}
