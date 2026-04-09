using System.Text.Json.Serialization;

namespace RomMLibrary.Models.RomM.Rom
{
    public class RomMRomUser
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("user_id")]
        public int UserId { get; set; }

        [JsonPropertyName("is_main_sibling")]
        public bool IsMainSibling { get; set; }

        [JsonPropertyName("last_played")]
        public DateTime? LastPlayed { get; set; }

        [JsonPropertyName("backlogged")]
        public bool Backlogged { get; set; }

        [JsonPropertyName("now_playing")]
        public bool NowPlaying { get; set; }

        [JsonPropertyName("rating")]
        public int Rating { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        public static readonly Dictionary<string, string> CompletionStatusMap = new Dictionary<string, string>
        {
            { "never_playing", "Abandoned" },
            { "retired", "Played" },
            { "incomplete", "On Hold" },
            { "finished", "Beaten" },
            { "completed_100", "Completed" },
            { "backlogged", "Plan to Play" },
            { "now_playing", "Playing" },
            { "not_played", "Not Played" }
        };
    }
}