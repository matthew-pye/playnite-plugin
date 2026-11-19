using System.Text.Json.Serialization;

namespace Graviton.Models.RomM.Saves
{
    public class RomMSnapshot
    {
        [JsonPropertyName("id")] 
        public int Id { get; set; }

        [JsonPropertyName("digest")] 
        public string? Digest { get; set; }

        [JsonPropertyName("kind")] 
        public string Kind { get; set; } = "channel";

        [JsonPropertyName("parent_snapshot_id")] 
        public int? ParentSnapshotId { get; set; }

        [JsonPropertyName("channel_id")] 
        public string? ChannelId { get; set; }

        [JsonPropertyName("device")] 
        public RomMDevice? Device { get; set; }

        [JsonPropertyName("emulator")] 
        public string? Emulator { get; set; }

        [JsonPropertyName("is_hardcore")] 
        public bool IsHardcore { get; set; }

        [JsonPropertyName("is_pinned")] 
        public bool IsPinned { get; set; }

        [JsonPropertyName("created_at")] 
        public DateTimeOffset CreatedAt { get; set; }

        [JsonPropertyName("held_by")] 
        public List<RomMSnapshotHeldBy> HeldBy { get; set; } = new();

        [JsonPropertyName("save")] 
        public RomMSnapshotSave? Save { get; set; }

        [JsonPropertyName("thumbnail")] 
        public RomMSnapshotScreenshot? Thumbnail { get; set; }
    }

    public class RomMSnapshotConflict
    {
        [JsonPropertyName("current")] 
        public RomMSnapshot? Current { get; set; }

        [JsonPropertyName("branch")]
        public RomMSnapshot? Branch { get; set; }

        [JsonPropertyName("reason")] 
        public string? Reason { get; set; }  
    }

    public class RomMSnapshotHeldBy
    {
        [JsonPropertyName("device")] 
        public RomMDevice? Device { get; set; }

        [JsonPropertyName("synced_at")] 
        public DateTimeOffset? SyncedAt { get; set; }
    }

    public class RomMSnapshotScreenshot
    {
        [JsonPropertyName("id")]
        public int? ID { get; set; }

        [JsonPropertyName("download_path")]
        public string? DownloadPath { get; set; }
    }
}