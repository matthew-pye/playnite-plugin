using System.Text.Json.Serialization;

namespace RomMLibrary.Models.RomM.Collection
{
    public class RomMCollection
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("rom_ids")]
        public List<int>? RomIDs { get; set; }

        [JsonPropertyName("is_favorite")]
        public bool IsFavorite { get; set; }
    }
}