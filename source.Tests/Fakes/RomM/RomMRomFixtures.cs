using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Graviton.Tests.Fakes.RomM;

internal static class RomMRomFixtures
{
    private const string RomTemplate = """
        {
          "id": 42,
          "igdb_id": null,
          "sgdb_id": null,
          "moby_id": null,
          "ss_id": null,
          "ra_id": null,
          "launchbox_id": null,
          "hasheous_id": null,
          "tgdb_id": null,
          "flashpoint_id": null,
          "hltb_id": null,
          "demozoo_id": null,
          "pouet_id": null,
          "csdb_id": null,
          "steam_id": null,
          "gamelist_id": null,
          "libretro_id": null,
          "platform_id": 3,
          "platform_slug": "n64",
          "platform_fs_slug": "n64",
          "platform_custom_name": null,
          "platform_display_name": "Nintendo 64",
          "fs_name": "example.z64",
          "fs_name_no_tags": "example.z64",
          "fs_name_no_ext": "example",
          "fs_extension": ".z64",
          "fs_path": "n64",
          "fs_size_bytes": 4096,
          "name": "Example Game",
          "name_sort_key": "example game",
          "slug": "example-game",
          "summary": "An example summary",
          "alternative_names": [],
          "youtube_video_id": null,
          "metadatum": {
            "rom_id": 42,
            "genres": [
              "Role-playing"
            ],
            "franchises": [],
            "collections": [
              "RPGs"
            ],
            "companies": [],
            "publishers": [],
            "developers": [],
            "game_modes": [],
            "age_ratings": [],
            "player_count": "1",
            "first_release_date": null,
            "average_rating": null
          },
          "igdb_metadata": null,
          "moby_metadata": null,
          "ss_metadata": null,
          "launchbox_metadata": null,
          "hasheous_metadata": null,
          "flashpoint_metadata": null,
          "hltb_metadata": null,
          "demozoo_metadata": null,
          "pouet_metadata": null,
          "csdb_metadata": null,
          "steam_metadata": null,
          "gamelist_metadata": null,
          "manual_metadata": null,
          "path_cover_small": null,
          "path_cover_large": null,
          "url_cover": null,
          "has_manual": false,
          "has_soundtrack": false,
          "path_manual": null,
          "url_manual": null,
          "path_video": null,
          "is_identifying": false,
          "is_unidentified": false,
          "is_identified": true,
          "revision": null,
          "regions": [],
          "languages": [],
          "tags": [],
          "crc_hash": null,
          "md5_hash": null,
          "sha1_hash": null,
          "ra_hash": null,
          "title_id": null,
          "save_target": null,
          "save_target_layout": null,
          "has_simple_single_file": true,
          "has_nested_single_file": false,
          "has_multiple_files": false,
          "is_easyrpg_game": false,
          "full_path": "n64/example.z64",
          "created_at": "2026-10-10T10:00:00+00:00",
          "updated_at": "2026-10-10T10:00:00+00:00",
          "missing_from_fs": false,
          "is_physical": false,
          "has_file_on_disk": true,
          "upc": null,
          "has_notes": false,
          "rom_user": {
            "id": 7,
            "user_id": 1,
            "rom_id": 42,
            "created_at": "2026-10-10T10:00:00+00:00",
            "updated_at": "2026-10-10T10:00:00+00:00",
            "last_played": null,
            "is_main_sibling": false,
            "backlogged": false,
            "now_playing": false,
            "hidden": true,
            "rating": 8,
            "difficulty": 0,
            "completion": 0,
            "status": null,
            "pinned_media": null
          },
          "merged_screenshots": [],
          "merged_ra_metadata": null,
          "files": [
            {
              "id": 1000,
              "rom_id": 42,
              "file_name": "example.z64",
              "file_path": "n64",
              "file_size_bytes": 4096,
              "full_path": "n64/example.z64",
              "is_top_level": true,
              "created_at": "2026-10-10T10:00:00+00:00",
              "updated_at": "2026-10-10T10:00:00+00:00",
              "last_modified": null,
              "crc_hash": null,
              "md5_hash": null,
              "sha1_hash": null,
              "ra_hash": null,
              "chd_sha1_hash": null,
              "archive_members": [],
              "category": "game",
              "track_meta": null,
              "doc_meta": null
            }
          ],
          "sibling_roms": []
        }
        """;

    private const string DetailedTemplate = """
        {
          "user_saves": [],
          "user_states": [],
          "all_user_saves": [],
          "all_user_states": [],
          "user_screenshots": [],
          "all_user_screenshots": [],
          "user_collections": [
            {
              "id": 3,
              "name": "Favorites",
              "is_smart": false
            }
          ],
          "all_user_notes": [],
          "download_formats": []
        }
        """;

