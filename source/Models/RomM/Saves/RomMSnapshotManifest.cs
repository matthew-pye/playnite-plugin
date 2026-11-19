using System.Text.Json.Serialization;

namespace Graviton.Models.RomM.Saves
{
    public class SnapshotManifest
    {
        [JsonPropertyName("rom_file_id")] 
        public int RomFileId { get; set; }

        [JsonPropertyName("channel_id")] 
        public string ChannelId { get; set; } = "";

        [JsonPropertyName("label")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
        public string? Label { get; set; }

        [JsonPropertyName("expected_current_id")] 
        public int? ExpectedCurrentId { get; set; }

        [JsonPropertyName("parent_snapshot_id")] 
        public int? ParentSnapshotId { get; set; }

        [JsonPropertyName("emulator")] 
        public string? Emulator { get; set; }

        [JsonPropertyName("core")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
        public string? Core { get; set; }

        [JsonPropertyName("save")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
        public ManifestSave? Save { get; set; }
    }

    public class ManifestSave
    {
        [JsonPropertyName("hash")] 
        public string Hash { get; set; } = "";

        [JsonPropertyName("shape")] 
        public string Shape { get; set; } = "SINGLE";

        [JsonPropertyName("format")] 
        public string Format { get; set; } = "native";
    }
}