using System.Text.Json.Serialization;

namespace RomMLibrary.Models.RomM.Metadata
{
    public class HLTBMetadata
    {
        [JsonPropertyName("main_story")]
        public uint MainStory { get; set; }

        [JsonPropertyName("main_plus_extra")]
        public uint MainStoryExtra { get; set; }

        [JsonPropertyName("completionist")]
        public uint Completionist { get; set; }

        [JsonPropertyName("all_styles")]
        public uint AllStyles { get; set; }
    }
}
