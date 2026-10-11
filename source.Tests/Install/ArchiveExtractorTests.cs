using Graviton.Install.Downloads;

using System.IO;
using System.IO.Compression;

using Xunit;

namespace Graviton.Tests.Install;

public sealed class ArchiveExtractorTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), "Graviton.Tests", Guid.NewGuid().ToString("N"));

    public ArchiveExtractorTests()
    {
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void ExtractArchive_WithNestedFile_PreservesBinaryContent()
    {
        var originalBytes = new byte[] { 0x00, 0xFF, 0x42, 0x80 };
        var archivePath = CreateArchive("game.zip", "Game/Updates/patch.bin", originalBytes);
        var installDirectory = Path.Combine(_testDirectory, "installed");

        ArchiveExtractor.ExtractArchive(archivePath, installDirectory, CancellationToken.None);

        var extractedPath = Path.Combine(installDirectory, "Game", "Updates", "patch.bin");

        Assert.True(File.Exists(extractedPath));
        Assert.Equal(originalBytes, File.ReadAllBytes(extractedPath));
    }

    [Fact]
    public void ExtractArchive_WhenCancelledBeforeExtraction_DoesNotCreateFiles()
    {
        var archivePath = CreateArchive("game.zip", "Game/patch.bin", new byte[] { 1, 2, 3 });
        var installDirectory = Path.Combine(_testDirectory, "installed");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        
        Assert.Throws<OperationCanceledException>(() =>
        {
            ArchiveExtractor.ExtractArchive(archivePath, installDirectory, cancellation.Token);
        });

        Assert.False(File.Exists(Path.Combine(installDirectory, "Game", "patch.bin")));
    }

    [Fact]
    public void CleanupCreatedFiles_RemovesNewFileAndEmptyDirectory()
    {
        var newDirectory = Path.Combine(_testDirectory, "installed", "Updates");
        Directory.CreateDirectory(newDirectory);

        var newFile = Path.Combine(newDirectory, "partial.bin");
        File.WriteAllBytes(newFile, new byte[] { 1, 2, 3 });

        ArchiveExtractor.CleanupCreatedFiles([newFile]);

        Assert.False(File.Exists(newFile));
        Assert.False(Directory.Exists(newDirectory));
    }

    [Fact]
    public void CleanupCreatedFiles_PreservesFilesNotIncludedInCleanup()
    {
        var installDirectory = Path.Combine(_testDirectory, "installed");
        Directory.CreateDirectory(installDirectory);

        var newFile = Path.Combine(installDirectory, "partial.bin");
        var existingFile = Path.Combine(installDirectory, "existing.sav");
        var existingBytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

        File.WriteAllBytes(newFile, new byte[] { 1, 2, 3 });
        File.WriteAllBytes(existingFile, existingBytes);

        ArchiveExtractor.CleanupCreatedFiles([newFile]);

        Assert.False(File.Exists(newFile));
        Assert.True(Directory.Exists(installDirectory));
        Assert.Equal(existingBytes, File.ReadAllBytes(existingFile));
    }

    private string CreateArchive(string archiveName, string entryName, byte[] content)
    {
        var archivePath = Path.Combine(_testDirectory, archiveName);

        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry(entryName);
            using var entryStream = entry.Open();
            entryStream.Write(content);
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
