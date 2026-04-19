using RomMLibrary.Models.RomM.Metadata;

using System.Text.Json.Serialization;

namespace RomMLibrary.Models.RomM.Rom
{
    public class RomMFile
    {
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("file_name")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("file_size_bytes")]
        public long? FileSize { get; set; }

        [JsonPropertyName("full_path")]
        public string FullPath { get; set; } = string.Empty;
    }

    public class RomMSibling
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("fs_name_no_tags")]
        public string? FileNameNoTags { get; set; }

        [JsonPropertyName("fs_name_no_ext")]
        public string? FileNameNoExt { get; set; }
    }

    public class RomMRom
    {
        // IDs
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("igdb_id")]
        public int? IgdbId { get; set; }

        [JsonPropertyName("sgdb_id")]
        public object? SgdbId { get; set; }

        [JsonPropertyName("moby_id")]
        public object? MobyId { get; set; }

        [JsonPropertyName("ss_id")]
        public int? SSId { get; set; }

        [JsonPropertyName("ra_id")]
        public int? RAId { get; set; }

        [JsonPropertyName("hasheous_id")]
        public int? HasheousId { get; set; }

        [JsonPropertyName("hltb_id")]
        public int? HLTBId { get; set; }

        // Platform
        [JsonPropertyName("platform_id")]
        public int PlatformId { get; set; }

        [JsonPropertyName("platform_slug")]
        public string? PlatformSlug { get; set; }

        [JsonPropertyName("platform_display_name")]
        public string? PlatformName { get; set; }

        // ROM Data
        [JsonPropertyName("fs_name")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("fs_name_no_tags")]
        public string? FileNameNoTags { get; set; }

        [JsonPropertyName("fs_name_no_ext")]
        public string? FileNameNoExt { get; set; }

        [JsonPropertyName("fs_extension")]
        public string? FileExtension { get; set; }

        [JsonPropertyName("fs_path")]
        public string? FilePath { get; set; }

        [JsonPropertyName("fs_size_bytes")]
        public ulong FileSizeBytes { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("slug")]
        public string? Slug { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        // Metadata
        [JsonPropertyName("metadatum")]
        public Metadatum? Metadatum { get; set; }

        [JsonPropertyName("igdb_metadata")]
        public IGDBMetadata? IgdbMetadata { get; set; }

        [JsonPropertyName("ss_metadata")]
        public SSMetadata? SSMetadata { get; set; }

        [JsonPropertyName("hltb_metadata")]
        public HLTBMetadata? HLTBMetadata { get; set; }

        [JsonPropertyName("regions")]
        public List<string>? Regions { get; set; }

        [JsonPropertyName("languages")]
        public List<string>? Languages { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        [JsonPropertyName("path_cover_small")]
        public string? PathCoverS { get; set; }

        [JsonPropertyName("path_cover_large")]
        public string? PathCoverL { get; set; }

        [JsonPropertyName("has_cover")]
        public bool HasCover { get; set; }

        [JsonPropertyName("url_cover")]
        public string? UrlCover { get; set; }

        [JsonPropertyName("has_manual")]
        public bool HasManual { get; set; }

        [JsonPropertyName("path_manual")]
        public string? ManualPath { get; set; }

        // ROM Info
        [JsonPropertyName("revision")]
        public string? Revision { get; set; }

        [JsonPropertyName("has_simple_single_file")]
        public bool HasSimpleSingleFile { get; set; }

        [JsonPropertyName("has_nested_single_file")]
        public bool HasNestedSingleFile { get; set; }
        
        [JsonPropertyName("has_multiple_files")]
        public bool HasMultipleFiles { get; set; }

        [JsonPropertyName("files")]
        public List<RomMFile> Files { get; set; } = new List<RomMFile>();

        [JsonPropertyName("siblings")]
        public List<RomMSibling>? Siblings { get; set; }

        [JsonPropertyName("crc_hash")]
        public string? CRC { get; set; }

        [JsonPropertyName("md5_hash")]
        public string? MD5 { get; set; }

        [JsonPropertyName("sha1_hash")]
        public string? SHA1 { get; set; }

        [JsonPropertyName("full_path")]
        public string? FullPath { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }

        // User Data
        [JsonPropertyName("rom_user")]
        public RomMRomUser? RomUser { get; set; }

        public bool Processed { get; set; } = false;
    }
}
