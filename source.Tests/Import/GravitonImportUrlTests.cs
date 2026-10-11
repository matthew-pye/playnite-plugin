using Graviton.Import;
using Graviton.Notifications;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;
using Graviton.Tests.Fakes.RomM;

using System.Web;

using Xunit;

namespace Graviton.Tests.Import;

public sealed class GravitonImportUrlTests
{
    [Fact]
    public void BuildGeneralROMUrl_WithDefaultFilters_IncludesRequiredRomMOptions()
    {
        var (controller, plugin) = CreateController();
        plugin.Settings.SkipMissingFiles = false;
        plugin.Settings.ExcludeGenres = "";

        var url = controller.BuildGeneralROMUrl();

        Assert.StartsWith("/api/roms?", url);
        Assert.Equal("none", GetQueryValue(url, "genres_logic"));
        Assert.Equal("name", GetQueryValue(url, "order_by"));
        Assert.Equal("asc", GetQueryValue(url, "order_dir"));
        Assert.Equal("true", GetQueryValue(url, "with_files"));
    }

    [Fact]
    public void BuildGeneralROMUrl_WhenMissingFilesAreExcluded_AddsMissingFalse()
    {
        var (controller, plugin) = CreateController();
        plugin.Settings.SkipMissingFiles = true;

        var url = controller.BuildGeneralROMUrl();

        Assert.Equal("false", GetQueryValue(url, "missing"));
    }

    [Fact]
    public void BuildGeneralROMUrl_WhenMissingFilesAreAllowed_OmitsMissingFilter()
    {
        var (controller, plugin) = CreateController();
        plugin.Settings.SkipMissingFiles = false;

        var url = controller.BuildGeneralROMUrl();

        Assert.Null(GetQueryValue(url, "missing"));
    }

    [Fact]
    public void BuildGeneralROMUrl_WithoutExcludedGenres_OmitsGenreFilters()
    {
        var (controller, plugin) = CreateController();
        plugin.Settings.ExcludeGenres = "";

        var url = controller.BuildGeneralROMUrl();

        Assert.Empty(GetQueryValues(url, "genres"));
    }

    [Fact]
    public void BuildGeneralROMUrl_WithSingleExcludedGenre_AddsGenreFilter()
    {
        var (controller, plugin) = CreateController();
        plugin.Settings.ExcludeGenres = "Adventure";

        var url = controller.BuildGeneralROMUrl();

        Assert.Equal(new[] { "Adventure" }, GetQueryValues(url, "genres"));
    }

    [Fact]
    public void BuildGeneralROMUrl_WithMultipleGenres_KeepsTheirOrder()
    {
        var (controller, plugin) = CreateController();
        plugin.Settings.ExcludeGenres = "Adventure;Platform;RPG";

        var url = controller.BuildGeneralROMUrl();

        Assert.Equal(new[] { "Adventure", "Platform", "RPG" }, GetQueryValues(url, "genres"));
    }

    [Fact]
    public void BuildGeneralROMUrl_WithTrailingSeparator_DoesNotAddAnEmptyGenre()
    {
        var (controller, plugin) = CreateController();
        plugin.Settings.ExcludeGenres = "Adventure;Platform; ";

        var url = controller.BuildGeneralROMUrl();

        Assert.Equal(new[] { "Adventure", "Platform" }, GetQueryValues(url, "genres"));
    }

    [Fact]
    public void BuildGeneralROMUrl_WithGenreContainingSpaces_EncodesAndRestoresTheValue()
    {
        var (controller, plugin) = CreateController();
        plugin.Settings.ExcludeGenres = "Role Playing";

        var url = controller.BuildGeneralROMUrl();

        Assert.Contains("genres=Role+Playing", url);
        Assert.Equal("Role Playing", GetQueryValue(url, "genres"));
    }

    [Fact]
    public void BuildGeneralROMUrl_WithSpecialCharacters_PreservesGenreValues()
    {
        var (controller, plugin) = CreateController();
        plugin.Settings.ExcludeGenres = "Action & Adventure;Pokémon";

        var url = controller.BuildGeneralROMUrl();

        Assert.Equal(new[] { "Action & Adventure", "Pokémon" }, GetQueryValues(url, "genres"));
    }

    private static string? GetQueryValue(string url, string name)
    {
        var query = HttpUtility.ParseQueryString(new Uri("https://example.invalid" + url).Query);
        return query[name];
    }

    private static string[] GetQueryValues(string url, string name)
    {
        var query = HttpUtility.ParseQueryString(new Uri("https://example.invalid" + url).Query);
        return query.GetValues(name) ?? [];
    }

    private static (GravitonImportController Controller, GravitonContextFake Plugin) CreateController()
    {
        var plugin = new GravitonContextFake();
        var playnite = new PlayniteFake();
        var romm = new RomMServerFake();
        var controller = new GravitonImportController(
            plugin: plugin.Context,
            playniteAPI: playnite.Api,
            logger: new GravitonLogger(plugin.Settings),
            server: romm.Server);

        return (controller, plugin);
    }
}
