using System.Text.Json.Serialization;

namespace Graviton.Models.RomM.Metadata
{
    public class Metadatum
    {
        [JsonPropertyName("rom_id")]
        public int Id { get; set; }

        [JsonPropertyName("genres")]
        public List<string>? Genres { get; set; }

        [JsonPropertyName("franchises")]
        public List<string>? Franchises { get; set; }

        [JsonPropertyName("collections")]
        public List<string>? Collections { get; set; }

        [JsonPropertyName("companies")]
        public List<string>? Companies { get; set; }

        [JsonPropertyName("game_modes")]
        public List<string>? Gamemodes { get; set; }

        [JsonPropertyName("age_ratings")]
        public List<string>? AgeRatings { get; set; }

        [JsonPropertyName("first_release_date")]
        public long? ReleaseDate { get; set; }

        [JsonPropertyName("average_rating")]
        public float? AverageRating { get; set; }

    }
}
