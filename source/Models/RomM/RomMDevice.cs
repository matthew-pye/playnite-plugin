using System.Text.Json.Serialization;

namespace RomMLibrary.Models.RomM
{
    public class RomMRegisterDevice
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("platform")]
        public string? Platform { get; set; }

        [JsonPropertyName("client")]
        public string? Client { get; set; }

        [JsonPropertyName("client_version")]
        public string? ClientVersion { get; set; }

        [JsonPropertyName("ip_address")]
        public string? IPAddress { get; set; }

        [JsonPropertyName("mac_address")]
        public string? MACAddress { get; set; }

        [JsonPropertyName("hostname")]
        public string? HostName { get; set; }

        [JsonPropertyName("allow_existing")]
        public bool? AllowExisting { get; set; }

        [JsonPropertyName("allow_duplicate")]
        public bool? AllowDuplicate { get; set; }

        [JsonPropertyName("reset_syncs")]
        public bool? ResetSyncs { get; set; }
    }

    public class RomMRegisterDeviceResponse
    {
        [JsonPropertyName("device_id")]
        public string? DeviceID { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }
    }

    public class RomMDevice
    {
        [JsonPropertyName("id")]
        public string? ID { get; set; }

        [JsonPropertyName("user_id")]
        public int UserID { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("platform")]
        public string? Platform { get; set; }

        [JsonPropertyName("client")]
        public string? Client { get; set; }

        [JsonPropertyName("client_version")]
        public string? ClientVersion { get; set; }

        [JsonPropertyName("ip_address")]
        public string? IPAddress { get; set; }

        [JsonPropertyName("mac_address")]
        public string? MACAddress { get; set; }

        [JsonPropertyName("hostname")]
        public string? Hostname { get; set; }

        [JsonPropertyName("sync_mode")]
        public string? SyncMode { get; set; }

        [JsonPropertyName("sync_enabled")]
        public bool SyncEnabled { get; set; }

        [JsonPropertyName("last_seen")]
        public DateTime LastSeen { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}
