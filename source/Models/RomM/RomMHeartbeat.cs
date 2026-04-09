using System.Text.Json.Serialization;

namespace RomMLibrary.Models.RomM
{
    struct ServerInfo
    {
        [JsonPropertyName("VERSION")]
        public string Version { get; set; }
        [JsonPropertyName("SHOW_SETUP_WIZARD")]
        public bool ShowSetupWizard { get; set; }
    }

    class RomMHeartbeat
    {
        [JsonPropertyName("SYSTEM")]
        public ServerInfo SystemInfo { get; set; } 
    }
}
