using System.Text.Json.Serialization;

namespace Graviton.Models.RomM.Saves
{
    public class RomMMemoryCard
    {
        [JsonPropertyName("id")]
        public int ID { get; set; }

        [JsonPropertyName("user_id")]
        public int UserID { get; set; }

        [JsonPropertyName("platform_id")]
        public int PlatformID { get; set; }

        [JsonPropertyName("emulator")]
        public string? Emulator { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("slot")]
        public int Slot { get; set; }

        [JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public string? UpdatedAt { get; set; }

        #region UI Only
        [JsonIgnore] public List<RomMSave> HistoricSaves = new();
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
        #endregion
    }
    public class RomMMemoryCardVersion
    {
        [JsonPropertyName("id")]
        public int ID { get; set; }

        [JsonPropertyName("memory_card_id")]
        public int MemoryCardID { get; set; }

        [JsonPropertyName("file_name")]
        public string? FileName { get; set; }

        [JsonPropertyName("file_size_bytes")]
        public long Filesize { get; set; }

        [JsonPropertyName("content_hash")]
        public string? ContentHash { get; set; }

        [JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public string? UpdatedAt { get; set; }

    }
}