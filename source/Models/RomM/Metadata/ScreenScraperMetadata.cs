using System.Text.Json.Serialization;

namespace RomMLibrary.Models.RomM.Metadata
{
    public class SSMetadata
    {
        [JsonPropertyName("box2d_url")]
        public string? Box2D { get; set; }
        [JsonPropertyName("box2d_side_url")]
        public string? Box2DSide { get; set; }
        [JsonPropertyName("box2d_back_url")]
        public string? Box2DBack { get; set; }
        [JsonPropertyName("box3d_url")]
        public string? Box3D { get; set; }
        [JsonPropertyName("fanart_url")]
        public string? Fanart { get; set; }
        [JsonPropertyName("fullbox_url")]
        public string? Fullbox { get; set; }
        [JsonPropertyName("manual_url")]
        public string? Manual { get; set; }
        [JsonPropertyName("physical_url")]
        public string? Physical { get; set; }
        [JsonPropertyName("screenshot_url")]
        public string? Screenshot { get; set; }
        [JsonPropertyName("ss_score")]
        public float Score { get; set; }
        [JsonPropertyName("genres")]
        public List<string> Genres { get; set; } = new List<string>();

        [JsonPropertyName("franchises")]
        public List<string> Franchises { get; set; } = new List<string>();
    }
}
