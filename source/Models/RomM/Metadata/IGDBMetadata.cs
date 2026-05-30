using System.Text.Json.Serialization;

namespace Graviton.Models.RomM.Metadata
{
    public struct IGDBAgeRating
    {
        [JsonPropertyName("rating")]
        public string Rating { get; set; }

        [JsonPropertyName("category")]
        public string RatingBoard { get; set; }

        [JsonPropertyName("rating_cover_url")]
        public string RatingIcon { get; set; }
    }

    public class IGDBMetadata
    {
        [JsonPropertyName("total_rating")]
        public string? TotalRating { get; set; }

        [JsonPropertyName("aggregated_rating")]
        public string? AggregatedRating { get; set; }

        [JsonPropertyName("genres")]
        public List<string>? Genres { get; set; }

        [JsonPropertyName("franchises")]
        public List<string>? Franchises { get; set; }

        [JsonPropertyName("age_ratings")]
        public List<IGDBAgeRating>? AgeRatings { get; set; }

    }
}