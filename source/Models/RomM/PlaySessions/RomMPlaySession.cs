
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Graviton.Models.RomM.PlaySessions
{
    public enum ImportPlaySessions
    {
        [Description("None")] None,
        [Description("Only this device")] OnlyThisDevice,
        [Description("All")] All
    }

    public class RomMPlaySession
    {
        [JsonPropertyName("id")]
        public int ID { get; set; }

        [JsonPropertyName("rom_id")]
        public int? ROMID { get; set; }

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
