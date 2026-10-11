using Graviton.Import;
using Graviton.Models.RomM.Platform;
using Graviton.Models.RomM.Rom;
using Graviton.Notifications;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;
using Graviton.Tests.Fakes.RomM;

using NSubstitute;

using System.Text.Json.Nodes;

using Xunit;

using static Playnite.Plugin;

namespace Graviton.Tests.Import;

public sealed class GravitonImportPaginationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(99)]
    public async Task DownloadROMData_WithShortFirstPage_StopsAfterOneRequest(int count)
    {
        var (controller, romm) = CreateController();
        var url = controller.BuildGeneralROMUrl();
        var firstPage = GetPageUrl(url, 17, 0);
        romm.ConfigureJsonGETResponse(firstPage, CreatePage(Enumerable.Range(1, count), count, 0));

        var games = await controller.DownloadROMData(new ImportGamesArgs(), url, CreatePlatform(17));

        Assert.Equal(count, games.Count);
        await romm.Server.Received(1).GETAsync(firstPage, false);
        await romm.Server.Received(1).GETAsync(Arg.Any<string>(), false);
    }

    [Fact]
    public async Task DownloadROMData_WithExactlyOneFullPage_RequestsAnEmptySecondPage()
    {
        var (controller, romm) = CreateController();
        var url = controller.BuildGeneralROMUrl();
        var firstPage = GetPageUrl(url, 17, 0);
        var secondPage = GetPageUrl(url, 17, 100);
        romm.ConfigureJsonGETResponse(firstPage, CreatePage(Enumerable.Range(1, 100), 100, 0));
        romm.ConfigureJsonGETResponse(secondPage, CreatePage([], 100, 100));

        var games = await controller.DownloadROMData(new ImportGamesArgs(), url, CreatePlatform(17));

        Assert.Equal(100, games.Count);
        Assert.Equal(1, games[0].Id);
        Assert.Equal(100, games[^1].Id);
        await romm.Server.Received(1).GETAsync(secondPage, false);
        await romm.Server.Received(2).GETAsync(Arg.Any<string>(), false);
    }

    [Fact]
    public async Task DownloadROMData_WithOneHundredAndOneRoms_CombinesBothPagesInOrder()
    {
        var (controller, romm) = CreateController();
        var url = controller.BuildGeneralROMUrl();
        romm.ConfigureJsonGETResponse(GetPageUrl(url, 17, 0), CreatePage(Enumerable.Range(1, 100), 101, 0));
        romm.ConfigureJsonGETResponse(GetPageUrl(url, 17, 100), CreatePage([101], 101, 100));

        var games = await controller.DownloadROMData(new ImportGamesArgs(), url, CreatePlatform(17));

        Assert.Equal(Enumerable.Range(1, 101), games.Select(game => game.Id));
        await romm.Server.Received(2).GETAsync(Arg.Any<string>(), false);
    }

    [Fact]
    public async Task DownloadROMData_WithExactlyTwoFullPages_RequestsTheThirdPage()
    {
        var (controller, romm) = CreateController();
        var url = controller.BuildGeneralROMUrl();
        romm.ConfigureJsonGETResponse(GetPageUrl(url, 17, 0), CreatePage(Enumerable.Range(1, 100), 200, 0));
        romm.ConfigureJsonGETResponse(GetPageUrl(url, 17, 100), CreatePage(Enumerable.Range(101, 100), 200, 100));
        romm.ConfigureJsonGETResponse(GetPageUrl(url, 17, 200), CreatePage([], 200, 200));

        var games = await controller.DownloadROMData(new ImportGamesArgs(), url, CreatePlatform(17));

        Assert.Equal(200, games.Count);
        await romm.Server.Received(1).GETAsync(GetPageUrl(url, 17, 200), false);
        await romm.Server.Received(3).GETAsync(Arg.Any<string>(), false);
    }

    [Fact]
    public async Task DownloadROMData_WithPartialThirdPage_RetainsEveryPage()
    {
        var (controller, romm) = CreateController();
        var url = controller.BuildGeneralROMUrl();
        romm.ConfigureJsonGETResponse(GetPageUrl(url, 17, 0), CreatePage(Enumerable.Range(1, 100), 205, 0));
        romm.ConfigureJsonGETResponse(GetPageUrl(url, 17, 100), CreatePage(Enumerable.Range(101, 100), 205, 100));
        romm.ConfigureJsonGETResponse(GetPageUrl(url, 17, 200), CreatePage(Enumerable.Range(201, 5), 205, 200));

        var games = await controller.DownloadROMData(new ImportGamesArgs(), url, CreatePlatform(17));

        Assert.Equal(Enumerable.Range(1, 205), games.Select(game => game.Id));
        await romm.Server.Received(3).GETAsync(Arg.Any<string>(), false);
    }

    [Fact]
    public async Task DownloadROMData_UsesTheSelectedPlatformId()
    {
        var (controller, romm) = CreateController();
        var url = controller.BuildGeneralROMUrl();
        var selectedPage = GetPageUrl(url, 42, 0);
        romm.ConfigureJsonGETResponse(selectedPage, CreatePage([5], 1, 0));

        var games = await controller.DownloadROMData(new ImportGamesArgs(), url, CreatePlatform(42));

        Assert.Equal(5, Assert.Single(games).Id);
        await romm.Server.Received(1).GETAsync(selectedPage, false);
        await romm.Server.DidNotReceive().GETAsync(GetPageUrl(url, 17, 0), false);
    }

    [Fact]
    public async Task DownloadROMData_UsesFiltersFromTheGeneralRomUrl()
    {
        var (controller, romm, plugin) = CreateControllerWithPlugin();
        plugin.Settings.SkipMissingFiles = true;
        plugin.Settings.ExcludeGenres = "Role Playing";
        var url = controller.BuildGeneralROMUrl();
        var firstPage = GetPageUrl(url, 17, 0);
        romm.ConfigureJsonGETResponse(firstPage, CreatePage([], 0, 0));

        await controller.DownloadROMData(new ImportGamesArgs(), url, CreatePlatform(17));

        Assert.Contains("missing=false&", firstPage);
        Assert.Contains("genres=Role+Playing&", firstPage);
        await romm.Server.Received(1).GETAsync(firstPage, false);
    }

    [Fact]
    public async Task DownloadROMData_PreservesRomNamesPathsAndFileSizes()
    {
        var (controller, romm) = CreateController();
        var url = controller.BuildGeneralROMUrl();
        var rom = new RomMRom
        {
            Id = 7,
            Name = "A Game",
            FileName = "A Game.zip",
            FileSizeBytes = 4096,
            FullPath = "gb/A Game.zip",
            SHA1 = new string('a', 40)
        };
        romm.ConfigureJsonGETResponse(GetPageUrl(url, 17, 0), SerializePage([rom], 1, 0));

        var games = await controller.DownloadROMData(new ImportGamesArgs(), url, CreatePlatform(17));

        var game = Assert.Single(games);
        Assert.Equal(7, game.Id);
        Assert.Equal("A Game", game.Name);
        Assert.Equal("A Game.zip", game.FileName);
        Assert.Equal(4096UL, game.FileSizeBytes);
        Assert.Equal("gb/A Game.zip", game.FullPath);
        Assert.Equal(rom.SHA1, game.SHA1);
    }

    [Fact]
    public async Task DownloadROMData_DoesNotRequireRomMResponseTotalsToStopPaging()
    {
        var (controller, romm) = CreateController();
        var url = controller.BuildGeneralROMUrl();
        romm.ConfigureJsonGETResponse(GetPageUrl(url, 17, 0), CreatePage([1, 2, 3], 10_000, 0));

        var games = await controller.DownloadROMData(new ImportGamesArgs(), url, CreatePlatform(17));

        Assert.Equal(3, games.Count);
        await romm.Server.Received(1).GETAsync(Arg.Any<string>(), false);
    }

    [Fact]
    public async Task DownloadROMData_WithAdditionalResponseFields_StillReadsItems()
    {
        var (controller, romm) = CreateController();
        var url = controller.BuildGeneralROMUrl();
        var rom = RomMRomFixtures.CreateRom(id: 18, name: "Example");
        rom["future_field"] = true;
        var response = JsonNode.Parse(RomMRomFixtures.CreatePage([rom], 1, 0))!.AsObject();
        response["future_metadata"] = new JsonObject { ["page_version"] = 2 };
        romm.ConfigureJsonGETResponse(GetPageUrl(url, 17, 0), response.ToJsonString());

        var games = await controller.DownloadROMData(new ImportGamesArgs(), url, CreatePlatform(17));

        Assert.Equal(18, Assert.Single(games).Id);
    }

    private static string GetPageUrl(string baseUrl, int platformId, int offset)
    {
        return $"{baseUrl}platform_ids={platformId}&limit=100&offset={offset}";
    }

    private static RomMPlatform CreatePlatform(int id)
    {
        return new RomMPlatform
        {
            Id = id,
            Name = "Example Platform"
        };
    }

    private static string CreatePage(IEnumerable<int> ids, int total, int offset)
    {
        var roms = ids.Select(id => RomMRomFixtures.CreateRom(
            id: id,
            name: $"Game {id}",
            fileName: $"Game {id}.zip",
            fileSizeBytes: 1024,
            platformId: 17));

        return RomMRomFixtures.CreatePage(roms, total, offset);
    }

    private static string SerializePage(IEnumerable<RomMRom> roms, int total, int offset)
    {
        var fixtures = roms.Select(rom =>
        {
            var fixture = RomMRomFixtures.CreateRom(
                id: rom.Id,
                name: rom.Name ?? $"Game {rom.Id}",
                fileName: rom.FileName,
                fileSizeBytes: rom.FileSizeBytes,
                platformId: 17);

            if (rom.FullPath is not null)
            {
                var separator = rom.FullPath.LastIndexOf('/');

                if (separator >= 0)
                {
                    var directory = rom.FullPath[..separator];
                    fixture["fs_path"] = directory;
                    fixture["files"]![0]!["file_path"] = directory;
                }

                fixture["full_path"] = rom.FullPath;
                fixture["files"]![0]!["full_path"] = rom.FullPath;
            }

            if (rom.SHA1 is not null)
            {
                fixture["sha1_hash"] = rom.SHA1;
            }

            return fixture;
        });

        return RomMRomFixtures.CreatePage(fixtures, total, offset);
    }

    private static (GravitonImportController Controller, RomMServerFake RomM) CreateController()
    {
        var (controller, romm, _) = CreateControllerWithPlugin();
        return (controller, romm);
    }

    private static (GravitonImportController Controller, RomMServerFake RomM, GravitonContextFake Plugin) CreateControllerWithPlugin()
    {
        var plugin = new GravitonContextFake();
        var playnite = new PlayniteFake();
        var romm = new RomMServerFake();
        var controller = new GravitonImportController(
            plugin: plugin.Context,
            playniteAPI: playnite.Api,
            logger: new GravitonLogger(plugin.Settings),
            server: romm.Server);

        return (controller, romm, plugin);
    }
}
