using System.Text.Json.Serialization;

namespace RomMLibrary.Models.RomM.Metadata
{
    public struct AgeRating
    {
        [JsonPropertyName("rating")]
        public float Rating { get; set; }
        [JsonPropertyName("category")]
        public float RatingBoard { get; set; }
        [JsonPropertyName("rating_cover_url")]
        public float RatingIcon { get; set; }
    }

    public class IGDBMetadata
    {
        [JsonPropertyName("total_rating")]
        public float TotalRating { get; set; }

        [JsonPropertyName("aggregated_rating")]
        public float AggregatedRating { get; set; }

        [JsonPropertyName("genres")]
        public List<string>? Genres { get; set; }

        [JsonPropertyName("franchises")]
        public List<string>? Franchises { get; set; }

        [JsonPropertyName("age_ratings")]
        public List<AgeRating>? AgeRatings { get; set; }

    }
}