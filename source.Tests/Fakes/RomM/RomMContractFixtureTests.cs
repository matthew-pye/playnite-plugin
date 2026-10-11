using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Fakes.RomM;

public sealed class RomMContractFixtureTests
{
    private static readonly string[] RomFields =
    [
        "id", "igdb_id", "sgdb_id", "moby_id", "ss_id", "ra_id",
        "launchbox_id", "hasheous_id", "tgdb_id", "flashpoint_id", "hltb_id", "demozoo_id",
        "pouet_id", "csdb_id", "steam_id", "gamelist_id", "libretro_id", "platform_id",
        "platform_slug", "platform_fs_slug", "platform_custom_name", "platform_display_name", "fs_name", "fs_name_no_tags",
        "fs_name_no_ext", "fs_extension", "fs_path", "fs_size_bytes", "name", "name_sort_key",
        "slug", "summary", "alternative_names", "youtube_video_id", "metadatum", "igdb_metadata",
        "moby_metadata", "ss_metadata", "launchbox_metadata", "hasheous_metadata", "flashpoint_metadata", "hltb_metadata",
        "demozoo_metadata", "pouet_metadata", "csdb_metadata", "steam_metadata", "gamelist_metadata", "manual_metadata",
        "path_cover_small", "path_cover_large", "url_cover", "has_manual", "has_soundtrack", "path_manual",
        "url_manual", "path_video", "is_identifying", "is_unidentified", "is_identified", "revision",
        "regions", "languages", "tags", "crc_hash", "md5_hash", "sha1_hash",
        "ra_hash", "title_id", "save_target", "save_target_layout", "has_simple_single_file", "has_nested_single_file",
        "has_multiple_files", "is_easyrpg_game", "full_path", "created_at", "updated_at", "missing_from_fs",
        "is_physical", "has_file_on_disk", "upc", "has_notes", "rom_user", "merged_screenshots",
        "merged_ra_metadata", "files", "sibling_roms"
    ];

    private static readonly string[] MetadataFields =
    [
        "rom_id", "genres", "franchises", "collections", "companies", "publishers",
        "developers", "game_modes", "age_ratings", "player_count", "first_release_date", "average_rating"
    ];

    private static readonly string[] RomUserFields =
    [
        "id", "user_id", "rom_id", "created_at", "updated_at", "last_played",
        "is_main_sibling", "backlogged", "now_playing", "hidden", "rating", "difficulty",
        "completion", "status", "pinned_media"
    ];

    private static readonly string[] RomFileFields =
    [
        "id", "rom_id", "file_name", "file_path", "file_size_bytes", "full_path",
        "is_top_level", "created_at", "updated_at", "last_modified", "crc_hash", "md5_hash",
        "sha1_hash", "ra_hash", "chd_sha1_hash", "archive_members", "category", "track_meta",
        "doc_meta"
    ];

    private static readonly string[] DetailedFields =
    [
        "user_saves", "user_states", "all_user_saves", "all_user_states", "user_screenshots", "all_user_screenshots",
        "user_collections", "all_user_notes", "download_formats"
    ];

    private static readonly string[] UserFields =
    [
        "id", "username", "email", "enabled", "role", "permission_group_id",
        "oauth_scopes", "avatar_path", "last_login", "last_active", "ra_username", "ra_progression",
        "ui_settings", "current_device_id", "created_at", "updated_at"
    ];

    private static readonly string[] SaveFields =
    [
        "id", "rom_id", "user_id", "file_name", "file_name_no_tags", "file_name_no_ext",
        "file_extension", "file_path", "file_size_bytes", "full_path", "download_path", "missing_from_fs",
        "created_at", "updated_at", "is_favorite", "labels", "emulator", "slot",
        "content_hash", "is_public", "screenshot", "origin_device_id", "device_syncs"
    ];

    private static readonly string[] PageFields =
    [
        "items", "total", "limit", "offset", "char_index", "rom_id_index",
        "filter_values"
    ];

    [Fact]
    public void RomFixture_ContainsRomMetadataAndFileFields()
    {
        using var document = JsonDocument.Parse(RomMRomFixtures.CreateRom().ToJsonString());
        var rom = document.RootElement;

        AssertFields(rom, RomFields);
        AssertFields(rom.GetProperty("metadatum"), MetadataFields);
        AssertFields(rom.GetProperty("rom_user"), RomUserFields);
        AssertFields(Assert.Single(rom.GetProperty("files").EnumerateArray()), RomFileFields);
        Assert.Equal(rom.GetProperty("id").GetInt32(), rom.GetProperty("metadatum").GetProperty("rom_id").GetInt32());
    }

    [Fact]
    public void DetailedRomFixture_ContainsAdditionalDetailedFields()
    {
        using var document = JsonDocument.Parse(RomMRomFixtures.CreateDetailedRom().ToJsonString());
        AssertFields(document.RootElement, RomFields);
        AssertFields(document.RootElement, DetailedFields);
    }

    [Fact]
    public void RomPageFixture_ContainsPaginationAndFilterFields()
    {
        var rom = RomMRomFixtures.CreateRom(id: 42);
        using var document = JsonDocument.Parse(RomMRomFixtures.CreatePage([rom], 1, 0));
        var page = document.RootElement;

        AssertFields(page, PageFields);
        Assert.Equal(42, Assert.Single(page.GetProperty("rom_id_index").EnumerateArray()).GetInt32());
        AssertFields(Assert.Single(page.GetProperty("items").EnumerateArray()), RomFields);
        Assert.Equal(JsonValueKind.Object, page.GetProperty("filter_values").ValueKind);
    }

    [Fact]
    public void UserFixture_ContainsUserSchemaFieldsAndValidRole()
    {
        using var document = JsonDocument.Parse(RomMRomFixtures.CreateUserResponse());
        var user = document.RootElement;

        AssertFields(user, UserFields);
        Assert.Equal("user", user.GetProperty("role").GetString());
        Assert.Equal(JsonValueKind.Null, user.GetProperty("last_login").ValueKind);
    }

    [Fact]
    public void SaveFixture_ContainsSaveSchemaFieldsAndVersionedDownloadPath()
    {
        var save = RomMSaveFixtures.CreateSave(987, 42, null, "backup save.zip");
        using var document = JsonDocument.Parse(RomMSaveFixtures.CreateResponse(save));
        var item = Assert.Single(document.RootElement.EnumerateArray());

        AssertFields(item, SaveFields);
        var path = item.GetProperty("download_path").GetString();
        Assert.StartsWith("/api/saves/987/content?timestamp=", path);
        Assert.Contains("&name=backup%20save.zip", path);
    }

    private static void AssertFields(JsonElement element, IEnumerable<string> fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            Assert.True(element.TryGetProperty(fieldName, out _), $"Missing RomM 5.4.0 field: {fieldName}");
        }
    }
}
