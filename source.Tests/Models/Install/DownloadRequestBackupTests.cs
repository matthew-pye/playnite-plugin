using Graviton.Models.Install;

using System.IO;
using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Models.Install;

public sealed class DownloadRequestBackupTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "GravitonTests",
        Guid.NewGuid().ToString("N"));

    private string BackupDirectory => Path.Combine(_testDirectory, "temp", "downloads");

    [Fact]
    public void BaseGameBackup_AfterReload_PreservesInstallDetails()
    {
        var backup = new DownloadRequestBackup
        {
            InstallType = InstallType.BaseGame,
            GameID = "42",
            ID = "42",
            DownloadPath = Path.Combine("temp", "game-id", "game.zip"),
            InstallDir = Path.Combine("Games", "Example Game")
        };

        var restored = SaveAndReload(backup);

        Assert.Equal(InstallType.BaseGame, restored.InstallType);
        Assert.Equal(backup.GameID, restored.GameID);
        Assert.Equal(backup.ID, restored.ID);
        Assert.Equal(backup.DownloadPath, restored.DownloadPath);
        Assert.Equal(backup.InstallDir, restored.InstallDir);
    }

    [Fact]
    public void RemoteBackup_AfterReload_PreservesEventIdAndDownloadUrl()
    {
        var backup = new DownloadRequestBackup
        {
            InstallType = InstallType.Remote,
            GameID = "42",
            ID = "remote-event-789",
            DownloadURL = "/api/roms/42/content/game.zip?file_ids=17,18",
            DownloadPath = Path.Combine("temp", "game-id", "game.zip"),
            InstallDir = Path.Combine("Games", "Example Game")
        };

        var restored = SaveAndReload(backup);

        Assert.Equal(InstallType.Remote, restored.InstallType);
        Assert.Equal("42", restored.GameID);
        Assert.Equal("remote-event-789", restored.ID);
        Assert.NotEqual(restored.GameID, restored.ID);
        Assert.Equal(backup.DownloadURL, restored.DownloadURL);
        Assert.Equal(backup.DownloadPath, restored.DownloadPath);
        Assert.Equal(backup.InstallDir, restored.InstallDir);
    }

    [Theory]
    [InlineData("update")]
    [InlineData("dlc")]
    public void UpdateDLCBackup_AfterReload_PreservesCandidateAndDependency(string category)
    {
        var candidate = new UpdateDLCCandidate(
            name: "Patch 1",
            filename: "patch.zip",
            category: category,
            fileIDs: new[] { 17, 18 },
            candidateRoot: "Updates",
            size: 4096)
        {
            IsSelected = true
        };

        var backup = new DownloadRequestBackup
        {
            InstallType = InstallType.UpdateDLC,
            GameID = "42",
            ID = "request-2",
            DownloadPath = Path.Combine("temp", "request-2", "patch.zip"),
            Candidate = candidate,
            Category = category,
            InstallStyle = InstallStyles.Folder,
            PreviousRequestId = "request-1"
        };

        var restored = SaveAndReload(backup);

        Assert.Equal(InstallType.UpdateDLC, restored.InstallType);
        Assert.Equal("42", restored.GameID);
        Assert.Equal("request-2", restored.ID);
        Assert.Equal(category, restored.Category);
        Assert.Equal(InstallStyles.Folder, restored.InstallStyle);
        Assert.Equal("request-1", restored.PreviousRequestId);
        Assert.Equal(backup.DownloadPath, restored.DownloadPath);

        var restoredCandidate = Assert.IsType<UpdateDLCCandidate>(restored.Candidate);
        Assert.Equal("Patch 1", restoredCandidate.Name);
        Assert.Equal("patch.zip", restoredCandidate.FileName);
        Assert.Equal(category, restoredCandidate.Category);
        Assert.Equal("Updates", restoredCandidate.CandidateRoot);
        Assert.Equal(new[] { 17, 18 }, restoredCandidate.FileIDs.ToArray());
        Assert.Equal(4096, restoredCandidate.Size);
        Assert.False(restoredCandidate.IsSelected);
    }

    private DownloadRequestBackup SaveAndReload(DownloadRequestBackup backup)
    {
        Directory.CreateDirectory(BackupDirectory);

        var path = Path.Combine(BackupDirectory, $"{Guid.NewGuid():N}.tmp");
        var json = JsonSerializer.Serialize(backup);

        File.WriteAllText(path, json);

        var contents = File.ReadAllText(path);
        var restored = JsonSerializer.Deserialize<DownloadRequestBackup>(contents);

        return Assert.IsType<DownloadRequestBackup>(restored);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}
