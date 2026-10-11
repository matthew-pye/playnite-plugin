using Graviton.Install.Downloads;

using System.IO;
using System.IO.Compression;

using Xunit;

namespace Graviton.Tests.Install;

public sealed class ArchiveExtractorSafetyTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), "Graviton.Tests", Guid.NewGuid().ToString("N"));

    public ArchiveExtractorSafetyTests()
    {
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void ExtractArchive_WithExistingUnrelatedFile_PreservesItsContent()
    {
        var installDirectory = Path.Combine(_testDirectory, "installed");
        var existingPath = Path.Combine(installDirectory, "Game", "Saves", "checkpoint.sav");
        var existingBytes = new byte[] { 0x00, 0xFF, 0x42, 0x80 };
        var patchBytes = new byte[] { 0x10, 0x20, 0x30 };

        Directory.CreateDirectory(Path.GetDirectoryName(existingPath)!);
        File.WriteAllBytes(existingPath, existingBytes);

        var archivePath = CreateArchive("update.zip", ("Game/Updates/patch.bin", patchBytes));

        ArchiveExtractor.ExtractArchive(archivePath, installDirectory, TestContext.Current.CancellationToken);

        Assert.Equal(existingBytes, File.ReadAllBytes(existingPath));
        Assert.Equal(patchBytes, File.ReadAllBytes(Path.Combine(installDirectory, "Game", "Updates", "patch.bin")));
    }

    [Fact]
    public void ExtractArchive_WithMultipleEntries_ExtractsAllFiles()
    {
        var installDirectory = Path.Combine(_testDirectory, "installed");
        var rootBytes = new byte[] { 0x01, 0x02 };
        var nestedBytes = new byte[] { 0xAA, 0xBB, 0xCC };
        var archivePath = CreateArchive(
            "game.zip",
            ("readme.txt", rootBytes),
            ("Game/Updates/patch.bin", nestedBytes));

        ArchiveExtractor.ExtractArchive(archivePath, installDirectory, TestContext.Current.CancellationToken);

        Assert.Equal(rootBytes, File.ReadAllBytes(Path.Combine(installDirectory, "readme.txt")));
        Assert.Equal(nestedBytes, File.ReadAllBytes(Path.Combine(installDirectory, "Game", "Updates", "patch.bin")));
    }

    [Fact]
    public void ExtractArchive_WithParentTraversalEntry_DoesNotWriteOutsideInstallDirectory()
    {
        var installDirectory = Path.Combine(_testDirectory, "installed");
        var outsidePath = Path.Combine(_testDirectory, "escaped.bin");
        var archivePath = CreateArchive("unsafe.zip", ("../escaped.bin", new byte[] { 0x01, 0x02 }));

        _ = Record.Exception(() =>
        {
            ArchiveExtractor.ExtractArchive(archivePath, installDirectory, TestContext.Current.CancellationToken);
        });

        Assert.False(File.Exists(outsidePath));
    }

    private string CreateArchive(string archiveName, params (string EntryName, byte[] Content)[] entries)
    {
        var archivePath = Path.Combine(_testDirectory, archiveName);

        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            foreach (var entryData in entries)
            {
                var entry = archive.CreateEntry(entryData.EntryName);
                using var entryStream = entry.Open();
                entryStream.Write(entryData.Content);
            }
        }

        return archivePath;
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}
