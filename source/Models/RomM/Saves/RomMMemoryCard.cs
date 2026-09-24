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
                    return Playnite.Loc.GetString("TimeSecondsAgo", ("Count", difference.TotalSeconds.ToString("F0")));

                if (difference.TotalMinutes < 60)
                    return Playnite.Loc.GetString("TimeMinutesAgo", ("Count", difference.TotalMinutes.ToString("F0")));

                var daysAgo = (DateTime.Today - UpdatedAtParsed.Date).Days;

                if (daysAgo == 0)
                    return Playnite.Loc.GetString("TimeHoursAgo", ("Count", difference.TotalHours.ToString("F0")));

                if (daysAgo == 1)
                    return Playnite.Loc.GetString("TimeYesterday", ("Time", UpdatedAtParsed.ToLocalTime().ToString("t")));

                return Playnite.Loc.GetString("TimeDaysAgo", ("Count", daysAgo));

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