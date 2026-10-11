using Graviton.Models.RomM.Saves;

using System.IO;
using System.Text.Json;

namespace Graviton.Tests.Fakes.RomM;

internal static class RomMSaveFixtures
{
    public static RomMSave CreateSave(int id, int romId, string? slot, string fileName = "save.sav")
    {
        return new RomMSave
        {
            ID = id,
            ROMID = romId,
            UserID = 7,
            FileName = fileName,
            Slot = slot,
            ContentHash = $"hash-{id}",
            FileSize = 2048,
            CreatedAt = "2026-10-10T11:00:00Z",
            UpdatedAt = "2026-10-10T12:00:00Z"
        };
    }

    public static string CreateResponse(params RomMSave[] saves)
    {
        var response = saves.Select(save => new
        {
            id = save.ID,
            rom_id = save.ROMID,
            user_id = save.UserID,
            file_name = save.FileName,
            file_name_no_tags = save.FileName,
            file_name_no_ext = Path.GetFileNameWithoutExtension(save.FileName),
            file_extension = Path.GetExtension(save.FileName),
            file_path = $"saves/{save.ROMID}/{save.FileName}",
            file_size_bytes = save.FileSize,
            full_path = $"/romm/saves/{save.ROMID}/{save.FileName}",
            download_path = $"/api/saves/{save.ID}/content?timestamp={DateTimeOffset.Parse(save.UpdatedAt!).ToString("yyyy-MM-dd HH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture)}&name={Uri.EscapeDataString(save.FileName!)}",
            missing_from_fs = false,
            created_at = save.CreatedAt,
            updated_at = save.UpdatedAt,
            emulator = (string?)null,
            slot = save.Slot,
            content_hash = save.ContentHash,
            is_public = false,
            is_favorite = false,
            labels = Array.Empty<string>(),
            screenshot = (object?)null,
            origin_device_id = (string?)null,
            device_syncs = Array.Empty<object>()
        });

        return JsonSerializer.Serialize(response);
    }
}
