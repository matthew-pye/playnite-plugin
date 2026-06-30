
using System.Text.Json.Serialization;

namespace Graviton.Models.RomM.PlaySessions
{
    public class RomMPlaySession
    {
        [JsonPropertyName("rom_id")]
        public int ROMId { get; set; }

        [JsonPropertyName("save_slot")]
        public string? SaveSlot { get; set; }

        [JsonPropertyName("start_time")]
        public string? StartTime { get; set; }

        [JsonPropertyName("end_time")]
        public string? StopTime { get; set; }

        [JsonPropertyName("duration_ms")]
        public int Duration { get; set; }
    }
}
