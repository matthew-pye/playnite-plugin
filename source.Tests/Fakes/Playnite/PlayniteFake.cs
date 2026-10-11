using NSubstitute;
using Playnite;

using Graviton.Tests.Fakes.Playnite.Dialogs;
using Graviton.Tests.Fakes.Playnite.Library;
using Graviton.Tests.Fakes.Playnite.Notifications;

namespace Graviton.Tests.Fakes.Playnite;

public sealed class PlayniteFake
{
    private string? _userDataDirectory;

    public IPlayniteApi Api { get; } = Substitute.For<IPlayniteApi>();
    public ILibraryApi Library { get; } = Substitute.For<ILibraryApi>();
    public FakeDialogs Dialogs { get; } = new();
    public FakeNotifications Notifications { get; } = new();

    public FakeLibraryCollection<Game> Games { get; } = new();
    public FakeLibraryCollection<GameSession> GameSessions { get; } = new();
    public FakeLibraryCollection<GameRelation> GameRelations { get; } = new();
    public FakeLibraryCollection<GameDescription> GameDescriptions { get; } = new();

    public FakeLibraryCollection<AgeRating> AgeRatings { get; } = new();
    public FakeLibraryCollection<Category> Categories { get; } = new();
    public FakeLibraryCollection<Company> Companies { get; } = new();
    public FakeLibraryCollection<CompletionStatus> CompletionStatuses { get; } = new();
    public FakeLibraryCollection<Feature> Features { get; } = new();
    public FakeLibraryCollection<Genre> Genres { get; } = new();
    public FakeLibraryCollection<Platform> Platforms { get; } = new();
    public FakeLibraryCollection<Region> Regions { get; } = new();
    public FakeLibraryCollection<Series> Series { get; } = new();

    public FakeLibraryCollection<Source> Sources { get; } = new();
    public FakeLibraryCollection<WebLinkType> WebLinkTypes { get; } = new();
    public FakeLibraryCollection<ExternalIdentifierType> ExternalIdentifierTypes { get; } = new();

    public PlayniteFake()
    {
        Api.Library.Returns(Library);
        Api.Dialogs.Returns(Dialogs.Api);
        Api.Notifications.Returns(Notifications.Api);
        Api.UserDataDir.Returns(_ =>
        {
            return _userDataDirectory ?? throw new InvalidOperationException("No Playnite user data directory has been configured.");
        });

        Library.Games.Returns(Games.Api);
        Library.GameSessions.Returns(GameSessions.Api);
        Library.GameRelations.Returns(GameRelations.Api);
        Library.GameDescriptions.Returns(GameDescriptions.Api);

        Library.AgeRatings.Returns(AgeRatings.Api);
        Library.Categories.Returns(Categories.Api);
        Library.Companies.Returns(Companies.Api);
        Library.CompletionStatuses.Returns(CompletionStatuses.Api);
        Library.Features.Returns(Features.Api);
        Library.Genres.Returns(Genres.Api);
        Library.Platforms.Returns(Platforms.Api);
        Library.Regions.Returns(Regions.Api);
        Library.Series.Returns(Series.Api);

        Library.Sources.Returns(Sources.Api);
        Library.WebLinkTypes.Returns(WebLinkTypes.Api);
        Library.ExternalIdentifierTypes.Returns(ExternalIdentifierTypes.Api);
    }

    public void ConfigureUserDataDirectory(string path)
    {
        _userDataDirectory = path;
    }
}
