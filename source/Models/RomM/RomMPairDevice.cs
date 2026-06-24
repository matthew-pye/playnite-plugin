using System.Text.Json.Serialization;

namespace Graviton.Models.RomM
{
    public class RomMPairDevice
    {
        [JsonPropertyName("device_code")]
        public string? DeviceCode { get; set; }

        [JsonPropertyName("user_code")]
        public string? UserCode { get; set; }

        [JsonPropertyName("verification_path")]
        public string? VeificationPath { get; set; }

        [JsonPropertyName("verification_path_complete")]
        public string? VeificationPathComplete { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("interval")]
        public int? Interval { get; set; }
    }

    public class RomMPairDeviceResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("device_id")]
        public string? DeviceID { get; set; }

    }
}
