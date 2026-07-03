using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Graviton.Models.RomM
{
    //NOTE - Swap descriptions for localization strings

    public enum SaveConflictStyle
    {
        [Description("Ask")]  Ask,
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

    public class RomMSave
    {
        [JsonPropertyName("rom_id")]
        public int ROMID { get; set; }

        [JsonPropertyName("file_name")]
        public string? FileName { get; set; }

        [JsonPropertyName("slot")]
        public string? Slot { get; set; }

        [JsonPropertyName("content_hash")]
        public string? ContentHash { get; set; }

        [JsonPropertyName("updated_at")]
        public string? UpdatedAt { get; set; }

        [JsonPropertyName("file_size_bytes")]
        public int FileSize { get; set; }

    }
}
