using System.Text.Json.Serialization;

namespace Graviton.Models.RomM.Install
{
    internal static class RomMInstallStatus
    {
        public static readonly string Done = "done";
        public static readonly string AlreadyInstalled = "already_installed";
        public static readonly string Failed = "failed";
    }

    internal class RomMRemoteInstallEvent
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("rom_id")]
        public long RomId { get; set; }

        [JsonPropertyName("file_ids")]
        public List<string> fileIDs { get; set; } = [];

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }
}
