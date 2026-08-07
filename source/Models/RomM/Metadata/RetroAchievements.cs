using System.Text.Json.Serialization;

namespace Graviton.Models.RomM.Metadata
{
    public class RetroAchievement
    {
        [JsonPropertyName("id")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? ID { get; set; }

        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("date_hardcore")]
        public string? HardcoreDate { get; set; }
    }

    public class RetroAchievementGame
    {
        [JsonPropertyName("rom_ra_id")]
        public int? ID { get; set; }

        [JsonPropertyName("max_possible")]
        public int? AchievementCount { get; set; }

        [JsonPropertyName("num_awarded")]
        public int? EarnedAchievementsCount { get; set; }

        [JsonPropertyName("num_awarded_hardcore")]
        public int? HardcoreEarnedAchievementsCount { get; set; }

        [JsonPropertyName("most_recent_awarded_date")]
        public DateTime? LastAchievementUnlockDate { get; set; }

        [JsonPropertyName("highest_award_kind")]
        public string? Award { get; set; }

        [JsonPropertyName("earned_achievements")]
        public List<RetroAchievement>? EarnedAchievements { get; set; }
    }

    public class RetroAchievementProgression
    {
        [JsonPropertyName("total")]
        public int? RAGamesCount { get; set; }

        [JsonPropertyName("results")]
        public List<RetroAchievementGame>? RAGames { get; set; }
    }

    public class FullRetroAchievement
    {
        [JsonPropertyName("ra_id")]
        public int? ID { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("points")]
        public int? Points { get; set; }

        [JsonPropertyName("num_awarded")]
        public int? NumAwarded { get; set; }

        [JsonPropertyName("num_awarded_hardcore")]
        public int? HardcoreNumAwarded { get; set; }

        [JsonPropertyName("badge_id")]
        public string? BadgeID { get; set; }

        [JsonPropertyName("badge_path_lock")]
        public string? LockedBadgePath { get; set; }

        [JsonPropertyName("badge_path")]
        public string? BadgePath { get; set; }

        [JsonPropertyName("display_order")]
        public int? DisplayOrder { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    public class MergedRetroAchievementsMetadata
    {
        [JsonPropertyName("first_release_date")]
        public long? ReleaseDate { get; set; }

        [JsonPropertyName("achievements")]
        public List<FullRetroAchievement>? Achievements { get; set; }
    }
}