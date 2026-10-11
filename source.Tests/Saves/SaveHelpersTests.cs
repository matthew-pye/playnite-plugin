using Graviton.Models;
using Graviton.Saves;

using System.IO;
using System.IO.Compression;
using System.Collections.ObjectModel;
using System.Text;

using Xunit;

namespace Graviton.Tests.Saves;

public sealed class SaveHelpersTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), "Graviton.Tests", Guid.NewGuid().ToString("N"));

    public SaveHelpersTests()
    {
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void ComputeFileContentHash_WithKnownContent_ReturnsExpectedHash()
    {
        var filePath = Path.Combine(_testDirectory, "slot1.sav");
        File.WriteAllText(filePath, "Graviton save data");

        var hash = SaveHelpers.ComputeFileContentHash(filePath);

        Assert.Equal("db1dff83ed462f13b961967f43cb85e2", hash);
    }

    [Fact]
    public void ComputeFileContentHash_WhenContentChanges_ReturnsDifferentHash()
    {
        var filePath = Path.Combine(_testDirectory, "slot1.sav");
        File.WriteAllText(filePath, "original save");
        var originalHash = SaveHelpers.ComputeFileContentHash(filePath);

        File.WriteAllText(filePath, "updated save");
        var updatedHash = SaveHelpers.ComputeFileContentHash(filePath);

        Assert.NotNull(originalHash);
        Assert.NotNull(updatedHash);
        Assert.NotEqual(originalHash, updatedHash);
    }

    [Fact]
    public void ComputePackedContentHash_WithKnownEntries_ReturnsExpectedHash()
    {
        var archivePath = CreateArchive(
            "saves.zip",
            ("slot1.sav", "first save"),
            ("nested/slot2.sav", "second save"));

        var hash = SaveHelpers.ComputePackedContentHash(archivePath);

        Assert.Equal("8d3fdbca2883d6e3f5c07c5764e1133b", hash);
    }

    [Fact]
    public void ComputePackedContentHash_WhenEntriesAreReordered_ReturnsSameHash()
    {
        var firstArchivePath = CreateArchive(
            "first.zip",
            ("slot1.sav", "first save"),
            ("nested/slot2.sav", "second save"));

        var secondArchivePath = CreateArchive(
            "second.zip",
            ("nested/slot2.sav", "second save"),
            ("slot1.sav", "first save"));

        var firstHash = SaveHelpers.ComputePackedContentHash(firstArchivePath);
        var secondHash = SaveHelpers.ComputePackedContentHash(secondArchivePath);

        Assert.NotNull(firstHash);
        Assert.Equal(firstHash, secondHash);
    }

    [Fact]
    public void ComputePackedContentHash_WhenEntryContentChanges_ReturnsDifferentHash()
    {
        var firstArchivePath = CreateArchive(
            "first.zip",
            ("slot1.sav", "original save"));

        var secondArchivePath = CreateArchive(
            "second.zip",
            ("slot1.sav", "updated save"));

        var firstHash = SaveHelpers.ComputePackedContentHash(firstArchivePath);
        var secondHash = SaveHelpers.ComputePackedContentHash(secondArchivePath);

        Assert.NotNull(firstHash);
        Assert.NotNull(secondHash);
        Assert.NotEqual(firstHash, secondHash);
    }

    [Fact]
    public void PackSave_WithSingleFile_PreservesBinaryContent()
    {
        var saveRoot = Path.Combine(_testDirectory, "saves");
        Directory.CreateDirectory(saveRoot);

        var savePath = Path.Combine(saveRoot, "slot1.sav");
        var originalBytes = new byte[] { 0x00, 0xFF, 0x42, 0x80 };
        File.WriteAllBytes(savePath, originalBytes);

        var archivePath = Path.Combine(_testDirectory, "packed.zip");
        var selectedPaths = new ObservableCollection<string>
        {
            EmulatorMapping.SavePathToken + Path.DirectorySeparatorChar + "slot1.sav"
        };

        var packed = SaveHelpers.PackSave(selectedPaths, saveRoot, archivePath, out var skippedPaths);

        Assert.True(packed);
        Assert.Empty(skippedPaths);

        using var archive = ZipFile.OpenRead(archivePath);
        var entry = Assert.Single(archive.Entries);
        Assert.Equal("slot1.sav", entry.FullName);

        using var stream = entry.Open();
        using var content = new MemoryStream();
        stream.CopyTo(content);

        Assert.Equal(originalBytes, content.ToArray());
    }

    [Fact]
    public void PackSave_WithSelectedDirectory_IncludesNestedFiles()
    {
        var saveRoot = Path.Combine(_testDirectory, "saves");
        var nestedDirectory = Path.Combine(saveRoot, "game", "profiles");
        Directory.CreateDirectory(nestedDirectory);

        File.WriteAllText(Path.Combine(saveRoot, "game", "slot1.sav"), "main save");
        File.WriteAllText(Path.Combine(nestedDirectory, "profile.sav"), "profile save");

        var archivePath = Path.Combine(_testDirectory, "packed.zip");
        var selectedPaths = new ObservableCollection<string>
        {
            EmulatorMapping.SavePathToken + Path.DirectorySeparatorChar + "game"
        };

        var packed = SaveHelpers.PackSave(selectedPaths, saveRoot, archivePath, out var skippedPaths);

        Assert.True(packed);
        Assert.Empty(skippedPaths);

        using var archive = ZipFile.OpenRead(archivePath);
        var paths = archive.Entries.Select(entry => entry.FullName).ToArray();

        Assert.Equal(2, paths.Length);
        Assert.Contains("game/slot1.sav", paths);
        Assert.Contains("game/profiles/profile.sav", paths);
    }

    [Fact]
    public void UnpackSave_WithRootFile_RestoresFileAndDeletesArchive()
    {
        var archivePath = CreateArchive("download.zip", ("slot1.sav", "restored save"));
        var destinationPath = Path.Combine(_testDirectory, "restored");

        var restoredPaths = SaveHelpers.UnpackSave(archivePath, destinationPath);

        Assert.NotNull(restoredPaths);
        var restoredFile = Assert.Single(restoredPaths);
        Assert.Equal(Path.Combine(destinationPath, "slot1.sav"), restoredFile);
        Assert.Equal("restored save", File.ReadAllText(restoredFile));
        Assert.False(File.Exists(archivePath));
    }

    [Fact]
    public void UnpackSave_WithNestedFiles_ReturnsTopLevelFolder()
    {
        var archivePath = CreateArchive(
            "nested.zip",
            ("game/slot1.sav", "main save"),
            ("game/profiles/profile.sav", "profile save"));

        var destinationPath = Path.Combine(_testDirectory, "restored");

        var restoredPaths = SaveHelpers.UnpackSave(archivePath, destinationPath);

        Assert.NotNull(restoredPaths);
        var restoredFolder = Assert.Single(restoredPaths);
        Assert.Equal(Path.Combine(destinationPath, "game") + Path.DirectorySeparatorChar, restoredFolder);
        Assert.Equal("main save", File.ReadAllText(Path.Combine(destinationPath, "game", "slot1.sav")));
        Assert.Equal("profile save", File.ReadAllText(Path.Combine(destinationPath, "game", "profiles", "profile.sav")));
        Assert.False(File.Exists(archivePath));
    }

    [Fact]
    public void UnpackSave_WithRootAndNestedFiles_ReturnsFileAndFolder()
    {
        var archivePath = CreateArchive(
            "mixed.zip",
            ("slot1.sav", "root save"),
            ("game/slot2.sav", "nested save"));

        var destinationPath = Path.Combine(_testDirectory, "restored");

        var restoredPaths = SaveHelpers.UnpackSave(archivePath, destinationPath);

        Assert.NotNull(restoredPaths);
        Assert.Equal(2, restoredPaths.Count);
        Assert.Contains(Path.Combine(destinationPath, "slot1.sav"), restoredPaths);
        Assert.Contains(Path.Combine(destinationPath, "game") + Path.DirectorySeparatorChar, restoredPaths);
        Assert.Equal("root save", File.ReadAllText(Path.Combine(destinationPath, "slot1.sav")));
        Assert.Equal("nested save", File.ReadAllText(Path.Combine(destinationPath, "game", "slot2.sav")));
    }

    [Fact]
    public void UnpackSave_WhenSaveAlreadyExists_OverwritesExistingFile()
    {
        var archivePath = CreateArchive("updated.zip", ("slot1.sav", "updated save"));
        var destinationPath = Path.Combine(_testDirectory, "restored");
        Directory.CreateDirectory(destinationPath);

        var existingFile = Path.Combine(destinationPath, "slot1.sav");
        File.WriteAllText(existingFile, "old save");

        var restoredPaths = SaveHelpers.UnpackSave(archivePath, destinationPath);

        Assert.NotNull(restoredPaths);
        Assert.Equal(existingFile, Assert.Single(restoredPaths));
        Assert.Equal("updated save", File.ReadAllText(existingFile));
        Assert.False(File.Exists(archivePath));
    }

    [Fact]
    public void PackSave_WithSameFileNameInDifferentFolders_PreservesBothPaths()
    {
        var saveRoot = Path.Combine(_testDirectory, "saves");
        var firstDirectory = Path.Combine(saveRoot, "emulator1");
        var secondDirectory = Path.Combine(saveRoot, "emulator2");
        Directory.CreateDirectory(firstDirectory);
        Directory.CreateDirectory(secondDirectory);

        File.WriteAllText(Path.Combine(firstDirectory, "slot1.sav"), "first emulator");
        File.WriteAllText(Path.Combine(secondDirectory, "slot1.sav"), "second emulator");

        var selectedPaths = new ObservableCollection<string>
        {
            EmulatorMapping.SavePathToken + Path.DirectorySeparatorChar + "emulator1" + Path.DirectorySeparatorChar + "slot1.sav",
            EmulatorMapping.SavePathToken + Path.DirectorySeparatorChar + "emulator2" + Path.DirectorySeparatorChar + "slot1.sav"
        };

        var archivePath = Path.Combine(_testDirectory, "packed.zip");
        var packed = SaveHelpers.PackSave(selectedPaths, saveRoot, archivePath, out var skippedPaths);

        Assert.True(packed);
        Assert.Empty(skippedPaths);

        using var archive = ZipFile.OpenRead(archivePath);
        Assert.Equal(2, archive.Entries.Count);

        var firstEntry = archive.GetEntry("emulator1/slot1.sav");
        var secondEntry = archive.GetEntry("emulator2/slot1.sav");
        Assert.NotNull(firstEntry);
        Assert.NotNull(secondEntry);

        using (var firstReader = new StreamReader(firstEntry.Open()))
        {
            Assert.Equal("first emulator", firstReader.ReadToEnd());
        }

        using (var secondReader = new StreamReader(secondEntry.Open()))
        {
            Assert.Equal("second emulator", secondReader.ReadToEnd());
        }
    }

    [Fact]
    public void UnpackSave_WithBinaryFile_PreservesExactBytes()
    {
        var archivePath = Path.Combine(_testDirectory, "binary.zip");
        var originalBytes = new byte[] { 0x00, 0xFF, 0x80, 0x42, 0x0A };

        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("slot1.sav");
            using var stream = entry.Open();
            stream.Write(originalBytes);
        }

        var destinationPath = Path.Combine(_testDirectory, "restored");
        var restoredPaths = SaveHelpers.UnpackSave(archivePath, destinationPath);

        Assert.NotNull(restoredPaths);
        var restoredFile = Assert.Single(restoredPaths);
        Assert.Equal(Path.Combine(destinationPath, "slot1.sav"), restoredFile);
        Assert.Equal(originalBytes, File.ReadAllBytes(restoredFile));
        Assert.False(File.Exists(archivePath));
    }

    [Fact]
    public void UnpackSave_WhenDestinationHasUnrelatedFile_PreservesIt()
    {
        var archivePath = CreateArchive("download.zip", ("slot1.sav", "new save"));
        var destinationPath = Path.Combine(_testDirectory, "restored");
        Directory.CreateDirectory(destinationPath);

        var unrelatedFile = Path.Combine(destinationPath, "notes.txt");
        File.WriteAllText(unrelatedFile, "do not change");

        var restoredPaths = SaveHelpers.UnpackSave(archivePath, destinationPath);

        Assert.NotNull(restoredPaths);
        Assert.Equal(Path.Combine(destinationPath, "slot1.sav"), Assert.Single(restoredPaths));
        Assert.Equal("new save", File.ReadAllText(Path.Combine(destinationPath, "slot1.sav")));
        Assert.Equal("do not change", File.ReadAllText(unrelatedFile));
        Assert.False(File.Exists(archivePath));
    }

    private string CreateArchive(string fileName, params (string Name, string Content)[] entries)
    {
        var archivePath = Path.Combine(_testDirectory, fileName);

        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var stream = entry.Open();
                var bytes = Encoding.UTF8.GetBytes(content);
                stream.Write(bytes);
            }
        }

        return archivePath;
    }

    public void Dispose()
    {
        Directory.Delete(_testDirectory, recursive: true);
    }
}
