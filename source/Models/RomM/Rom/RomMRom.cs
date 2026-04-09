using System.Text.Json.Serialization;

namespace RomMLibrary.Models.RomM.Rom
{
    public class metadatum
    {
        [JsonPropertyName("rom_id")]
        public int Id { get; set; }

        [JsonPropertyName("genres")]
        public List<string> Genres { get; set; } = new List<string>();

        [JsonPropertyName("franchises")]
        public List<string> Franchises { get; set; } = new List<string>();

        [JsonPropertyName("collections")]
        public List<string> Collections { get; set; } = new List<string>();

        [JsonPropertyName("companies")]
        public List<string> Companies { get; set; } = new List<string>();

        [JsonPropertyName("game_modes")]
        public List<string> Gamemodes { get; set; } = new List<string>();

        [JsonPropertyName("age_ratings")]
        public List<string> Age_Ratings { get; set; } = new List<string>();

        [JsonPropertyName("first_release_date")]
        public long? Release_Date { get; set; }

        [JsonPropertyName("average_rating")]
        public float? Average_Rating { get; set; }

    }

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

        [JsonPropertyName("platform_id")]
        public int PlatformId { get; set; }

        [JsonPropertyName("platform_slug")]
        public string? PlatformSlug { get; set; }

        [JsonPropertyName("platform_name")]
        public string? PlatformName { get; set; }

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

        [JsonPropertyName("first_release_date")]
        public long? FirstReleaseDate { get; set; }

        [JsonPropertyName("metadatum")]
        public metadatum? Metadatum { get; set; }

        [JsonPropertyName("alternative_names")]
        public List<string>? AlternativeNames { get; set; }

        [JsonPropertyName("genres")]
        public List<string>? Genres { get; set; }

        [JsonPropertyName("franchises")]
        public List<string>? Franchises { get; set; }

        [JsonPropertyName("collections")]
        public List<string>? Collections { get; set; }

        [JsonPropertyName("companies")]
        public List<string>? Companies { get; set; }

        [JsonPropertyName("game_modes")]
        public List<string>? GameModes { get; set; }

        [JsonPropertyName("igdb_metadata")]
        public RomMIgdbMetadata? IgdbMetadata { get; set; }

        [JsonPropertyName("path_cover_small")]
        public string? PathCoverS { get; set; }

        [JsonPropertyName("path_cover_large")]
        public string? PathCoverL { get; set; }

        [JsonPropertyName("has_cover")]
        public bool HasCover { get; set; }

        [JsonPropertyName("url_cover")]
        public string? UrlCover { get; set; }

        [JsonPropertyName("revision")]
        public string? Revision { get; set; }

        [JsonPropertyName("regions")]
        public List<string>? Regions { get; set; }

        [JsonPropertyName("languages")]
        public List<string>? Languages { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

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

        [JsonPropertyName("sha1_hash")]
        public string? SHA1 { get; set; }

        [JsonPropertyName("has_manual")]
        public bool HasManual {  get; set; }

        [JsonPropertyName("path_manual")]
        public string? ManualPath { get; set; }

        [JsonPropertyName("full_path")]
        public string? FullPath { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [JsonPropertyName("rom_user")]
        public RomMRomUser? RomUser { get; set; }

        [JsonPropertyName("sort_comparator")]
        public string? SortComparator { get; set; }

        public bool Processed { get; set; } = false;
}
}
