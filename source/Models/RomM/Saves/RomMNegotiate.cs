using System.Text.Json.Serialization;

namespace Graviton.Models.RomM
{
    public class RomMNegotiate
    {
        [JsonPropertyName("device_id")]
        public string? DeviceID { get; set; }

        [JsonPropertyName("saves")]
        public List<RomMSave> Saves { get; set; } = new List<RomMSave>();
    }

    public class RomMNegotiateResponse
    {
        [JsonPropertyName("session_id")]
        public string? SessionID { get; set; }

        [JsonPropertyName("operations")]
        public List<RomMNegotiateOperations> Operations { get; set; } = new List<RomMNegotiateOperations>();
    }

    public class RomMNegotiateOperations
    {
        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("rom_id")]
        public int ROMID { get; set; }

        [JsonPropertyName("save_id")]
        public int SaveID { get; set; }

        [JsonPropertyName("file_name")]
        public string? FileName { get; set; }

        [JsonPropertyName("slot")]
        public string? Slot { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("server_updated_at")]
        public string? UpdatedAt { get; set; }

        [JsonPropertyName("server_content_hash")]
        public string? ContentHash { get; set; }

    }


}
