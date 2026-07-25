using System.Text.Json.Serialization;

namespace Graviton.Models.RomM.Saves
{
    public enum SaveSyncStatus
    {
        upload,
        download,
        conflict,
        no_op
    }

    internal class RomMSave
    {
        [JsonPropertyName("id")]
        public int ID { get; set; }

        [JsonPropertyName("rom_id")]
        public int ROMID { get; set; }

        [JsonPropertyName("user_id")]
        public int UserID { get; set; }

        [JsonPropertyName("file_name")]
        public string? FileName { get; set; }

        [JsonPropertyName("file_size_bytes")]
        public long? FileSize { get; set; }

        [JsonPropertyName("full_path")]
        public string? FullPath { get; set; }

        [JsonPropertyName("download_path")]
        public string? DownloadPath { get; set; }

        [JsonPropertyName("missing_from_fs")]
        public bool MissingFromFileSystem { get; set; }

        [JsonPropertyName("slot")]
        public string? Slot { get; set; }

        [JsonPropertyName("content_hash")]
        public string? ContentHash { get; set; }

        [JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public string? UpdatedAt { get; set; }

        [JsonIgnore] public List<RomMSave> HistoricSaves = new();
    }
}
