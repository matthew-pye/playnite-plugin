using System.Text.Json.Serialization;

namespace RomMLibrary.Models.RomM.Platform
{
    public class RomMPlatformFirmware
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("file_name")]
        public string? FileName { get; set; }

        [JsonPropertyName("file_name_no_tags")]
        public string? FileNameNoTags { get; set; }

        [JsonPropertyName("file_name_no_ext")]
        public string? FileNameNoExt { get; set; }

        [JsonPropertyName("file_extension")]
        public string? FileExtension { get; set; }

        [JsonPropertyName("file_path")]
        public string? FilePath { get; set; }

        [JsonPropertyName("file_size_bytes")]
        public ulong FileSizeBytes { get; set; }

        [JsonPropertyName("full_path")]
        public string? FullPath { get; set; }

        [JsonPropertyName("is_verified")]
        public bool IsVerified { get; set; }

        [JsonPropertyName("crc_hash")]
        public string? CRCHash { get; set; }

        [JsonPropertyName("md5_hash")]
        public string? MD5Hash { get; set; }

        [JsonPropertyName("sha1_hash")]
        public string? SHA1Hash { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}
