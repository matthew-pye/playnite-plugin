using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Graviton.Models.RomM.Saves
{
    public enum SaveConflictStyle
    {
        [Description("Ask")] Ask,
        [Description("Prefer Newer")] PreferNewer,
        [Description("Prefer Remote")] PreferRemote,
        [Description("Prefer Local")] PreferLocal
    }

    public enum SaveLayoutStyle
    {
        [Description("Single File")] SingleFile,
        [Description("Fixed Set")] FixedSet,
        [Description("Folder")] WholeFolder,
        [Description("Manual Per-Game")] ManualPerGame
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

    }

    public class LocalSave
    {
        public List<string> SourceFilePaths { get; set; } = new();

        public int SaveID { get; set; }
        public string? Slot { get; set; }
        public bool Enabled { get; set; } = false;
    }
}
