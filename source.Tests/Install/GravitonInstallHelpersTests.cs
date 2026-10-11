using Graviton.Install;

using System.IO;

using Xunit;

namespace Graviton.Tests.Install;

public sealed class GravitonInstallHelpersTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), "Graviton.Tests", Guid.NewGuid().ToString("N"));

    public GravitonInstallHelpersTests()
    {
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void GetInstalledTopPaths_WithFilesAndDirectories_ReturnsOnlyTopLevelPaths()
    {
        var sourceDirectory = Path.Combine(_testDirectory, "source");
        var destinationDirectory = Path.Combine(_testDirectory, "installed");
        var nestedDirectory = Path.Combine(sourceDirectory, "Updates", "Nested");

        Directory.CreateDirectory(nestedDirectory);
        File.WriteAllBytes(Path.Combine(sourceDirectory, "game.bin"), [0x00, 0xFF]);
        File.WriteAllBytes(Path.Combine(nestedDirectory, "patch.bin"), [0x12, 0x34]);

        var result = GravitonInstallHelpers.GetInstalledTopPaths(sourceDirectory, destinationDirectory);

        Assert.Equal(2, result.Count);
        Assert.Contains(Path.Combine(destinationDirectory, "Updates"), result);
        Assert.Contains(Path.Combine(destinationDirectory, "game.bin"), result);
        Assert.DoesNotContain(Path.Combine(destinationDirectory, "Updates", "Nested"), result);
        Assert.False(Directory.Exists(destinationDirectory));
    }

    [Fact]
    public void GetInstalledTopPaths_WithEmptySource_ReturnsEmptyList()
    {
        var sourceDirectory = Path.Combine(_testDirectory, "empty");
        var destinationDirectory = Path.Combine(_testDirectory, "installed");
        Directory.CreateDirectory(sourceDirectory);

        var result = GravitonInstallHelpers.GetInstalledTopPaths(sourceDirectory, destinationDirectory);

        Assert.Empty(result);
    }

    [Fact]
    public void SanitizeString_WithInvalidFilenameCharacters_RemovesThemAndTrimsWhitespace()
    {
        var result = GravitonInstallHelpers.SanitizeString("  Game: Name?.zip  ");

        Assert.Equal("Game Name.zip", result);
    }

    [Fact]
    public void SanitizeString_WithPathSeparators_RemovesThem()
    {
        var result = GravitonInstallHelpers.SanitizeString(@"Game\DLC/patch.bin");

        Assert.Equal("GameDLCpatch.bin", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t ")]
    public void SanitizeString_WithEmptyOrWhitespaceValue_ReturnsUnnamed(string value)
    {
        var result = GravitonInstallHelpers.SanitizeString(value);

        Assert.Equal("Unnamed", result);
    }

    [Fact]
    public void SanitizeString_WithValidUnicodeFilename_PreservesIt()
    {
        var result = GravitonInstallHelpers.SanitizeString("Pokémon Édition [USA].zip");

        Assert.Equal("Pokémon Édition [USA].zip", result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}
