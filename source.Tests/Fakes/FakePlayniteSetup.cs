using Moq;
using Playnite;

namespace Graviton.Tests.Fakes
{

    public sealed class FakePlayniteSetup
    {
        public List<Game> Games { get; } = new();
        public List<Genre> Genres { get; } = new();
        public List<Category> Categories { get; } = new();
        public List<Series> Series { get; } = new();
        public List<Feature> Features { get; } = new();
        public List<AgeRating> AgeRatings { get; } = new();
        public List<Region> Regions { get; } = new();
        public List<Platform> Platforms { get; } = new();
        public List<GameDescription> GameDescriptions { get; } = new();
        public List<GameNote> GameNotes { get; } = new();
        public List<GameRelation> GameRelations { get; } = new();
        public List<CompletionStatus> CompletionStatuses { get; } = new();

        public Mock<IPlayniteApi> ApiMock { get; }
        public Mock<ILibraryApi> LibraryMock { get; }
        public IPlayniteApi Api => ApiMock.Object;

        public FakePlayniteSetup(string userDataDir)
        {
            ApiMock     = new Mock<IPlayniteApi>(MockBehavior.Loose);
            LibraryMock = new Mock<ILibraryApi>(MockBehavior.Loose);

            ApiMock.Setup(a => a.Library).Returns(LibraryMock.Object);
            ApiMock.Setup(a => a.UserDataDir).Returns(userDataDir);

            ApiMock.Setup(a => a.Notifications).Returns(new Mock<INotificationsAPI>(MockBehavior.Loose).Object);

            var appInfo = new Mock<IApplicationInfoApi>(MockBehavior.Loose);
            appInfo.Setup(a => a.ApplicationDirectory).Returns(userDataDir);
            ApiMock.Setup(a => a.AppInfo).Returns(appInfo.Object);

            ApiMock.Setup(a => a.GetLocalizedString(It.IsAny<string>())).Returns((string id) => id);

            LibraryMock.Setup(l => l.Games).Returns(BuildCollection(Games).Object);
            LibraryMock.Setup(l => l.Genres).Returns(BuildCollection(Genres).Object);
            LibraryMock.Setup(l => l.Categories).Returns(BuildCollection(Categories).Object);
            LibraryMock.Setup(l => l.Series).Returns(BuildCollection(Series).Object);
            LibraryMock.Setup(l => l.Features).Returns(BuildCollection(Features).Object);
            LibraryMock.Setup(l => l.AgeRatings).Returns(BuildCollection(AgeRatings).Object);
            LibraryMock.Setup(l => l.Regions).Returns(BuildCollection(Regions).Object);
            LibraryMock.Setup(l => l.Platforms).Returns(BuildCollection(Platforms).Object);
            LibraryMock.Setup(l => l.GameDescriptions).Returns(BuildCollection(GameDescriptions).Object);
            LibraryMock.Setup(l => l.GameNotes).Returns(BuildGameNotes().Object);
            LibraryMock.Setup(l => l.GameRelations).Returns(BuildGameRelations().Object);
            LibraryMock.Setup(l => l.CompletionStatuses).Returns(BuildCollection(CompletionStatuses).Object);

            LibraryMock.Setup(l => l.Sources).Returns(new Mock<ILibraryCollection<Source>>(MockBehavior.Loose).Object);
            LibraryMock.Setup(l => l.WebLinkTypes).Returns(new Mock<ILibraryCollection<WebLinkType>>(MockBehavior.Loose).Object);
            LibraryMock.Setup(l => l.ExternalIdentifierTypes).Returns(new Mock<ILibraryCollection<ExternalIdentifierType>>(MockBehavior.Loose).Object);
        }

        public void AddExistingGame(Game game) => Games.Add(game);
        public void AddCompletionStatus(string name) => CompletionStatuses.Add(new CompletionStatus(name));

        private static Mock<ILibraryCollection<T>> BuildCollection<T>(List<T> list)
            where T : LibraryObject
        {
            var mock = new Mock<ILibraryCollection<T>>(MockBehavior.Loose);

            mock.As<IEnumerable<T>>().Setup(m => m.GetEnumerator()).Returns(() => list.GetEnumerator());
            mock.As<System.Collections.IEnumerable>().Setup(m => m.GetEnumerator()).Returns(() => list.GetEnumerator());

            mock.Setup(m => m.Get(It.IsAny<string>())).Returns((string id) => list.FirstOrDefault(x => x.Id == id));
            mock.Setup(m => m.Get(It.IsAny<IEnumerable<string>>())).Returns((IEnumerable<string> ids) => list.Where(x => ids.Contains(x.Id)).ToList());

            mock.Setup(m => m.AddAsync(It.IsAny<T>())).Callback<T>(item => list.Add(item)).Returns(Task.CompletedTask);
            mock.Setup(m => m.AddAsync(It.IsAny<IEnumerable<T>>())).Callback<IEnumerable<T>>(items => list.AddRange(items)).Returns(Task.CompletedTask);

            mock.Setup(m => m.UpdateAsync(It.IsAny<T>())).Returns(Task.FromResult<CollectionItemUpdateData<T>>(null!));

            mock.Setup(m => m.RemoveAsync(It.IsAny<string>())).Returns(Task.FromResult<T>(null!)!);

            return mock;
        }

        private Mock<ILibraryCollection<GameNote>> BuildGameNotes()
        {
            var mock = BuildCollection(GameNotes);

            mock.Setup(m => m.UpdateAsync(It.IsAny<GameNote>())).Callback<GameNote>(updated =>
                {
                    var i = GameNotes.FindIndex(n => n.Id == updated.Id);
                    if (i >= 0) GameNotes[i] = updated;
                })
                .Returns(Task.FromResult<CollectionItemUpdateData<GameNote>>(null!));

            return mock;
        }

        private Mock<ILibraryCollection<GameRelation>> BuildGameRelations()
        {
            var mock = BuildCollection(GameRelations);

            mock.Setup(m => m.UpdateAsync(It.IsAny<GameRelation>())).Callback<GameRelation>(updated =>
                {
                    var i = GameRelations.FindIndex(r => r.Id == updated.Id);
                    if (i >= 0) GameRelations[i] = updated;
                })
                .Returns(Task.FromResult<CollectionItemUpdateData<GameRelation>>(null!));

            mock.Setup(m => m.RemoveAsync(It.IsAny<string>())).Callback<string>(id => GameRelations.RemoveAll(r => r.Id == id)).Returns(Task.FromResult<GameRelation>(null!)!);

            return mock;
        }
    }
}
