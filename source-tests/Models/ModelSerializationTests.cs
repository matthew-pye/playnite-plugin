using Graviton.Models.RomM.Rom;

using Graviton.Tests.Fakes;

using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Models
{

    [Collection(GravitonCollection.Name)]
    public class ModelSerializationTests
    {

        [Fact]
        public void RomMRom_Deserialise_MapsAllIds()
        {
            var rom = JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!;

            Assert.Equal(101,  rom.Id);
            Assert.Equal(4972, rom.IgdbId);
            Assert.Equal(512,  rom.SSId);
            Assert.Equal(7653, rom.RAId);
            Assert.Equal(8801, rom.HasheousId);
            Assert.Equal(3631, rom.HLTBId);
            Assert.Equal(1,    rom.PlatformId);
        }

        [Fact]
        public void RomMRom_Deserialise_MapsFileSystemFields()
        {
            var rom = JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!;

            Assert.Equal("Pokemon - Emerald Version (USA, Europe).gba", rom.FileName);
            Assert.Equal("Pokemon - Emerald Version",                   rom.FileNameNoTags);
            Assert.Equal("Pokemon - Emerald Version (USA, Europe)",     rom.FileNameNoExt);
            Assert.Equal("gba",         rom.FileExtension);
            Assert.Equal("gba",         rom.FilePath);
            Assert.Equal((ulong)16_777_216, rom.FileSizeBytes);
        }

        [Fact]
        public void RomMRom_Deserialise_MapsCoreGameFields()
        {
            var rom = JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!;

            Assert.Equal("Pokemon Emerald Version",   rom.Name);
            Assert.Equal("pokemon-emerald-version",   rom.Slug);
            Assert.NotNull(rom.Summary);
            Assert.Equal("AAB0DD9D12B79B2A67B64A4C5F98F37DCEE60AA1", rom.SHA1);
            Assert.Equal("00D76021",                  rom.CRC);
            Assert.True(rom.HasSimpleSingleFile);
            Assert.False(rom.HasMultipleFiles);
            Assert.False(rom.HasNestedSingleFile);
        }

        [Fact]
        public void RomMRom_Deserialise_MapsRegionsAndLanguages()
        {
            var rom = JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!;

            Assert.Contains("USA",    rom.Regions!);
            Assert.Contains("Europe", rom.Regions!);
            Assert.Contains("en",     rom.Languages!);
        }

        [Fact]
        public void RomMRom_Deserialise_MapsDates()
        {
            var rom = JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!;

            Assert.Equal(new DateTime(2024, 3, 1, 12, 0, 0, DateTimeKind.Utc), rom.CreatedAt);
        }


        [Fact]
        public void RomMRom_Deserialise_MapsFiles()
        {
            var rom = JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!;

            Assert.Single(rom.Files);
            var file = rom.Files[0];
            Assert.Equal(201,        file.Id);
            Assert.Equal("Pokemon - Emerald Version (USA, Europe).gba", file.FileName);
            Assert.Equal("gba/Pokemon - Emerald Version (USA, Europe).gba", file.FullPath);
        }

        [Fact]
        public void RomMRom_Deserialise_MultipleFiles_AllPresent()
        {
            var rom = JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRomMultiFile)!;

            Assert.Equal(3, rom.Files.Count);
            Assert.True(rom.HasMultipleFiles);
        }

        [Fact]
        public void Metadatum_Deserialise_MapsAllFields()
        {
            var rom = JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!;
            var m   = rom.Metadatum!;

            Assert.Equal(101, m.Id);
            Assert.Contains("Role-playing (RPG)", m.Genres!);
            Assert.Contains("Adventure",          m.Genres!);
            Assert.Contains("Pokemon",            m.Franchises!);
            Assert.Contains("RPG Classics",       m.Collections!);
            Assert.Contains("Game Freak",         m.Companies!);
            Assert.Contains("Single player",      m.Gamemodes!);
            Assert.Equal(1_109_203_200_000L,      m.ReleaseDate);
            Assert.Equal(88.5f, m.AverageRating!.Value, precision: 1);
        }

        [Fact]
        public void IGDBMetadata_Deserialise_MapsAgeRatings()
        {
            var rom = JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!;
            var ar  = rom.IgdbMetadata!.AgeRatings!;

            Assert.Single(ar);
            Assert.Equal("PEGI", ar[0].RatingBoard);
            Assert.Equal("3",    ar[0].Rating);
        }


        [Fact]
        public void HLTBMetadata_Deserialise_MapsAllFields()
        {
            var rom = JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!;
            var h   = rom.HLTBMetadata!;

            Assert.Equal(25u,  h.MainStory);
            Assert.Equal(40u,  h.MainStoryExtra);  
            Assert.Equal(200u, h.Completionist);
            Assert.Equal(0u,   h.AllStyles);
        }

        [Fact]
        public void RomMRomUser_Deserialise_MapsAllFields()
        {
            var rom  = JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!;
            var user = rom.RomUser!;

            Assert.Equal(1,               user.Id);
            Assert.Equal(42,              user.UserId);
            Assert.False(user.IsMainSibling);
            Assert.Equal(9,               user.Rating);
            Assert.Equal("completed_100", user.Status);
            Assert.False(user.Hidden);
            Assert.False(user.Backlogged);
            Assert.False(user.NowPlaying);
            Assert.NotNull(user.LastPlayed);
        }

        [Fact]
        public void RomMRomUser_Deserialise_HiddenRom_SetsHiddenTrue()
        {
            var rom = JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRomHidden)!;
            Assert.True(rom.RomUser!.Hidden);
            Assert.Equal("not_played", rom.RomUser.Status);
        }

        [Theory]
        [InlineData("never_playing", "Never Playing")]
        [InlineData("retired",       "Abandoned")]
        [InlineData("incomplete",    "On Hold")]
        [InlineData("finished",      "Played")]
        [InlineData("completed_100", "Completed")]
        [InlineData("backlogged",    "Plan to Play")]
        [InlineData("now_playing",   "Playing")]
        [InlineData("not_played",    "Not Played")]
        public void CompletionStatusMap_ContainsExpectedMapping(string romMStatus, string playniteLabel)
        {
            Assert.True(RomMRomUser.CompletionStatusMap.TryGetValue(romMStatus, out var actual),
                $"Map is missing key '{romMStatus}'");
            Assert.Equal(playniteLabel, actual);
        }

        [Fact]
        public void CompletionStatusMap_HasExactlyEightEntries()
        {
            Assert.Equal(8, RomMRomUser.CompletionStatusMap.Count);
        }

        [Fact]
        public void RomMRom_Deserialise_UserCollections()
        {
            var rom = JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRom)!;

            Assert.Single(rom.Collections!);
            Assert.Equal("My GBA Favourites", rom.Collections![0].Name);
            Assert.False(rom.Collections![0].IsFavorite);
        }

        [Fact]
        public void RomMRom_Deserialise_FavoritesCollection()
        {
            var rom = JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRomInFavourites)!;

            Assert.Single(rom.Collections!);
            Assert.Equal("Favorites", rom.Collections![0].Name);
            Assert.True(rom.Collections![0].IsFavorite);
        }

        [Fact]
        public void RomMRom_MinimalResponse_NullableFieldsAreNull()
        {
            var rom = JsonSerializer.Deserialize<RomMRom>(FakeApiResponses.RomMRomMinimal)!;

            Assert.Null(rom.IgdbId);
            Assert.Null(rom.SSId);
            Assert.Null(rom.RAId);
            Assert.Null(rom.Metadatum);
            Assert.Null(rom.IgdbMetadata);
            Assert.Null(rom.HLTBMetadata);
            Assert.Null(rom.RomUser);
            Assert.Null(rom.Collections);
            Assert.Null(rom.Regions);
            Assert.False(rom.Processed);
        }
    }
}
