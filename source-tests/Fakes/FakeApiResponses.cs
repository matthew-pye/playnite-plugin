namespace Graviton.Tests.Fakes
{
    public static class FakeApiResponses
    {
        // URL - /api/heartbeat
        public const string Heartbeat = """
            {
              "SYSTEM": {
                "VERSION": "0.15.2",
                "SHOW_SETUP_WIZARD": false
              }
            }
            """;

        // URL - /api/platforms
        public const string PlatformList = """
            [
              {
                "id": 1,
                "slug": "gba",
                "fs_slug": "gba",
                "name": "Game Boy Advance",
                "rom_count": 42,
                "igdb_id": 24,
                "url_logo": "https://example.com/gba.png"
              }
            ]
            """;

        // URL - /api/roms/101
        public const string RomMRom = """
            {
              "id": 101,
              "igdb_id": 4972,
              "ss_id": 512,
              "ra_id": 7653,
              "hasheous_id": 8801,
              "hltb_id": 3631,
              "platform_id": 1,
              "platform_slug": "gba",
              "platform_display_name": "Game Boy Advance",
              "fs_name": "Pokemon - Emerald Version (USA, Europe).gba",
              "fs_name_no_tags": "Pokemon - Emerald Version",
              "fs_name_no_ext": "Pokemon - Emerald Version (USA, Europe)",
              "fs_extension": "gba",
              "fs_path": "gba",
              "fs_size_bytes": 16777216,
              "name": "Pokemon Emerald Version",
              "slug": "pokemon-emerald-version",
              "summary": "Step into the Pokemon world in Pokemon Emerald.",
              "sha1_hash": "AAB0DD9D12B79B2A67B64A4C5F98F37DCEE60AA1",
              "crc_hash": "00D76021",
              "md5_hash": "605DEF0E0DA9C6DE2BD76D73CBC01DB8",
              "has_simple_single_file": true,
              "has_nested_single_file": false,
              "has_multiple_files": false,
              "has_cover": true,
              "url_cover": "https://images.igdb.com/igdb/image/upload/t_cover_big/co1ieo.jpg",
              "regions": ["USA", "Europe"],
              "languages": ["en"],
              "tags": [],
              "files": [
                {
                  "id": 201,
                  "file_name": "Pokemon - Emerald Version (USA, Europe).gba",
                  "file_size_bytes": 16777216,
                  "full_path": "gba/Pokemon - Emerald Version (USA, Europe).gba"
                }
              ],
              "sibling_roms": [],
              "metadatum": {
                "rom_id": 101,
                "genres": ["Role-playing (RPG)", "Adventure"],
                "franchises": ["Pokemon"],
                "collections": ["RPG Classics"],
                "companies": ["Game Freak", "Nintendo"],
                "game_modes": ["Single player", "Multiplayer"],
                "first_release_date": 1109203200000,
                "average_rating": 88.5
              },
              "igdb_metadata": {
                "age_ratings": [
                  {
                    "rating": "3",
                    "category": "PEGI",
                    "rating_cover_url": "https://example.com/pegi3.png"
                  }
                ]
              },
              "hltb_metadata": {
                "main_story": 25,
                "main_plus_extra": 40,
                "completionist": 200,
                "all_styles": 0
              },
              "rom_user": {
                "id": 1,
                "user_id": 42,
                "is_main_sibling": false,
                "last_played": "2025-01-15T18:30:00Z",
                "backlogged": false,
                "now_playing": false,
                "rating": 9,
                "status": "completed_100",
                "hidden": false
              },
              "user_collections": [
                {
                  "id": 10,
                  "name": "My GBA Favourites",
                  "rom_ids": [101],
                  "is_favorite": false
                }
              ],
              "all_user_notes": [],
              "created_at": "2024-03-01T12:00:00Z",
              "updated_at": "2024-05-10T09:00:00Z"
            }
            """;

        // URL - /api/roms/202 - Edited
        public const string RomMRomMinimal = """
            {
              "id": 202,
              "platform_id": 2,
              "fs_name": "Super Mario World (USA).sfc",
              "fs_size_bytes": 524288,
              "name": "Super Mario World",
              "sha1_hash": "6B47BB75D16514B6A476AA0C73A683A2A4C18765",
              "has_simple_single_file": true,
              "has_nested_single_file": false,
              "has_multiple_files": false,
              "files": [
                {
                  "id": 301,
                  "file_name": "Super Mario World (USA).sfc",
                  "file_size_bytes": 524288,
                  "full_path": "snes/Super Mario World (USA).sfc"
                }
              ],
              "created_at": "2024-01-10T00:00:00Z",
              "updated_at": "2024-01-10T00:00:00Z"
            }
            """;

        // URL - /api/roms/303
        public const string RomMRomInFavourites = """
            {
              "id": 303,
              "platform_id": 1,
              "fs_name": "Zelda - The Legend of Zelda (USA).nes",
              "fs_size_bytes": 131072,
              "name": "The Legend of Zelda",
              "sha1_hash": "BA4F07C8F01B1219E4BF2E7E83E5D64E55B43E97",
              "has_simple_single_file": true,
              "has_nested_single_file": false,
              "has_multiple_files": false,
              "files": [
                {
                  "id": 401,
                  "file_name": "Zelda - The Legend of Zelda (USA).nes",
                  "file_size_bytes": 131072,
                  "full_path": "nes/Zelda - The Legend of Zelda (USA).nes"
                }
              ],
              "user_collections": [
                {
                  "id": 1,
                  "name": "Favorites",
                  "rom_ids": [303],
                  "is_favorite": true
                }
              ],
              "created_at": "2024-01-01T00:00:00Z",
              "updated_at": "2024-01-01T00:00:00Z"
            }
            """;

        // URL - /api/roms/404
        public const string RomMRomMultiFile = """
            {
              "id": 404,
              "platform_id": 3,
              "fs_name": "Final Fantasy VII (USA)",
              "fs_size_bytes": 1073741824,
              "name": "Final Fantasy VII",
              "sha1_hash": "CCDDAABB11223344EEFF5566778899AABBCCDDEE",
              "has_simple_single_file": false,
              "has_nested_single_file": false,
              "has_multiple_files": true,
              "files": [
                {
                  "id": 501,
                  "file_name": "disc1.bin",
                  "file_size_bytes": 300000000,
                  "full_path": "psx/Final Fantasy VII/discs/disc1.bin"
                },
                {
                  "id": 502,
                  "file_name": "Final Fantasy VII.cue",
                  "file_size_bytes": 512,
                  "full_path": "psx/Final Fantasy VII/Final Fantasy VII.cue"
                },
                {
                  "id": 503,
                  "file_name": "disc2.bin",
                  "file_size_bytes": 300000000,
                  "full_path": "psx/Final Fantasy VII/discs/disc2.bin"
                }
              ],
              "created_at": "2024-01-01T00:00:00Z",
              "updated_at": "2024-01-01T00:00:00Z"
            }
            """;

        // URL - /api/roms/505
        public const string RomMRomHidden = """
            {
              "id": 505,
              "platform_id": 1,
              "fs_name": "Hidden Game (USA).gba",
              "fs_size_bytes": 8388608,
              "name": "Hidden Game",
              "sha1_hash": "DEADBEEF1234567890ABCDEFDEADBEEF12345678",
              "has_simple_single_file": true,
              "has_nested_single_file": false,
              "has_multiple_files": false,
              "files": [
                {
                  "id": 601,
                  "file_name": "Hidden Game (USA).gba",
                  "file_size_bytes": 8388608,
                  "full_path": "gba/Hidden Game (USA).gba"
                }
              ],
              "rom_user": {
                "id": 2,
                "user_id": 42,
                "is_main_sibling": false,
                "backlogged": false,
                "now_playing": false,
                "rating": 0,
                "status": "not_played",
                "hidden": true
              },
              "created_at": "2024-01-01T00:00:00Z",
              "updated_at": "2024-01-01T00:00:00Z"
            }
            """;

        // URL - /api/roms/606 - Edited
        public const string RomMRomNoFileName = """
            {
              "id": 606,
              "platform_id": 1,
              "fs_name": "",
              "fs_size_bytes": 0,
              "name": "Corrupt Entry",
              "sha1_hash": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
              "has_simple_single_file": false,
              "has_nested_single_file": false,
              "has_multiple_files": false,
              "files": [],
              "created_at": "2024-01-01T00:00:00Z",
              "updated_at": "2024-01-01T00:00:00Z"
            }
            """;
    }
}
