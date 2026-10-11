using System.IO;

namespace Graviton.Tests.Install.Downloads;

public sealed class DownloadQueueControllerCleanupTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        "GravitonTests",
        Guid.NewGuid().ToString("N"));


    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }
}
