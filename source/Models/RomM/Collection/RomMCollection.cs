using System.Text.Json.Serialization;

namespace Graviton.Models.RomM.Collection
{
    public class RomMCollection
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("rom_ids")]
        public List<int> RomIDs { get; set; } = new List<int>();

        [JsonPropertyName("is_favorite")]
        public bool IsFavorite { get; set; }

        [JsonIgnore]
        public bool HasBeenUpdated { get; set; } = false;
    }
}