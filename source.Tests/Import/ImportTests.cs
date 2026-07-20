using Graviton.Import;
using Graviton.Models;
using Graviton.Models.RomM.Platform;
using Graviton.Models.RomM.Rom;

using Graviton.Tests.Fakes;

using System.IO;
using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Import
{

    [Collection(GravitonCollection.Name)]
    public class ImportTests(GravitonTestFixture fixture)
    {

        private static EmulatorMapping MakeMapping(string platformName = "Game Boy Advance")
        {
            return new EmulatorMapping
            {
                MappingId = Guid.NewGuid(),
                RomMPlatform = new RomMPlatform { Id = 1, Slug = "gba", Name = platformName }
            };
        }

        private GravitonImport Importer(EmulatorMapping mapping, params RomMRom[] roms) => new GravitonImport(GravitonPlugin.Instance, fixture.Playnite.Api, fixture.Logger.Object, CancellationToken.None, mapping, roms.ToList());

        private void Ready(string host = "http://romm.local")
        {
            fixture.ResetGames();
            fixture.Playnite.Genres.Clear();
            fixture.Playnite.Categories.Clear();
            fixture.Playnite.Series.Clear();
            fixture.Playnite.Features.Clear();
            fixture.Playnite.Regions.Clear();
            fixture.Playnite.AgeRatings.Clear();
            fixture.Playnite.CompletionStatuses.Clear();
            fixture.Playnite.GameRelations.Clear();
            fixture.ApplySettings(s => s.Host = host);
        }

        [Fact]
        public async Task ProcessData_SingleRom_AddsOneGameToLibrary()
        {
            Ready();
            var (newGames, ids) = await Importer(MakeMapping(), JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!).ProcessData();

            Assert.Single(newGames);
            Assert.Single(ids);
            Assert.Single(fixture.Playnite.Games);
        }

        [Fact]
        public async Task ProcessData_SingleRom_SetsCorrectName()
        {
            Ready();
            await Importer(MakeMapping(), JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!).ProcessData();
            Assert.Equal("Pokemon Emerald Version", fixture.Playnite.Games[0].Name);
        }

        [Fact]
        public async Task ProcessData_SingleRom_SetsLibraryIdAndSourceIdToPluginId()
        {
            Ready();
            await Importer(MakeMapping(), JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!).ProcessData();

            var game = fixture.Playnite.Games[0];
            Assert.Equal(GravitonPlugin.Id, game.LibraryId);
            Assert.Equal(GravitonPlugin.Id, game.SourceId);
        }

        [Fact]
        public async Task ProcessData_SingleRom_LibraryGameIdIsRomIdColonSha1()
        {
            Ready();
            await Importer(MakeMapping(), JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!).ProcessData();

            Assert.Equal("101:AAB0DD9D12B79B2A67B64A4C5F98F37DCEE60AA1",
                         fixture.Playnite.Games[0].LibraryGameId);
        }

        [Fact]
        public async Task ProcessData_SingleRom_SetsEstimatedInstallSize()
        {
            Ready();
            await Importer(MakeMapping(), JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!).ProcessData();
            Assert.Equal((ulong)16_777_216, fixture.Playnite.Games[0].EstimatedInstallSize);
        }

        [Fact]
        public async Task ProcessData_RomUserRating9_UserScoreIs90()
        {
            Ready();
            await Importer(MakeMapping(), JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!).ProcessData();
            Assert.Equal(90, fixture.Playnite.Games[0].UserScore);
        }

        [Fact]
        public async Task ProcessData_AverageRating88Point5_CommunityScoreIs88()
        {
            Ready();
            await Importer(MakeMapping(), JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!).ProcessData();
            Assert.Equal(88, fixture.Playnite.Games[0].CommunityScore);
        }

        [Fact]
        public async Task ProcessData_RomWithNoRating_UserScoreIsMinusOne()
        {
            Ready();
            var rom = JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRomMinimal)!;
            await Importer(MakeMapping(), rom).ProcessData();
            Assert.Equal(-1, fixture.Playnite.Games[0].UserScore);
        }

        [Fact]
        public async Task ProcessData_RomInFavoritesCollection_FavoriteIsTrue()
        {
            Ready();
            await Importer(MakeMapping(), JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRomInFavourites)!).ProcessData();
            Assert.True(fixture.Playnite.Games[0].Favorite);
        }

        [Fact]
        public async Task ProcessData_RomNotInFavorites_FavoriteIsFalse()
        {
            Ready();
            await Importer(MakeMapping(), JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRomMinimal)!).ProcessData();
            Assert.False(fixture.Playnite.Games[0].Favorite);
        }

        [Fact]
        public async Task ProcessData_HiddenRomUser_HiddenIsTrue()
        {
            Ready();
            await Importer(MakeMapping(), JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRomHidden)!).ProcessData();
            Assert.True(fixture.Playnite.Games[0].Hidden);
        }

        [Fact]
        public async Task ProcessData_CompletedStatus_MapsToPlayniteCompletionStatus()
        {
            Ready();
            fixture.Playnite.AddCompletionStatus("Completed");

            await Importer(MakeMapping(), JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!).ProcessData();

            var statusId = fixture.Playnite.CompletionStatuses[0].Id;
            Assert.Equal(statusId, fixture.Playnite.Games[0].CompletionStatusId);
        }

        [Fact]
        public async Task ProcessData_UnknownStatus_CompletionStatusIdIsNull()
        {
            Ready();
            await Importer(MakeMapping(), JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!).ProcessData();
            Assert.Null(fixture.Playnite.Games[0].CompletionStatusId);
        }

        [Fact]
        public async Task ProcessData_RomWithIgdbId_AddsIgdbExternalIdentifier()
        {
            Ready();
            await Importer(MakeMapping(), JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!).ProcessData();

            Assert.Contains(fixture.Playnite.Games[0].ExternalIdentifiers!,
                            ei => ei.TypeId == "igdb" && ei.IdValue == "4972");
        }

        [Fact]
        public async Task ProcessData_RomWithRommId_AddsRommExternalIdentifier()
        {
            Ready();
            await Importer(MakeMapping(), JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!).ProcessData();

            Assert.Contains(fixture.Playnite.Games[0].ExternalIdentifiers!, ei => ei.TypeId == "romm" && ei.IdValue == "101");
        }

        [Fact]
        public async Task ProcessData_RomWithSlug_AddsIgdbWebLink()
        {
            Ready();
            await Importer(MakeMapping(), JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!).ProcessData();

            Assert.Contains(fixture.Playnite.Games[0].Links!, l => l.Url!.Contains("pokemon-emerald-version") && l.Url.Contains("igdb.com"));
        }

        [Fact]
        public async Task ProcessData_RomWithSSId_AddsScreenscraperLinkAndIdentifier()
        {
            Ready();
            await Importer(MakeMapping(), JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!).ProcessData();

            Assert.Contains(fixture.Playnite.Games[0].Links!, l => l.Url!.Contains("screenscraper") && l.Url.Contains("512"));
            Assert.Contains(fixture.Playnite.Games[0].ExternalIdentifiers!, ei => ei.TypeId == "screenscraper" && ei.IdValue == "512");
        }

        [Fact]
        public async Task ProcessData_RomWithGenres_GenresAddedToLibrary()
        {
            Ready();
            await Importer(MakeMapping(), JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!).ProcessData();

            Assert.Contains(fixture.Playnite.Genres, g => g.Name == "Role-playing (RPG)");
            Assert.Contains(fixture.Playnite.Genres, g => g.Name == "Adventure");
        }

        [Fact]
        public async Task ProcessData_RomWithMetadatumCollections_AddedAsCategories()
        {
            Ready();
            await Importer(MakeMapping(), JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!).ProcessData();

            Assert.Contains(fixture.Playnite.Categories, c => c.Name == "RPG Classics");
        }

        [Fact]
        public async Task ProcessData_FavoritesInMetadatumCollections_NotAddedAsCategory()
        {
            Ready();
            var rom = JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!;
            rom.Metadatum!.Collections = new List<string> { "Favorites", "RPG Classics" };

            await Importer(MakeMapping(), rom).ProcessData();

            Assert.DoesNotContain(fixture.Playnite.Categories, c => c.Name == "Favorites");
            Assert.Contains(fixture.Playnite.Categories, c => c.Name == "RPG Classics");
        }

        [Fact]
        public async Task ProcessData_RomWithFranchises_AddedAsSeries()
        {
            Ready();
            await Importer(MakeMapping(), JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!).ProcessData();
            Assert.Contains(fixture.Playnite.Series, s => s.Name == "Pokemon");
        }

        [Fact]
        public async Task ProcessData_RomWithGameModes_AddedAsFeatures()
        {
            Ready();
            await Importer(MakeMapping(), JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!).ProcessData();
            Assert.Contains(fixture.Playnite.Features, f => f.Name == "Single player");
            Assert.Contains(fixture.Playnite.Features, f => f.Name == "Multiplayer");
        }

        [Fact]
        public async Task ProcessData_RomWithRegions_AddedToRegions()
        {
            Ready();
            await Importer(MakeMapping(), JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!).ProcessData();
            Assert.Contains(fixture.Playnite.Regions, r => r.Name == "USA");
            Assert.Contains(fixture.Playnite.Regions, r => r.Name == "Europe");
        }

        [Fact]
        public async Task ProcessData_RomWithIgdbAgeRatings_AddedToAgeRatings()
        {
            Ready();
            await Importer(MakeMapping(), JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!).ProcessData();
            Assert.Contains(fixture.Playnite.AgeRatings, a => a.Name == "PEGI 3");
        }

        [Fact]
        public async Task ProcessData_RomAlreadyImported_NotAddedAgain()
        {
            Ready();
            fixture.SeedImportedGame(new Playnite.Game { LibraryGameId = "101:AAB0DD9D12B79B2A67B64A4C5F98F37DCEE60AA1" });

            var (newGames, ids) = await Importer(MakeMapping(), JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!).ProcessData();

            Assert.Empty(newGames);
            Assert.Single(ids);
            Assert.Single(fixture.Playnite.Games);
        }

        [Fact]
        public async Task ProcessData_ExistingRomMovedToFavorites_FavoriteFlagUpdated()
        {
            Ready();
            var existing = new Playnite.Game
            {
                LibraryGameId = "303:BA4F07C8F01B1219E4BF2E7E83E5D64E55B43E97",
                Favorite = false
            };
            fixture.SeedImportedGame(existing);

            await Importer(MakeMapping(), JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRomInFavourites)!).ProcessData();

            Assert.True(existing.Favorite);
        }


        [Fact]
        public async Task ProcessData_RomWithEmptyFileName_IsSkipped()
        {
            Ready();
            var (newGames, ids) = await Importer(MakeMapping(), JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRomNoFileName)!).ProcessData();

            Assert.Empty(newGames);
            Assert.Empty(ids);
            Assert.Empty(fixture.Playnite.Games);
        }


        [Fact]
        public async Task ProcessData_RomWithNoSha1_GeneratesHashAndImports()
        {
            Ready();
            var rom = JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRomMinimal)!;
            rom.SHA1 = null;

            await Importer(MakeMapping(), rom).ProcessData();

            Assert.Single(fixture.Playnite.Games);
            var parts = fixture.Playnite.Games[0].LibraryGameId!.Split(':');
            Assert.Equal(2, parts.Length);
            Assert.Matches("^[0-9A-Fa-f]{40}$", parts[1]);
        }

        [Fact]
        public async Task ProcessData_SingleFileRom_WritesJsonFileToGamesDir()
        {
            Ready();
            await Importer(MakeMapping(), JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!).ProcessData();

            var path = Path.Combine(fixture.TempDir, "Games", "AAB0DD9D12B79B2A67B64A4C5F98F37DCEE60AA1.json");
            Assert.True(File.Exists(path));
        }

        [Fact]
        public async Task ProcessData_SingleFileRom_JsonContainsCorrectDownloadUrl()
        {
            Ready();
            await Importer(MakeMapping(), JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!).ProcessData();

            var json = File.ReadAllText(Path.Combine(fixture.TempDir, "Games", "AAB0DD9D12B79B2A67B64A4C5F98F37DCEE60AA1.json"));

            Assert.Contains("http://romm.local", json);
            Assert.Contains("/api/roms/201/", json);
        }

        [Fact]
        public async Task ProcessData_MultiFileRom_WritesJsonWithRomLevelDownloadUrl()
        {
            Ready("http://romm.local");
            fixture.ApplySettings(s => s.Host = "http://romm.local");

            var rom = JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRomMultiFile)!;
            await Importer(MakeMapping("PlayStation"), rom).ProcessData();

            var sha1 = "CCDDAABB11223344EEFF5566778899AABBCCDDEE";
            var json = File.ReadAllText(Path.Combine(fixture.TempDir, "Games", $"{sha1}.json"));

            Assert.Contains("http://romm.local", json);
            Assert.Contains("/api/roms/404/", json);
        }

        [Fact]
        public async Task ProcessData_MultiFileRom_DetermineFilePicksShallowPath()
        {
            Ready();
            var rom = JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRomMultiFile)!;
            await Importer(MakeMapping("PlayStation"), rom).ProcessData();

            var sha1 = "CCDDAABB11223344EEFF5566778899AABBCCDDEE";
            Assert.True(File.Exists(Path.Combine(fixture.TempDir, "Games", $"{sha1}.json")));
        }
    }
}