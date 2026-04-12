using System.Text.Json.Serialization;

namespace RomMLibrary.Models.RomM
{
    public class RomMUser
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;
        [JsonPropertyName("avatar_path")]
        public string IconPath { get; set; } = string.Empty;
        [JsonPropertyName("last_login")]
        public string LastLogin { get; set; } = string.Empty;
        [JsonPropertyName("last_active")]
        public string LastActive { get; set; } = string.Empty;
        [JsonPropertyName("ra_username")]
        public string RAUsername { get; set; } = string.Empty;
    }
}
