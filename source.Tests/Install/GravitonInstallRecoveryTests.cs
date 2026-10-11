using Graviton.Install;
using Graviton.Models.Install;
using Graviton.Notifications;
using Graviton.Tests.Fakes.Playnite;
using Graviton.Tests.Fakes.RomM;

using NSubstitute;

using System.IO;
using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Install;

public sealed class GravitonInstallRecoveryTests : IDisposable
{
    private readonly string _backupDirectory = Path.Combine(
        Path.GetTempPath(),
        "Graviton.Tests",
        Guid.NewGuid().ToString("N"));

    public GravitonInstallRecoveryTests()
    {
        Directory.CreateDirectory(_backupDirectory);
    }

    [Fact]
    public void ReadDownloadBackups_WithMultipleValidFiles_ReturnsAllRequests()
    {
        WriteBackup("base.tmp", JsonSerializer.Serialize(new DownloadRequestBackup
        {
            InstallType = InstallType.BaseGame,
            ID = "42",
            GameID = "42"
        }));

        WriteBackup("remote.tmp", JsonSerializer.Serialize(new DownloadRequestBackup
        {
            InstallType = InstallType.Remote,
            ID = "remote-event-123",
            GameID = "42"
        }));

        var requests = CreateController().ReadDownloadBackups(_backupDirectory);

        Assert.Equal(2, requests.Count);
        Assert.Contains(requests, request =>
        {
            return request.InstallType == InstallType.BaseGame && request.ID == "42";
        });
        Assert.Contains(requests, request =>
        {
            return request.InstallType == InstallType.Remote &&
                   request.ID == "remote-event-123" &&
                   request.GameID == "42";
        });
    }

    [Fact]
    public void ReadDownloadBackups_WithInvalidJson_ContinuesLoadingOtherRequests()
    {
        var validPath = WriteBackup("valid.tmp", JsonSerializer.Serialize(new DownloadRequestBackup
        {
            InstallType = InstallType.BaseGame,
            ID = "42",
            GameID = "42"
        }));
        var invalidPath = WriteBackup("invalid.tmp", "{ not valid JSON }");

        List<string> loadedFiles = new();
        List<(string Path, Exception Error)> failures = new();

        var requests = CreateController().ReadDownloadBackups(
            _backupDirectory,
            onLoaded: path =>
            {
                loadedFiles.Add(path);
            },
            onFailed: (path, error) =>
            {
                failures.Add((path, error));
            });

        var request = Assert.Single(requests);
        Assert.Equal("42", request.ID);
        Assert.Equal(validPath, Assert.Single(loadedFiles));
        Assert.Equal(invalidPath, Assert.Single(failures).Path);
        Assert.IsType<JsonException>(failures[0].Error);
        Assert.True(File.Exists(invalidPath));
    }

    [Fact]
    public void ReadDownloadBackups_WithNullJson_DeletesEmptyBackup()
    {
        var nullPath = WriteBackup("null.tmp", "null");

        var requests = CreateController().ReadDownloadBackups(_backupDirectory);

        Assert.Empty(requests);
        Assert.False(File.Exists(nullPath));
    }

    [Fact]
    public void ReadDownloadBackups_IgnoresFilesWithoutTmpExtension()
    {
        var ignoredPath = WriteBackup("backup.json", JsonSerializer.Serialize(new DownloadRequestBackup
        {
            ID = "ignored"
        }));

        var requests = CreateController().ReadDownloadBackups(_backupDirectory);

        Assert.Empty(requests);
        Assert.True(File.Exists(ignoredPath));
    }

    [Fact]
    public void ReadDownloadBackups_WhenDirectoryIsEmpty_ReturnsEmptyList()
    {
        var requests = CreateController().ReadDownloadBackups(_backupDirectory);

        Assert.Empty(requests);
    }

    private static GravitonInstallController CreateController()
    {
        var plugin = Substitute.For<IGravitonContext>();
        var playnite = new PlayniteFake();
        var romm = new RomMServerFake();
        var logger = new GravitonLogger();

        return new GravitonInstallController(
            plugin: plugin,
            playniteAPI: playnite.Api,
            logger: logger,
            server: romm.Server);
    }

    private string WriteBackup(string filename, string content)
    {
        var path = Path.Combine(_backupDirectory, filename);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_backupDirectory))
        {
            Directory.Delete(_backupDirectory, recursive: true);
        }
    }
}
