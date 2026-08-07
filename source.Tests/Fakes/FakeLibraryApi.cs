using Playnite;

using System.IO;

namespace Graviton.Tests.Fakes
{
    public class FakeLibraryApi : ILibraryApi
    {
        public FakeLibraryCollection<Game> Games { get; } = new();
        public FakeLibraryCollection<GameSession> GameSessions { get; } = new();
        public FakeLibraryCollection<GameAchievement> GameAchievements { get; } = new();
        public FakeLibraryCollection<GameDescription> GameDescriptions { get; } = new();
        public FakeLibraryCollection<GameNote> GameNotes { get; } = new();
        public FakeLibraryCollection<GameScripts> GameScripts { get; } = new();
        public FakeLibraryCollection<GameAction> GameActions { get; } = new();
        public FakeLibraryCollection<AgeRating> AgeRatings { get; } = new();
        public FakeLibraryCollection<Category> Categories { get; } = new();
        public FakeLibraryCollection<Company> Companies { get; } = new();
        public FakeLibraryCollection<CompletionStatus> CompletionStatuses { get; } = new();
        public FakeLibraryCollection<Feature> Features { get; } = new();
        public FakeLibraryCollection<Source> Sources { get; } = new();
        public FakeLibraryCollection<Genre> Genres { get; } = new();
        public FakeLibraryCollection<Platform> Platforms { get; } = new();
        public FakeLibraryCollection<Region> Regions { get; } = new();
        public FakeLibraryCollection<Series> Series { get; } = new();
        public FakeLibraryCollection<Tag> Tags { get; } = new();
        public FakeLibraryCollection<AppAction> AppActions { get; } = new();
        public FakeLibraryCollection<ViewConfiguration> ViewConfigurations { get; } = new();
        public FakeLibraryCollection<ExternalIdentifierType> ExternalIdentifierTypes { get; } = new();
        public FakeLibraryCollection<WebLinkType> WebLinkTypes { get; } = new();
        public FakeLibraryCollection<GameRelation> GameRelations { get; } = new();

        public string LibraryDir { get; set; } = Path.Combine(Path.GetTempPath(), "GravitonTests", "Library");

        ILibraryCollection<Game> ILibraryApi.Games => Games;
        ILibraryCollection<GameSession> ILibraryApi.GameSessions => GameSessions;
        ILibraryCollection<GameAchievement> ILibraryApi.GameAchievements => GameAchievements;
        ILibraryCollection<GameDescription> ILibraryApi.GameDescriptions => GameDescriptions;
        ILibraryCollection<GameNote> ILibraryApi.GameNotes => GameNotes;
        ILibraryCollection<GameScripts> ILibraryApi.GameScripts => GameScripts;
        ILibraryCollection<GameAction> ILibraryApi.GameActions => GameActions;
        ILibraryCollection<AgeRating> ILibraryApi.AgeRatings => AgeRatings;
        ILibraryCollection<Category> ILibraryApi.Categories => Categories;
        ILibraryCollection<Company> ILibraryApi.Companies => Companies;
        ILibraryCollection<CompletionStatus> ILibraryApi.CompletionStatuses => CompletionStatuses;
        ILibraryCollection<Feature> ILibraryApi.Features => Features;
        ILibraryCollection<Source> ILibraryApi.Sources => Sources;
        ILibraryCollection<Genre> ILibraryApi.Genres => Genres;
        ILibraryCollection<Platform> ILibraryApi.Platforms => Platforms;
        ILibraryCollection<Region> ILibraryApi.Regions => Regions;
        ILibraryCollection<Series> ILibraryApi.Series => Series;
        ILibraryCollection<Tag> ILibraryApi.Tags => Tags;
        ILibraryCollection<AppAction> ILibraryApi.AppActions => AppActions;
        ILibraryCollection<ViewConfiguration> ILibraryApi.ViewConfigurations => ViewConfigurations;
        ILibraryCollection<ExternalIdentifierType> ILibraryApi.ExternalIdentifierTypes => ExternalIdentifierTypes;
        ILibraryCollection<WebLinkType> ILibraryApi.WebLinkTypes => WebLinkTypes;
        ILibraryCollection<GameRelation> ILibraryApi.GameRelations => GameRelations;

        public IPluginLibraryCollection<T> GetCustomCollection<T>(bool cacheData, bool multiType) where T : LibraryObject =>
            throw new NotSupportedException($"FakeLibraryApi.GetCustomCollection<{typeof(T).Name}> isn't implemented yet - ");

        public Task<string?> AddFileAsync(string path, string ownerId, string? savedFileName = null, bool useAddonFolder = false) =>
            throw new NotSupportedException("FakeLibraryApi.AddFileAsync isn't implemented yet.");

        public bool RemoveFile(string id) =>
            throw new NotSupportedException("FakeLibraryApi.RemoveFile isn't implemented yet.");

        public string GetFileStorageDir(string parentId) =>
            throw new NotSupportedException("FakeLibraryApi.GetFileStorageDir isn't implemented yet.");

        public string GetFullFilePath(string databasePath) =>
            throw new NotSupportedException("FakeLibraryApi.GetFullFilePath isn't implemented yet.");

        public Task<Game> ImportGameAsync(ImportableGame game, string causation = "") =>
            throw new NotSupportedException("FakeLibraryApi.ImportGameAsync isn't implemented yet.");

        public Task<List<Game>> ImportGamesAsync(IEnumerable<ImportableGame> games, string causation = "") =>
            throw new NotSupportedException("FakeLibraryApi.ImportGamesAsync isn't implemented yet.");

        public Task<List<Game>> UpdateLibraryWithGamesAsync(IEnumerable<ImportableGame> toImport) =>
            throw new NotSupportedException("FakeLibraryApi.UpdateLibraryWithGamesAsync isn't implemented yet.");

        public void Dispose() {}
    }
}