    public static JsonObject CreateRom(
        int id = 42,
        string name = "Example Game",
        string? fileName = null,
        ulong fileSizeBytes = 4096,
        int platformId = 3)
    {
        var rom = JsonNode.Parse(RomTemplate)!.AsObject();
        var resolvedFileName = fileName ?? $"Game {id}.zip";
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(resolvedFileName);
        var fileExtension = Path.GetExtension(resolvedFileName);

        rom["id"] = id;
        rom["name"] = name;
        rom["name_sort_key"] = name.ToLowerInvariant();
        rom["slug"] = name.ToLowerInvariant().Replace(" ", "-");
        rom["fs_name"] = resolvedFileName;
        rom["fs_name_no_tags"] = resolvedFileName;
        rom["fs_name_no_ext"] = fileNameWithoutExtension;
        rom["fs_extension"] = fileExtension;
        rom["fs_size_bytes"] = fileSizeBytes;
        var platformSlug = platformId == 3 ? "n64" : $"platform-{platformId}";
        rom["platform_id"] = platformId;
        rom["platform_slug"] = platformSlug;
        rom["platform_fs_slug"] = platformSlug;
        rom["platform_display_name"] = platformId == 3 ? "Nintendo 64" : $"Platform {platformId}";
        rom["fs_path"] = platformSlug;
        rom["full_path"] = $"{platformSlug}/{resolvedFileName}";
        rom["metadatum"]!["rom_id"] = id;
        rom["rom_user"]!["rom_id"] = id;
        rom["rom_user"]!["id"] = id + 1000;
        rom["files"]![0]!["id"] = id + 10000;
        rom["files"]![0]!["rom_id"] = id;
        rom["files"]![0]!["file_name"] = resolvedFileName;
        rom["files"]![0]!["file_size_bytes"] = fileSizeBytes;
        rom["files"]![0]!["file_path"] = platformSlug;
        rom["files"]![0]!["full_path"] = $"{platformSlug}/{resolvedFileName}";

        return rom;
    }

    public static JsonObject CreateDetailedRom(int id = 42)
    {
        var rom = CreateRom(id, "Example Game", "example.z64", 4096, 3);
        var detail = JsonNode.Parse(DetailedTemplate)!.AsObject();

        foreach (var property in detail)
        {
            rom[property.Key] = property.Value?.DeepClone();
        }

        return rom;
    }

    public static string CreatePage(IEnumerable<JsonObject> roms, int total, int offset, int limit = 100)
    {
        var items = roms.ToList();
        var itemArray = new JsonArray();
        var idIndex = new JsonArray();

        foreach (var rom in items)
        {
            itemArray.Add(rom.DeepClone());
            idIndex.Add(rom["id"]!.GetValue<int>());
        }

        var page = new JsonObject
        {
            ["items"] = itemArray,
            ["total"] = total,
            ["limit"] = limit,
            ["offset"] = offset,
            ["char_index"] = new JsonObject(),
            ["rom_id_index"] = idIndex,
            ["filter_values"] = CreateEmptyFilterValues()
        };

        return page.ToJsonString();
    }

    public static string CreateUserResponse()
    {
        var user = new
        {
            id = 1,
            username = "tester",
            email = "tester@example.test",
            enabled = true,
            role = "user",
            permission_group_id = (int?)null,
            oauth_scopes = Array.Empty<string>(),
            avatar_path = "",
            last_login = (string?)null,
            last_active = (string?)null,
            ra_username = (string?)null,
            ra_progression = (object?)null,
            ui_settings = (object?)null,
            current_device_id = (string?)null,
            created_at = "2026-10-10T10:00:00+00:00",
            updated_at = "2026-10-10T10:00:00+00:00"
        };

        return JsonSerializer.Serialize(user);
    }

    private static JsonObject CreateEmptyFilterValues()
    {
        return new JsonObject
        {
            ["genres"] = new JsonArray(),
            ["franchises"] = new JsonArray(),
            ["collections"] = new JsonArray(),
            ["companies"] = new JsonArray(),
            ["publishers"] = new JsonArray(),
            ["developers"] = new JsonArray(),
            ["game_modes"] = new JsonArray(),
            ["age_ratings"] = new JsonArray(),
            ["player_counts"] = new JsonArray(),
            ["regions"] = new JsonArray(),
            ["languages"] = new JsonArray(),
            ["tags"] = new JsonArray(),
            ["platforms"] = new JsonArray()
        };
    }
}
