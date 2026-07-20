using Graviton.Import;
using Graviton.Models.RomM.Rom;

using Graviton.Tests.Fakes;

using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Import
{
    [Collection(GravitonCollection.Name)]
    public class MetadataProviderTests
    {
        private static RomMRom DeserializeRom(string json) => JsonSerializer.Deserialize<RomMRom>(json)!;

        // RomM 5.x dropped the "has_cover" flag from /api/roms responses - cover
        // presence is now signalled purely by path_cover_large being populated.
        [Fact]
        public void ResolveCoverPath_RomHasCoverPath_ReturnsPathCoverLarge()
        {
            var rom = DeserializeRom(FakeApiResponses.RomMRom);

            var cover = GravitonMetadataProviderGameSession.ResolveCoverPath(rom);

            Assert.Equal("library/covers/gba/pokemon-emerald-version/large.png", cover);
        }

        [Fact]
        public void ResolveCoverPath_RomHasNoCoverPath_ReturnsNull()
        {
            var rom = DeserializeRom(FakeApiResponses.RomMRomMinimal);

            var cover = GravitonMetadataProviderGameSession.ResolveCoverPath(rom);

            Assert.Null(cover);
        }

        [Fact]
        public void ResolveCoverPath_NullRom_ReturnsNull()
        {
            Assert.Null(GravitonMetadataProviderGameSession.ResolveCoverPath(null));
        }
    }
}
