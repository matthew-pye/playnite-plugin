using Graviton.Install.Downloads;

using System.IO;
using System.IO.Compression;

using Xunit;

namespace Graviton.Tests.Install;

public sealed class ArchiveExtractorDetectionTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), "Graviton.Tests", Guid.NewGuid().ToString("N"));

    public ArchiveExtractorDetectionTests()
    {
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void IsFileCompressed_WithZipArchive_ReturnsTrue()
    {
        var archivePath = CreateArchive("game.zip");

        var result = ArchiveExtractor.IsFileCompressed(archivePath);

        Assert.True(result);
    }

    [Theory]
    [InlineData("game.iso")]
    [InlineData("game.ISO")]
    public void IsFileCompressed_WithIsoExtension_ReturnsFalse(string archiveName)
    {
        var archivePath = CreateArchive(archiveName);

        var result = ArchiveExtractor.IsFileCompressed(archivePath);

        Assert.False(result);
    }

    [Fact]
    public void IsFileCompressed_WithPlainTextFile_ReturnsFalse()
    {
        var filePath = Path.Combine(_testDirectory, "readme.txt");
        File.WriteAllText(filePath, "This is not an archive.");

        var result = ArchiveExtractor.IsFileCompressed(filePath);

        Assert.False(result);
    }

    private string CreateArchive(string archiveName)
    {
        var archivePath = Path.Combine(_testDirectory, archiveName);

        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("game.bin");
            using var entryStream = entry.Open();
            entryStream.Write(new byte[] { 0x00, 0xFF, 0x42, 0x80 });
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
