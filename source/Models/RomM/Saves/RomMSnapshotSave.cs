using System.Text.Json.Serialization;

namespace Graviton.Models.RomM.Saves
{
    public class RomMSnapshotSave
    {
        [JsonPropertyName("id")] 
        public int Id { get; set; }               
        
        [JsonPropertyName("file_name")]
        public string? FileName { get; set; }

        [JsonPropertyName("file_size_bytes")]
        public long FileSizeBytes { get; set; }

        [JsonPropertyName("content_hash")] 
        public string? ContentHash { get; set; }

        [JsonPropertyName("identity_hash")] 
        public string? IdentityHash { get; set; }

        [JsonPropertyName("shape")] 
        public string? Shape { get; set; }

        [JsonPropertyName("format")] 
        public string? Format { get; set; }

        [JsonPropertyName("emulator")] 
        public string? Emulator { get; set; }

        [JsonPropertyName("core")] 
        public string? Core { get; set; }

    }
    
}