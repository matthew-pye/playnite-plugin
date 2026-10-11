using Graviton.Install.Downloads;
using Graviton.Notifications;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;
using Graviton.Tests.Fakes.RomM;

using Xunit;

namespace Graviton.Tests.Install.Downloads;

public sealed class DownloadQueueControllerConfigurationTests
{
    [Theory]
    [InlineData(-10, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(10, 10)]
    public void Constructor_ClampsConcurrencyToAtLeastOne(int requested, int expected)
    {
        var viewModel = new DownloadQueueViewModel();
        var controller = CreateController(viewModel, requested);

        Assert.Equal(expected, controller.MaxConcurrent);
    }

    [Fact]
    public void ViewModel_ReturnsTheInstanceProvidedToController()
    {
        var viewModel = new DownloadQueueViewModel();
        var controller = CreateController(viewModel, 2);

        Assert.Same(viewModel, controller.ViewModel);
    }

    [Fact]
    public void IsDownloading_WhenRequestWasNeverQueued_ReturnsFalse()
    {
        var controller = CreateController(new DownloadQueueViewModel(), 2);

        Assert.False(controller.IsDownloading("missing-download"));
    }

    [Fact]
    public void Cancel_WhenRequestWasNeverQueued_DoesNotThrow()
    {
        var controller = CreateController(new DownloadQueueViewModel(), 2);

        var exception = Record.Exception(() =>
        {
            controller.Cancel("missing-download");
        });

        Assert.Null(exception);
    }

    private static DownloadQueueController CreateController(DownloadQueueViewModel viewModel, int maxConcurrent)
    {
        var plugin = new GravitonContextFake();
        var playnite = new PlayniteFake();
        var romm = new RomMServerFake();
        var logger = new GravitonLogger(plugin.Settings);

        return new DownloadQueueController(
            plugin: plugin.Context,
            playniteAPI: playnite.Api,
            logger: logger,
            romMServer: romm.Server,
            downloadQueueVM: viewModel,
            maxConcurrent: maxConcurrent);
    }
}
