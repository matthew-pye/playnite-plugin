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

    public class RomMSave
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

        #region UI Only
        [JsonIgnore] public List<RomMSave> HistoricSaves = new();
        [JsonIgnore] public string? ROMName { get; set; }
        [JsonIgnore] public DateTime UpdatedAtParsed { get; set; }

        [JsonIgnore]
        public string UpdatedAtUI
        {
            get
            {
                UpdatedAtParsed = DateTime.Parse(UpdatedAt!);
                var difference = DateTime.Now - UpdatedAtParsed;

                if (difference.TotalSeconds < 60)
                    return $"{difference.TotalSeconds:F0}s ago";

                if (difference.TotalMinutes < 60)
                    return $"{difference.TotalMinutes:F0}m ago";

                var daysAgo = (DateTime.Today - UpdatedAtParsed.Date).Days;

                if (daysAgo == 0)
                    return $"{difference.TotalHours:F0}h ago";

                if (daysAgo == 1)
                    return $"Yesterday, {UpdatedAtParsed.ToLocalTime():t}";

                return $"{daysAgo}d ago";

            }
        }

        [JsonIgnore]
        public string FileSizeUI
        {
            get
            {
                if (FileSize <= 0)
                    return "Unknown";

                if (FileSize < 1000)
                {
                    return $"{FileSize} Bytes";
                }
                else if (FileSize < 1000000)
                {
                    return $"{((float)FileSize / 1000).ToString("F1")}KB";
                }
                else
                {
                    return $"{((float)FileSize! / 1000000).ToString("F1")}MB";
                }
            }
        }
        #endregion
    }
}
