using Graviton.Models.RomM.Platform;
using Graviton.Models.RomM.Rom;

using Graviton.Tests.Fakes;

using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Models
{

    [Collection(GravitonCollection.Name)]
    public class ModelSerializationTests
    {
        private static RomMRom DeserializeRom(string json) => JsonSerializer.Deserialize<RomMRom>(json)!;

        #region RomMRom

        [Fact]
        public void RomMRom_Deserialise_IDs()
        {
            var rom = DeserializeRom(FakeApiResponses.RomMRom);

            Assert.Equal(101, rom.Id);
            Assert.Equal(4972, rom.IgdbId);
            Assert.Equal(512, rom.SSId);
            Assert.Equal(7653, rom.RAId);
            Assert.Equal(8801, rom.HasheousId);
            Assert.Equal(3631, rom.HLTBId);
            Assert.Equal(1, rom.PlatformId);
        }

        [Fact]
        public void RomMRom_Deserialise_FileSystem()
        {
            var rom = DeserializeRom(FakeApiResponses.RomMRom);

            Assert.Equal("Pokemon - Emerald Version (USA, Europe).gba", rom.FileName);
            Assert.Equal("Pokemon - Emerald Version", rom.FileNameNoTags);
            Assert.Equal("Pokemon - Emerald Version (USA, Europe)", rom.FileNameNoExt);
            Assert.Equal("gba", rom.FileExtension);
            Assert.Equal("gba", rom.FilePath);
            Assert.Equal((ulong)16_777_216, rom.FileSizeBytes);
        }

        [Fact]
        public void RomMRom_Deserialise_ROMDetails()
        {
            var rom = DeserializeRom(FakeApiResponses.RomMRom);

            Assert.Equal("Pokemon Emerald Version", rom.Name);
            Assert.Equal("pokemon-emerald-version", rom.Slug);
            Assert.NotNull(rom.Summary);
            Assert.Equal("AAB0DD9D12B79B2A67B64A4C5F98F37DCEE60AA1", rom.SHA1);
            Assert.Equal("00D76021", rom.CRC);
        }

        [Fact]
        public void RomMRom_Deserialise_IsSimpleSingle()
        {
            var rom = DeserializeRom(FakeApiResponses.RomMRom);

            Assert.True(rom.HasSimpleSingleFile);
            Assert.False(rom.HasNestedSingleFile);
            Assert.False(rom.HasMultipleFiles);
        }

        [Fact]
        public void RomMRom_Deserialise_IsMultiFile()
        {
            var rom = DeserializeRom(FakeApiResponses.RomMRomMultiFile);

            Assert.False(rom.HasSimpleSingleFile);
            Assert.False(rom.HasNestedSingleFile);
            Assert.True(rom.HasMultipleFiles);
        }

        [Fact]
        public void RomMRom_Deserialise_RegionsAndLanguage()
        {
            var rom = DeserializeRom(FakeApiResponses.RomMRom);

            Assert.Contains("USA", rom.Regions!);
            Assert.Contains("en", rom.Languages!);
        }

        [Fact]
        public void RomMRom_Deserialise_CreatedAt()
        {
            var rom = DeserializeRom(FakeApiResponses.RomMRom);

            Assert.Equal(new DateTime(2024, 3, 1, 12, 0, 0, DateTimeKind.Utc), rom.CreatedAt);
        }

        [Fact]
        public void RomMRom_Deserialise_Files_SingleFile()
        {
            var rom = DeserializeRom(FakeApiResponses.RomMRom);

            Assert.Single(rom.Files);
        }

        [Fact]
        public void RomMRom_Deserialise_Files_SingleFile_HasData()
        {
            var file = DeserializeRom(FakeApiResponses.RomMRom).Files[0];

            Assert.Equal(201, file.Id);
            Assert.Equal("Pokemon - Emerald Version (USA, Europe).gba", file.FileName);
            Assert.Equal("gba/Pokemon - Emerald Version (USA, Europe).gba", file.FullPath);
        }

        [Fact]
        public void RomMRom_Deserialise_Files_MultipleFile()
        {
            var rom = DeserializeRom(FakeApiResponses.RomMRomMultiFile);

            Assert.Equal(3, rom.Files.Count);
        }

        [Fact]
        public void RomMRomMinimal_Deserialise_NullablesAreNull()
        {
            var rom = DeserializeRom(FakeApiResponses.RomMRomMinimal);

            Assert.Null(rom.IgdbId);
            Assert.Null(rom.SSId);
            Assert.Null(rom.RAId);
            Assert.Null(rom.Metadatum);
            Assert.Null(rom.IgdbMetadata);
            Assert.Null(rom.SSMetadata);
            Assert.Null(rom.HLTBMetadata);
            Assert.Null(rom.RomUser);
            Assert.Null(rom.Collections);
            Assert.Null(rom.Regions);
        }

        [Fact]
        public void RomMRomMinimal_Deserialise_RequiredFieldsArePresent()
        {
            var rom = DeserializeRom(FakeApiResponses.RomMRomMinimal);

            Assert.Equal(202, rom.Id);
            Assert.Equal("Super Mario World", rom.Name);
            Assert.Equal("6B47BB75D16514B6A476AA0C73A683A2A4C18765", rom.SHA1);
            Assert.Single(rom.Files);
        }

        [Fact]
        public void RomMRomMinimal_Deserialise_ProcessedStartsFalse()
        {
            var rom = DeserializeRom(FakeApiResponses.RomMRomMinimal);

            Assert.False(rom.Processed);
        }

        [Fact]
        public void RomMRom_Deserialise_CoverPaths()
        {
            var rom = DeserializeRom(FakeApiResponses.RomMRom);

            Assert.Equal("library/covers/gba/pokemon-emerald-version/small.png", rom.PathCoverS);
            Assert.Equal("library/covers/gba/pokemon-emerald-version/large.png", rom.PathCoverL);
        }

        [Fact]
        public void RomMRomMinimal_Deserialise_NoCoverPaths()
        {
            var rom = DeserializeRom(FakeApiResponses.RomMRomMinimal);

            Assert.Null(rom.PathCoverS);
            Assert.Null(rom.PathCoverL);
        }

        #endregion

        #region Metadatum

        [Fact]
        public void Metadatum_Deserialise_ROMIdAndAverageRating()
        {
            var metadatum = DeserializeRom(FakeApiResponses.RomMRom).Metadatum!;

            Assert.Equal(101, metadatum.Id);
            Assert.Equal(88.5f, metadatum.AverageRating!.Value, precision: 1);
        }

        [Fact]
        public void Metadatum_Deserialise_ReleaseDate()
        {
            var metadatum = DeserializeRom(FakeApiResponses.RomMRom).Metadatum!;

            Assert.Equal(1_109_203_200_000L, metadatum.ReleaseDate);
        }

        [Fact]
        public void Metadatum_Deserialise_Genres()
        {
            var metadatum = DeserializeRom(FakeApiResponses.RomMRom).Metadatum!;

            Assert.Contains("Role-playing (RPG)", metadatum.Genres!);
            Assert.Contains("Adventure", metadatum.Genres!);
        }

        [Fact]
        public void Metadatum_Deserialise_Franchises()
        {
            var metadatum = DeserializeRom(FakeApiResponses.RomMRom).Metadatum!;

            Assert.Contains("Pokemon", metadatum.Franchises!);
        }

        [Fact]
        public void Metadatum_Deserialise_Collections()
        {
            var metadatum = DeserializeRom(FakeApiResponses.RomMRom).Metadatum!;

            Assert.Contains("RPG Classics", metadatum.Collections!);
        }

        [Fact]
        public void Metadatum_Deserialise_Companies()
        {
            var metadatum = DeserializeRom(FakeApiResponses.RomMRom).Metadatum!;

            Assert.Contains("Game Freak", metadatum.Companies!);
            Assert.Contains("Nintendo", metadatum.Companies!);
        }

        [Fact]
        public void Metadatum_Deserialise_GameModes()
        {
            var metadatum = DeserializeRom(FakeApiResponses.RomMRom).Metadatum!;

            Assert.Contains("Single player", metadatum.Gamemodes!);
            Assert.Contains("Multiplayer", metadatum.Gamemodes!);
        }

        #endregion

        #region 3rdPartyMetadata

        [Fact]
        public void IGDBMetadata_Deserialise_AgeRatings()
        {
            var igdb = DeserializeRom(FakeApiResponses.RomMRom).IgdbMetadata!;

            Assert.Single(igdb.AgeRatings!);
            Assert.Equal("PEGI", igdb.AgeRatings![0].RatingBoard);
            Assert.Equal("3", igdb.AgeRatings![0].Rating);
        }

        [Fact]
        public void HLTBMetadata_Deserialise_CompletionTimes()
        {
            var hltb = DeserializeRom(FakeApiResponses.RomMRom).HLTBMetadata!;

            Assert.Equal(25u, hltb.MainStory);
            Assert.Equal(40u, hltb.MainStoryExtra);
            Assert.Equal(200u, hltb.Completionist);
            Assert.Equal(0u, hltb.AllStyles);
        }

        [Fact]
        public void SSMetadata_Deserialise_ScoreAndGenres()
        {
            var ss = DeserializeRom(FakeApiResponses.RomMRom).SSMetadata!;

            Assert.Equal("87", ss.Score);
            Assert.Contains("Role Playing Game", ss.Genres);
        }

        #endregion

        #region RomMRomUser

        [Fact]
        public void RomMRomUser_Deserialise_UserROMData()
        {
            var user = DeserializeRom(FakeApiResponses.RomMRom).RomUser!;

            Assert.Equal(1, user.Id);
            Assert.Equal(42, user.UserId);
            Assert.False(user.IsMainSibling);
        }

        [Fact]
        public void RomMRomUser_Deserialise_UserData()
        {
            var user = DeserializeRom(FakeApiResponses.RomMRom).RomUser!;

            Assert.Equal(9, user.Rating);
            Assert.Equal("completed_100", user.Status);
            Assert.False(user.Backlogged);
            Assert.False(user.NowPlaying);
            Assert.NotNull(user.LastPlayed);
        }

        [Fact]
        public void RomMRomUser_Deserialise_HiddenIsFalse()
        {
            var user = DeserializeRom(FakeApiResponses.RomMRom).RomUser!;

            Assert.False(user.Hidden);
        }

        [Fact]
        public void RomMRomUser_Deserialise_HiddenIsTrue()
        {
            var user = DeserializeRom(FakeApiResponses.RomMRomHidden).RomUser!;

            Assert.True(user.Hidden);
        }

        [Theory]
        [InlineData("never_playing", "Never Playing")]
        [InlineData("retired", "Abandoned")]
        [InlineData("incomplete", "On Hold")]
        [InlineData("finished", "Played")]
        [InlineData("completed_100", "Completed")]
        [InlineData("backlogged", "Plan to Play")]
        [InlineData("now_playing", "Playing")]
        [InlineData("not_played", "Not Played")]
        public void CompletionStatusMap_ContainsExpectedStatuses(string romMStatus, string playniteLabel)
        {
            Assert.True(RomMRomUser.CompletionStatusMap.TryGetValue(romMStatus, out var actual), $"Map is missing key '{romMStatus}'");
            Assert.Equal(playniteLabel, actual);
        }

        #endregion

        #region RomMCollection

        [Fact]
        public void RomMRom_Deserialise_NonFavouriteCollection()
        {
            var c = DeserializeRom(FakeApiResponses.RomMRom).Collections!.Single();

            Assert.Equal(10, c.Id);
            Assert.Equal("My GBA Favourites", c.Name);
            Assert.Contains(101, c.RomIDs);
            Assert.False(c.IsFavorite);
        }

        [Fact]
        public void RomMRom_Deserialise_FavouriteCollection()
        {
            var c = DeserializeRom(FakeApiResponses.RomMRomInFavourites).Collections!.Single();

            Assert.Equal("Favorites", c.Name);
            Assert.True(c.IsFavorite);
        }

        [Fact]
        public void RomMCollection_Deserialise_HasBeenUpdatedStartsFalse()
        {
            var c = DeserializeRom(FakeApiResponses.RomMRom).Collections!.Single();

            Assert.False(c.HasBeenUpdated);
        }

        #endregion

        #region RomMPlatform

        [Fact]
        public void RomMPlatform_Deserialise()
        {
            var platforms = JsonSerializer.Deserialize<List<RomMPlatform>>(FakeApiResponses.PlatformList)!;

            Assert.Single(platforms);
            var p = platforms[0];
            Assert.Equal(1, p.Id);
            Assert.Equal("gba", p.Slug);
            Assert.Equal("gba", p.FsSlug);
            Assert.Equal("Game Boy Advance", p.Name);
            Assert.Equal(42, p.RomCount);
            Assert.Equal(24UL, p.IgdbId);
            Assert.Equal("https://example.com/gba.png", p.LogoPath);
            Assert.Equal("Game Boy Advance", p.DisplayName);
        }

        #endregion

    }
}
