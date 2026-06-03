using System.Text.Json.Serialization;

namespace Graviton.Models.RomM
{
    public class RomMUserNotes
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Note { get; set; } = string.Empty;

        [JsonPropertyName("is_public")]
        public bool IsPublic { get; set; }
    }
}
