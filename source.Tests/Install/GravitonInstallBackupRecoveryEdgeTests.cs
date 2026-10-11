using Graviton.Install;
using Graviton.Models.Install;
using Graviton.Models.RomM.Rom;
using Graviton.Notifications;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;
using Graviton.Tests.Fakes.RomM;

using System.IO;
using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Install;

public sealed class GravitonInstallBackupRecoveryEdgeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Graviton.Tests", Guid.NewGuid().ToString("N"));
    private readonly GravitonInstallController _controller;

    public GravitonInstallBackupRecoveryEdgeTests()
    {
        Directory.CreateDirectory(_root);
        var plugin = new GravitonContextFake();
        var playnite = new PlayniteFake();
        var romm = new RomMServerFake();
        _controller = new GravitonInstallController(plugin.Context, playnite.Api, new GravitonLogger(plugin.Settings), romm.Server);
    }

    [Fact]
    public void ReadDownloadBackups_RetainsRemoteInstallRecoveryFields()
    {
        var backup = new DownloadRequestBackup
        {
            InstallType = InstallType.Remote,
            ID = "remote-request #9",
            GameID = "123",
            DownloadPath = "C:/Temp/pending download.zip",
            InstallDir = "D:/Emulators/PS2/Games",
            DownloadURL = "/api/roms/123/content/Game%20%231.zip?file_ids=21,22"
        };
        var path = Write("remote.tmp", JsonSerializer.Serialize(backup));

        var restored = Assert.Single(_controller.ReadDownloadBackups(_root));

        Assert.Equal(InstallType.Remote, restored.InstallType);
        Assert.Equal(backup.ID, restored.ID);
        Assert.Equal(backup.GameID, restored.GameID);
        Assert.Equal(backup.DownloadPath, restored.DownloadPath);
        Assert.Equal(backup.InstallDir, restored.InstallDir);
        Assert.Equal(backup.DownloadURL, restored.DownloadURL);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void ReadDownloadBackups_RetainsUpdateDlcCandidateStateForRecovery()
    {
        var candidate = new UpdateDLCCandidate("Costume Pack", "DLC.zip", RomMCategory.DLC, [20, 21],
            candidateRoot: "Content/Costumes", relativePath: "Costumes/pack.bin", size: 4096)
        {
            InstalledTopPaths = ["C:/Games/Costumes"],
            InstalledFileIDs = [20],
            InstalledSize = 1024,
            Status = InstallStatus.PartialInstalled,
            PreviousInstallStyle = InstallStyles.MappedFolder
        };
        var backup = new DownloadRequestBackup
        {
            InstallType = InstallType.UpdateDLC,
            ID = "download-request",
            GameID = "123",
            Category = RomMCategory.DLC,
            Candidate = candidate,
            InstallStyle = InstallStyles.MappedFolder,
            PreviousRequestId = "previous-download"
        };
        Write("dlc.tmp", JsonSerializer.Serialize(backup));

        var restored = Assert.Single(_controller.ReadDownloadBackups(_root));

        Assert.Equal(InstallType.UpdateDLC, restored.InstallType);
        Assert.Equal(RomMCategory.DLC, restored.Category);
        Assert.Equal(InstallStyles.MappedFolder, restored.InstallStyle);
        Assert.Equal("previous-download", restored.PreviousRequestId);
        var restoredCandidate = Assert.IsType<UpdateDLCCandidate>(restored.Candidate);
        Assert.Equal("Costume Pack", restoredCandidate.Name);
        Assert.Equal(new[] { 20, 21 }, restoredCandidate.FileIDs);
        Assert.Equal("Content/Costumes", restoredCandidate.CandidateRoot);
        Assert.Equal("Costumes/pack.bin", restoredCandidate.SingleFileRelativePath);
        Assert.Equal(4096, restoredCandidate.Size);
        Assert.Equal(InstallStatus.PartialInstalled, restoredCandidate.Status);
        Assert.Equal(InstallStyles.MappedFolder, restoredCandidate.PreviousInstallStyle);
        Assert.Equal(new[] { 20 }, restoredCandidate.InstalledFileIDs);
        Assert.Equal(new[] { "C:/Games/Costumes" }, restoredCandidate.InstalledTopPaths);
        Assert.Equal(1024, restoredCandidate.InstalledSize);
    }

    [Fact]
    public void ReadDownloadBackups_MixedInstallTypesRemainDistinct()
    {
        Write("game.tmp", JsonSerializer.Serialize(new DownloadRequestBackup { InstallType = InstallType.BaseGame, ID = "base", GameID = "10" }));
        Write("remote.tmp", JsonSerializer.Serialize(new DownloadRequestBackup { InstallType = InstallType.Remote, ID = "remote", GameID = "20" }));
        Write("dlc.tmp", JsonSerializer.Serialize(new DownloadRequestBackup { InstallType = InstallType.UpdateDLC, ID = "dlc", GameID = "30" }));

        var restored = _controller.ReadDownloadBackups(_root);

        Assert.Equal(3, restored.Count);
        Assert.Contains(restored, item => item.InstallType == InstallType.BaseGame && item.ID == "base" && item.GameID == "10");
        Assert.Contains(restored, item => item.InstallType == InstallType.Remote && item.ID == "remote" && item.GameID == "20");
        Assert.Contains(restored, item => item.InstallType == InstallType.UpdateDLC && item.ID == "dlc" && item.GameID == "30");
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("{\"ID\":\"ok\"} trailing")]
    public void ReadDownloadBackups_InvalidTopLevelJsonIsReportedWithoutDeletingTheFile(string content)
    {
        var path = Write("corrupt.tmp", content);
        List<(string Path, Exception Error)> failures = [];

        var restored = _controller.ReadDownloadBackups(_root, onFailed: (file, error) => failures.Add((file, error)));

        Assert.Empty(restored);
        Assert.Equal(path, Assert.Single(failures).Path);
        Assert.IsType<JsonException>(failures[0].Error);
        Assert.Equal(content, File.ReadAllText(path));
    }

    [Fact]
    public void ReadDownloadBackups_MultipleCorruptFilesDoNotPreventRecoveryOfValidOnes()
    {
        var firstInvalid = Write("broken-a.tmp", "{");
        var secondInvalid = Write("broken-b.tmp", "[false]");
        var firstValid = Write("valid-a.tmp", "{\"ID\":\"first\",\"GameID\":\"100\"}");
        var secondValid = Write("valid-b.tmp", "{\"ID\":\"second\",\"GameID\":\"200\"}");
        List<string> loaded = [];
        List<string> failures = [];

        var restored = _controller.ReadDownloadBackups(_root,
            onLoaded: file => loaded.Add(file),
            onFailed: (file, _) => failures.Add(file));

        Assert.Equal(2, restored.Count);
        Assert.Contains(restored, value => value.ID == "first" && value.GameID == "100");
        Assert.Contains(restored, value => value.ID == "second" && value.GameID == "200");
        Assert.Equal(new[] { firstValid, secondValid }.Order(), loaded.Order());
        Assert.Equal(new[] { firstInvalid, secondInvalid }.Order(), failures.Order());
        Assert.Equal(4, Directory.GetFiles(_root, "*.tmp").Length);
    }

    [Fact]
    public void ReadDownloadBackups_NullRecordsAreRemovedWithoutTouchingValidBackups()
    {
        var emptyOne = Write("empty-a.tmp", "null");
        var emptyTwo = Write("empty-b.tmp", "  null  ");
        var valid = Write("valid.tmp", "{\"ID\":\"in-progress\",\"GameID\":\"42\"}");
        List<string> loaded = [];
        List<string> failures = [];

        var restored = _controller.ReadDownloadBackups(_root,
            onLoaded: loaded.Add,
            onFailed: (file, _) => failures.Add(file));

        Assert.Equal("in-progress", Assert.Single(restored).ID);
        Assert.Equal(valid, Assert.Single(loaded));
        Assert.Empty(failures);
        Assert.False(File.Exists(emptyOne));
        Assert.False(File.Exists(emptyTwo));
        Assert.True(File.Exists(valid));
    }

    [Fact]
    public void ReadDownloadBackups_IgnoresNestedBackupFiles()
    {
        var nested = Path.Combine(_root, "OldSession");
        Directory.CreateDirectory(nested);
        var ignored = Path.Combine(nested, "remote.tmp");
        File.WriteAllText(ignored, "{\"ID\":\"nested\"}");
        Write("active.tmp", "{\"ID\":\"active\"}");

        var restored = _controller.ReadDownloadBackups(_root);

        Assert.Equal("active", Assert.Single(restored).ID);
        Assert.True(File.Exists(ignored));
    }

    [Fact]
    public void ReadDownloadBackups_UnknownFieldsDoNotInvalidateAnExistingBackup()
    {
        var path = Write("future.tmp", """
            {"InstallType":1,"ID":"future-request","GameID":"42","NewRomMField":{"enabled":true},"DownloadURL":"/api/roms/42/content/rom.zip"}
            """);

        var restored = Assert.Single(_controller.ReadDownloadBackups(_root));

        Assert.Equal(InstallType.Remote, restored.InstallType);
        Assert.Equal("future-request", restored.ID);
        Assert.Equal("/api/roms/42/content/rom.zip", restored.DownloadURL);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void ReadDownloadBackups_DoesNotRemoveSuccessfullyParsedBackups()
    {
        var content = "{\"InstallType\":0,\"ID\":\"unfinished-game\",\"GameID\":\"42\"}";
        var path = Write("recover.tmp", content);

        var first = _controller.ReadDownloadBackups(_root);
        var second = _controller.ReadDownloadBackups(_root);

        Assert.Equal("unfinished-game", Assert.Single(first).ID);
        Assert.Equal("unfinished-game", Assert.Single(second).ID);
        Assert.Equal(content, File.ReadAllText(path));
    }

    private string Write(string fileName, string contents)
    {
        var path = Path.Combine(_root, fileName);
        File.WriteAllText(path, contents);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
