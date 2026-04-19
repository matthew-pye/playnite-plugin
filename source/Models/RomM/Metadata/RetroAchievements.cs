using System.Text.Json.Serialization;

namespace RomMLibrary.Models.RomM.Metadata
{
    public class RetroAchievement
    {
        [JsonPropertyName("id")]
        public int? ID { get; set; }

        [JsonPropertyName("date")]
        public DateTime? Date { get; set; }

        [JsonPropertyName("date_hardcore")]
        public DateTime? HardcoreDate { get; set; }
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

    public class RetroAchievmentProgression
    {
        [JsonPropertyName("total")]
        public int? RAGamesCount { get; set; }

        [JsonPropertyName("results")]
        public List<RetroAchievementGame>? RAGames { get; set; }
    }
}