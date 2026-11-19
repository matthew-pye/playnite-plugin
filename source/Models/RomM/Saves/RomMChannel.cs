using System.Text.Json.Serialization;

namespace Graviton.Models.RomM.Saves
{
    public class RomMChannel
    {
        [JsonPropertyName("id")] 
        public string Id { get; set; } = "";

        [JsonPropertyName("label")] 
        public string Label { get; set; } = "";

        [JsonPropertyName("rom_id")]
        public int RomId { get; set; }

        [JsonPropertyName("rom_file_id")] 
        public int? RomFileId { get; set; }

        [JsonPropertyName("is_public")] 
        public bool IsPublic { get; set; }

        [JsonPropertyName("is_hardcore")] 
        public bool IsHardcore { get; set; }

        [JsonPropertyName("is_own")] 
        public bool IsOwn { get; set; }

        [JsonPropertyName("current_snapshot_id")] 
        public int? CurrentSnapshotId { get; set; }

        [JsonPropertyName("current")] 
        public RomMSnapshot? Current { get; set; }

        [JsonPropertyName("snapshot_count")] 
        public int SnapshotCount { get; set; }

        [JsonPropertyName("updated_at")] 
        public DateTimeOffset UpdatedAt { get; set; }


    }
}