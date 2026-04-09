using System.Text.Json.Serialization;

namespace RomMLibrary.Models.RomM
{
    public class RomMUser
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("username")]
        public string? Username { get; set; }
        [JsonPropertyName("email")]
        public string? Email { get; set; }
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }
        [JsonPropertyName("role")]
        public string? Role { get; set; }
        [JsonPropertyName("avatar_path")]
        public string? IconPath { get; set; }
        [JsonPropertyName("last_login")]
        public string? LastLogin { get; set; }
        [JsonPropertyName("last_active")]
        public string? LastActive { get; set; }
        [JsonPropertyName("ra_username")]
        public string? RAUsername { get; set; }
    }
}
