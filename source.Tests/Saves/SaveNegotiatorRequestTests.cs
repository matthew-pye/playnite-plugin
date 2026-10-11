using Graviton.Models;
using Graviton.Models.ROM;
using Graviton.Models.Saves;
using Graviton.Notifications;
using Graviton.Saves;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;
using Graviton.Tests.Fakes.RomM;

using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using Xunit;

namespace Graviton.Tests.Saves;

public sealed class SaveNegotiatorRequestTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Graviton.Tests", Guid.NewGuid().ToString("N"));
    private readonly GravitonContextFake _plugin = new();
    private readonly Guid _mappingId = Guid.NewGuid();

    public SaveNegotiatorRequestTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "temp"));
        Directory.CreateDirectory(Path.Combine(_root, "saves"));

        _plugin.ConfigurePluginDataDirectory(_root);
        _plugin.Settings.AccountState.DeviceID = "handheld-device";
        _plugin.Settings.Mappings.Add(new EmulatorMapping
        {
            MappingId = _mappingId,
            SavePath = Path.Combine(_root, "saves")
        });
    }

    [Fact]
    public void BuildNegotiate_WithNoROMs_ReturnsEmptyRequestWithDeviceID()
    {
        var request = CreateNegotiator().BuildNegotiate(new List<RomMRomLocal>());

        Assert.Equal("handheld-device", request.DeviceID);
        Assert.Empty(request.Saves);
    }

    [Fact]
    public void BuildNegotiate_WithNoTrackedSave_SkipsROM()
    {
        var rom = CreateROM(42);

        var request = CreateNegotiator().BuildNegotiate(new List<RomMRomLocal> { rom });

        Assert.Empty(request.Saves);
    }

    [Fact]
    public void BuildNegotiate_WhenMappingDoesNotExist_SkipsROM()
    {
        var rom = CreateROM(42, "slot1.sav");
        rom.MappingID = Guid.NewGuid();
        WriteSave("slot1.sav", "saved content");

        var request = CreateNegotiator().BuildNegotiate(new List<RomMRomLocal> { rom });

        Assert.Empty(request.Saves);
    }

    [Fact]
    public void BuildNegotiate_WithSingleFile_SendsRomM540FieldsAndExpectedHash()
    {
        var path = WriteSave("slot1.sav", "Graviton save data");
        var timestamp = new DateTime(2026, 10, 10, 12, 30, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, timestamp);
        var rom = CreateROM(42, "slot1.sav", "autosave");
        rom.LocalSave!.Filename = "slot1.sav";

        var request = CreateNegotiator().BuildNegotiate(new List<RomMRomLocal> { rom });

        var save = Assert.Single(request.Saves);
        Assert.Equal(42, save.ROMID);
        Assert.Equal("slot1.sav", save.FileName);
        Assert.Equal("autosave", save.Slot);
        Assert.Equal(new FileInfo(path).Length, save.FileSize);
        Assert.Equal(ExpectedHash(File.ReadAllBytes(path)), save.ContentHash);
        Assert.Equal(File.GetLastWriteTimeUtc(path), DateTime.Parse(save.UpdatedAt!).ToUniversalTime());
        Assert.Equal(save.ContentHash, rom.LocalSave.ContentHash);
        Assert.Equal(save.FileSize, rom.LocalSave.FileSize);
    }

    [Fact]
    public void BuildNegotiate_WhenFileChanges_UsesCurrentContentAndSize()
    {
        var path = WriteSave("slot1.sav", "old");
        var rom = CreateROM(42, "slot1.sav");
        var negotiator = CreateNegotiator();

        var first = Assert.Single(negotiator.BuildNegotiate(new List<RomMRomLocal> { rom }).Saves);
        var originalHash = first.ContentHash;
        var originalSize = first.FileSize;

        File.WriteAllText(path, "a much longer replacement save", Encoding.UTF8);

        var second = Assert.Single(negotiator.BuildNegotiate(new List<RomMRomLocal> { rom }).Saves);

        Assert.NotEqual(originalHash, second.ContentHash);
        Assert.True(second.FileSize > originalSize);
        Assert.Equal(second.ContentHash, rom.LocalSave!.ContentHash);
        Assert.Equal(second.FileSize, rom.LocalSave.FileSize);
    }

    [Fact]
    public void BuildNegotiate_WithMultipleROMs_PreservesTheirIDsAndSlots()
    {
        WriteSave("first.sav", "first");
        WriteSave("second.sav", "second");
        var first = CreateROM(42, "first.sav", "autosave");
        var second = CreateROM(73, "second.sav", "autosave");

        var request = CreateNegotiator().BuildNegotiate(new List<RomMRomLocal> { first, second });

        Assert.Collection(request.Saves,
            save =>
            {
                Assert.Equal(42, save.ROMID);
                Assert.Equal("autosave", save.Slot);
                Assert.Equal("first.sav", save.FileName);
            },
            save =>
            {
                Assert.Equal(73, save.ROMID);
                Assert.Equal("autosave", save.Slot);
                Assert.Equal("second.sav", save.FileName);
            });
    }

    [Fact]
    public void BuildNegotiate_WithArchivalSlot_PreservesNullSlot()
    {
        WriteSave("backup.sav", "archived save");
        var rom = CreateROM(42, "backup.sav", slot: null);

        var save = Assert.Single(CreateNegotiator().BuildNegotiate(new List<RomMRomLocal> { rom }).Saves);

        Assert.Null(save.Slot);
        Assert.Equal("backup.sav", save.FileName);
    }

    [Fact]
    public void BuildNegotiate_WithSelectedDirectory_CreatesPackedMetadataAndRemovesTemporaryArchive()
    {
        WriteSave("game/slot1.sav", "first file");
        WriteSave("game/settings/profile.sav", "second file");
        var rom = CreateROM(42, "game", "autosave");
        rom.LocalSave!.Filename = "packed.zip";

        var save = Assert.Single(CreateNegotiator().BuildNegotiate(new List<RomMRomLocal> { rom }).Saves);

        Assert.Equal("packed.zip", save.FileName);
        Assert.True(save.FileSize > 0);
        Assert.NotNull(save.ContentHash);
        Assert.Equal(32, save.ContentHash.Length);
        Assert.Equal(save.ContentHash, rom.LocalSave.ContentHash);
        Assert.False(File.Exists(Path.Combine(_root, "temp", "packed.zip")));
    }

    [Fact]
    public void BuildNegotiate_WithMultipleFiles_UsesNewestFileTimestamp()
    {
        var firstPath = WriteSave("first.sav", "first file");
        var secondPath = WriteSave("second.sav", "second file");
        var firstTime = new DateTime(2026, 10, 9, 9, 0, 0, DateTimeKind.Utc);
        var secondTime = new DateTime(2026, 10, 10, 14, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(firstPath, firstTime);
        File.SetLastWriteTimeUtc(secondPath, secondTime);
        var rom = CreateROM(42, "first.sav");
        rom.LocalSave!.Filename = "selected.zip";
        rom.LocalSave.SourceFilePaths.Add(EmulatorMapping.SavePathToken + Path.DirectorySeparatorChar + "second.sav");

        var save = Assert.Single(CreateNegotiator().BuildNegotiate(new List<RomMRomLocal> { rom }).Saves);

        Assert.Equal("selected.zip", save.FileName);
        Assert.Equal(File.GetLastWriteTimeUtc(secondPath), DateTime.Parse(save.UpdatedAt!).ToUniversalTime());
        Assert.False(File.Exists(Path.Combine(_root, "temp", "selected.zip")));
    }

    [Fact]
    public void BuildNegotiate_WhenMissingFilesRemain_SkipsSyncWithoutChangingTheSave()
    {
        var rom = CreateROM(42, "missing.sav");
        rom.LocalSave!.Status = SaveStatus.MissingFiles;
        rom.LocalSave.MissingFiles.Add(Path.Combine(_root, "saves", "missing.sav"));

        var request = CreateNegotiator().BuildNegotiate(new List<RomMRomLocal> { rom });

        Assert.Empty(request.Saves);
        Assert.Equal(SaveStatus.MissingFiles, rom.LocalSave.Status);
        Assert.Single(rom.LocalSave.MissingFiles);
    }

    [Fact]
    public void BuildNegotiate_WhenMissingFileReturns_ClearsMissingFilesAndIncludesSave()
    {
        var path = WriteSave("restored.sav", "restored contents");
        var rom = CreateROM(42, "restored.sav");
        rom.LocalSave!.Status = SaveStatus.MissingFiles;
        rom.LocalSave.MissingFiles.Add(path);

        var request = CreateNegotiator().BuildNegotiate(new List<RomMRomLocal> { rom });

        Assert.Single(request.Saves);
        Assert.Empty(rom.LocalSave.MissingFiles);
        Assert.Equal(ExpectedHash(File.ReadAllBytes(path)), request.Saves[0].ContentHash);
    }

    [Fact]
    public void BuildNegotiate_WhenOneROMHasNoMapping_IncludesTheOther()
    {
        WriteSave("valid.sav", "valid");
        WriteSave("ignored.sav", "ignored");
        var valid = CreateROM(42, "valid.sav");
        var ignored = CreateROM(73, "ignored.sav");
        ignored.MappingID = Guid.NewGuid();

        var request = CreateNegotiator().BuildNegotiate(new List<RomMRomLocal> { ignored, valid });

        Assert.Equal(42, Assert.Single(request.Saves).ROMID);
    }

    private SaveNegotiator CreateNegotiator()
    {
        var playnite = new PlayniteFake();
        var romm = new RomMServerFake();
        var logger = new GravitonLogger(_plugin.Settings);

        return new SaveNegotiator(_plugin.Context, playnite.Api, logger, romm.Server);
    }

    private RomMRomLocal CreateROM(int id, string? relativeFile = null, string? slot = "autosave")
    {
        return new RomMRomLocal
        {
            Id = id,
            MappingID = _mappingId,
            Name = $"Game {id}",
            LocalSave = relativeFile is null ? null : new GravitonSave
            {
                ROMID = id,
                Filename = relativeFile,
                Slot = slot,
                SourceFilePaths = new ObservableCollection<string>
                {
                    EmulatorMapping.SavePathToken + Path.DirectorySeparatorChar + relativeFile
                }
            }
        };
    }

    private string WriteSave(string relativePath, string content)
    {
        var path = Path.Combine(_root, "saves", relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }

    private static string ExpectedHash(byte[] bytes)
    {
        return Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
