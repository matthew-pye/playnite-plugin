using System.Text.Json.Serialization;

namespace RomMLibrary.Models.RomM.Rom
{
    public class RomMRomPlatform
    {
        [JsonPropertyName("igdb_id")]
        public int? IgdbId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}