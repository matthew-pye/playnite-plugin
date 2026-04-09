using System.Text.Json.Serialization;

namespace RomMLibrary.Models.RomM.Rom
{
    public class AgeRating
    {
        [JsonPropertyName("rating")]
        public string? Rating { get; set; }

        [JsonPropertyName("category")]
        public string? RatingsBoard { get; set; }

        [JsonPropertyName("rating_cover_url")]
        public string? RatingsCover { get; set; }
    }

    public class RomMIgdbMetadata
    {
        [JsonPropertyName("total_rating")]
        public string? TotalRating { get; set; }

        [JsonPropertyName("aggregated_rating")]
        public string? AggregatedRating { get; set; }

        [JsonPropertyName("first_release_date")]
        public long? FirstReleaseDate { get; set; }

        [JsonPropertyName("genres")]
        public List<string> Genres { get; set; } = new List<string>();

        [JsonPropertyName("franchises")]
        public List<string> Franchises { get; set; } = new List<string>();

        [JsonPropertyName("alternative_names")]
        public List<string> AlternativeNames { get; set; } = new List<string>();

        [JsonPropertyName("collections")]
        public List<string> Collections { get; set; } = new List<string>();

        [JsonPropertyName("companies")]
        public List<string> Companies { get; set; } = new List<string>();

        [JsonPropertyName("game_modes")]
        public List<string> GameModes { get; set; } = new List<string>();

        [JsonPropertyName("age_ratings")]
        public List<AgeRating> AgeRatings { get; set; } = new List<AgeRating>();

        [JsonPropertyName("platforms")]
        public List<RomMRomPlatform> Platforms { get; set; } = new List<RomMRomPlatform>();

    }
}
