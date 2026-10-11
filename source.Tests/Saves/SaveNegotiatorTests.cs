using Graviton.Models.RomM;
using Graviton.Models.RomM.Saves;
using Graviton.Saves;
using Graviton.Tests.Fakes.RomM;

using NSubstitute;

using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Saves;

public sealed class SaveNegotiatorTests
{
    [Theory]
    [InlineData("upload", SaveSyncStatus.upload)]
    [InlineData("download", SaveSyncStatus.download)]
    [InlineData("conflict", SaveSyncStatus.conflict)]
    [InlineData("no_op", SaveSyncStatus.no_op)]
    [InlineData("delete", SaveSyncStatus.delete)]
    public async Task Negotiate_ParsesSyncAction(string action, SaveSyncStatus expectedAction)
    {
        var romm = new RomMServerFake();
        var serverHasSave = action is "download" or "conflict" or "no_op";

        var reason = action switch
        {
            "upload" => "Save exists on client but not on server",
            "download" => "Server save is newer (no sync history)",
            "conflict" => "Same timestamp but different content",
            "no_op" => "Content is identical",
            "delete" => "Save was deleted on the server",
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };

        var json = JsonSerializer.Serialize(new
        {
            session_id = 12,
            operations = new[]
            {
                new
                {
                    action,
                    rom_id = 42,
                    save_id = serverHasSave ? 123 : (int?)null,
                    file_name = "autosave.sav",
                    slot = "autosave",
                    emulator = (string?)null,
                    reason,
                    server_updated_at = serverHasSave ? "2026-10-10T12:00:00Z" : null,
                    server_content_hash = serverHasSave ? "abc123" : null
                }
            },
            total_upload = action == "upload" ? 1 : 0,
            total_download = action == "download" ? 1 : 0,
            total_conflict = action == "conflict" ? 1 : 0,
            total_no_op = action == "no_op" ? 1 : 0,
            total_delete = action == "delete" ? 1 : 0
        });

        romm.ConfigureJsonPOSTResponse("/api/sync/negotiate", json);

        var request = new RomMNegotiate
        {
            DeviceID = "handheld-device",
            Saves = new List<RomMNegotiateSave>
            {
                new()
                {
                    ROMID = 42,
                    FileName = "autosave.sav",
                    Slot = "autosave",
                    ContentHash = action == "no_op" ? "abc123" : "old-hash",
                    UpdatedAt = action == "conflict"
                        ? "2026-10-10T12:00:00Z"
                        : "2026-10-10T11:00:00Z",
                    FileSize = 1024
                }
            }
        };

        var negotiator = CreateNegotiator(romm);
        var result = await negotiator.Negotiate(request);

        Assert.NotNull(result);
        Assert.Equal(12, result.SessionID);
        Assert.Equal(expectedAction, Assert.Single(result.Operations).Action);
    }

    [Fact]
    public async Task Negotiate_PreservesOperationDetailsAndSendsRequest()
    {
        var romm = new RomMServerFake();
        romm.ConfigureJsonPOSTResponse("/api/sync/negotiate", """
        {
            "session_id": 73,
            "operations": [
                {
                    "action": "download",
                    "rom_id": 42,
                    "save_id": 123,
                    "file_name": "autosave.sav",
                    "slot": "autosave",
                    "emulator": null,
                    "reason": "Server save is newer (no sync history)",
                    "server_content_hash": "abc123",
                    "server_updated_at": "2026-10-10T12:00:00Z"
                }
            ],
            "total_upload": 0,
            "total_download": 1,
            "total_conflict": 0,
            "total_no_op": 0,
            "total_delete": 0
        }
        """);

        var request = new RomMNegotiate
        {
            DeviceID = "handheld-device",
            Saves = new List<RomMNegotiateSave>
            {
                new()
                {
                    ROMID = 42,
                    FileName = "autosave.sav",
                    Slot = "autosave",
                    ContentHash = "old-hash",
                    UpdatedAt = "2026-10-10T11:00:00Z",
                    FileSize = 1024
                }
            }
        };

        var negotiator = CreateNegotiator(romm);
        var result = await negotiator.Negotiate(request);

        Assert.NotNull(result);
        Assert.Equal(73, result.SessionID);

        var operation = Assert.Single(result.Operations);
        Assert.Equal(SaveSyncStatus.download, operation.Action);
        Assert.Equal(42, operation.ROMID);
        Assert.Equal(123, operation.SaveID);
        Assert.Equal("autosave.sav", operation.FileName);
        Assert.Equal("autosave", operation.Slot);
        Assert.Equal("Server save is newer (no sync history)", operation.Reason);
        Assert.Equal("abc123", operation.ServerContentHash);
        Assert.Equal("2026-10-10T12:00:00Z", operation.ServerUpdatedAt);

        await romm.Server.Received(1).POSTAsync(
            APIPath: "/api/sync/negotiate",
            json: request,
            PublicEndpoint: false);

        var requestJson = JsonSerializer.SerializeToElement(request);
        Assert.Equal("handheld-device", requestJson.GetProperty("device_id").GetString());

        var saves = requestJson.GetProperty("saves");
        Assert.Equal(1, saves.GetArrayLength());

        var save = saves[0];
        Assert.Equal(42, save.GetProperty("rom_id").GetInt32());
        Assert.Equal("autosave.sav", save.GetProperty("file_name").GetString());
        Assert.Equal("autosave", save.GetProperty("slot").GetString());
        Assert.Equal("old-hash", save.GetProperty("content_hash").GetString());
        Assert.Equal("2026-10-10T11:00:00Z", save.GetProperty("updated_at").GetString());
        Assert.Equal(1024, save.GetProperty("file_size_bytes").GetInt64());
    }

    [Fact]
    public async Task Negotiate_WithNoOperations_ReturnsEmptyOperations()
    {
        var romm = new RomMServerFake();
        romm.ConfigureJsonPOSTResponse("/api/sync/negotiate", """
        {
            "session_id": 14,
            "operations": [],
            "total_upload": 0,
            "total_download": 0,
            "total_conflict": 0,
            "total_no_op": 0,
            "total_delete": 0
        }
        """);

        var request = new RomMNegotiate
        {
            DeviceID = "handheld-device"
        };

        var negotiator = CreateNegotiator(romm);
        var result = await negotiator.Negotiate(request);

        Assert.NotNull(result);
        Assert.Equal(14, result.SessionID);
        Assert.Empty(result.Operations);
    }

    [Fact]
    public async Task Negotiate_WhenRomMReturnsNull_ReturnsNull()
    {
        var romm = new RomMServerFake();
        romm.ConfigureJsonPOSTResponse("/api/sync/negotiate");

        var request = new RomMNegotiate
        {
            DeviceID = "handheld-device"
        };

        var negotiator = CreateNegotiator(romm);
        var result = await negotiator.Negotiate(request);

        Assert.Null(result);
    }

    [Fact]
    public async Task Negotiate_WithMultipleSlots_PreservesAllOperations()
    {
        var romm = new RomMServerFake();
        romm.ConfigureJsonPOSTResponse("/api/sync/negotiate", """
        {
            "session_id": 24,
            "operations": [
                {
                    "action": "upload",
                    "rom_id": 42,
                    "save_id": null,
                    "file_name": "autosave.sav",
                    "slot": "autosave",
                    "emulator": null,
                    "reason": "Save exists on client but not on server",
                    "server_updated_at": null,
                    "server_content_hash": null
                },
                {
                    "action": "download",
                    "rom_id": 42,
                    "save_id": 105,
                    "file_name": "manual-1.sav",
                    "slot": "manual-1",
                    "emulator": null,
                    "reason": "Save exists on server but not on client",
                    "server_updated_at": "2026-10-10T12:00:00Z",
                    "server_content_hash": "new-hash"
                }
            ],
            "total_upload": 1,
            "total_download": 1,
            "total_conflict": 0,
            "total_no_op": 0,
            "total_delete": 0
        }
        """);

        var request = new RomMNegotiate
        {
            DeviceID = "handheld-device",
            Saves = new List<RomMNegotiateSave>
            {
                new()
                {
                    ROMID = 42,
                    FileName = "autosave.sav",
                    Slot = "autosave",
                    ContentHash = "local-hash",
                    UpdatedAt = "2026-10-10T11:00:00Z",
                    FileSize = 1024
                }
            }
        };

        var negotiator = CreateNegotiator(romm);
        var result = await negotiator.Negotiate(request);

        Assert.NotNull(result);
        Assert.Equal(2, result.Operations.Count);

        var autosave = result.Operations[0];
        Assert.Equal(42, autosave.ROMID);
        Assert.Equal("autosave", autosave.Slot);
        Assert.Equal(SaveSyncStatus.upload, autosave.Action);
        Assert.Null(autosave.SaveID);

        var manualSave = result.Operations[1];
        Assert.Equal(42, manualSave.ROMID);
        Assert.Equal("manual-1", manualSave.Slot);
        Assert.Equal(SaveSyncStatus.download, manualSave.Action);
        Assert.Equal(105, manualSave.SaveID);
    }

    [Fact]
    public async Task Negotiate_WithArchivalSave_UploadsWithNullSlot()
    {
        var romm = new RomMServerFake();
        romm.ConfigureJsonPOSTResponse("/api/sync/negotiate", """
        {
            "session_id": 25,
            "operations": [
                {
                    "action": "upload",
                    "rom_id": 42,
                    "save_id": null,
                    "file_name": "manual-backup.sav",
                    "slot": null,
                    "emulator": null,
                    "reason": "Save exists on client but not on server",
                    "server_updated_at": null,
                    "server_content_hash": null
                }
            ],
            "total_upload": 1,
            "total_download": 0,
            "total_conflict": 0,
            "total_no_op": 0,
            "total_delete": 0
        }
        """);

        var request = new RomMNegotiate
        {
            DeviceID = "handheld-device",
            Saves = new List<RomMNegotiateSave>
            {
                new()
                {
                    ROMID = 42,
                    FileName = "manual-backup.sav",
                    Slot = null,
                    ContentHash = "archive-hash",
                    UpdatedAt = "2026-10-10T11:00:00Z",
                    FileSize = 1024
                }
            }
        };

        var negotiator = CreateNegotiator(romm);
        var result = await negotiator.Negotiate(request);

        Assert.NotNull(result);

        var operation = Assert.Single(result.Operations);
        Assert.Equal(SaveSyncStatus.upload, operation.Action);
        Assert.Null(operation.Slot);
        Assert.Null(operation.SaveID);
        Assert.Equal("manual-backup.sav", operation.FileName);
        Assert.Equal("Save exists on client but not on server", operation.Reason);
    }

    [Fact]
    public async Task Negotiate_WithAdditionalResponseFields_ParsesKnownFields()
    {
        var romm = new RomMServerFake();
        romm.ConfigureJsonPOSTResponse("/api/sync/negotiate", """
        {
            "session_id": 26,
            "future_session_property": true,
            "operations": [
                {
                    "action": "no_op",
                    "rom_id": 42,
                    "save_id": 107,
                    "file_name": "autosave.sav",
                    "slot": "autosave",
                    "emulator": null,
                    "reason": "Content is identical",
                    "server_updated_at": "2026-10-10T12:00:00Z",
                    "server_content_hash": "abc123",
                    "future_operation_property": {
                        "revision": 3
                    }
                }
            ],
            "total_upload": 0,
            "total_download": 0,
            "total_conflict": 0,
            "total_no_op": 1,
            "total_delete": 0
        }
        """);

        var request = new RomMNegotiate
        {
            DeviceID = "handheld-device",
            Saves = new List<RomMNegotiateSave>
            {
                new()
                {
                    ROMID = 42,
                    FileName = "autosave.sav",
                    Slot = "autosave",
                    ContentHash = "abc123",
                    UpdatedAt = "2026-10-10T11:00:00Z",
                    FileSize = 1024
                }
            }
        };

        var negotiator = CreateNegotiator(romm);
        var result = await negotiator.Negotiate(request);

        Assert.NotNull(result);
        Assert.Equal(26, result.SessionID);

        var operation = Assert.Single(result.Operations);
        Assert.Equal(42, operation.ROMID);
        Assert.Equal("autosave", operation.Slot);
        Assert.Equal(SaveSyncStatus.no_op, operation.Action);
        Assert.Equal(107, operation.SaveID);
        Assert.Equal("abc123", operation.ServerContentHash);
    }

    [Fact]
    public async Task Negotiate_WithOnlyServerSave_ReturnsDownload()
    {
        var romm = new RomMServerFake();
        romm.ConfigureJsonPOSTResponse("/api/sync/negotiate", """
        {
            "session_id": 80,
            "operations": [
                {
                    "action": "download",
                    "rom_id": 42,
                    "save_id": 301,
                    "file_name": "autosave.sav",
                    "slot": "autosave",
                    "emulator": null,
                    "reason": "Save exists on server but not on client",
                    "server_updated_at": "2026-10-10T12:00:00Z",
                    "server_content_hash": "server-hash"
                }
            ],
            "total_upload": 0,
            "total_download": 1,
            "total_conflict": 0,
            "total_no_op": 0,
            "total_delete": 0
        }
        """);

        var request = new RomMNegotiate
        {
            DeviceID = "handheld-device"
        };

        var negotiator = CreateNegotiator(romm);
        var result = await negotiator.Negotiate(request);

        Assert.NotNull(result);
        Assert.Equal(80, result.SessionID);

        var operation = Assert.Single(result.Operations);
        Assert.Equal(SaveSyncStatus.download, operation.Action);
        Assert.Equal(42, operation.ROMID);
        Assert.Equal(301, operation.SaveID);
        Assert.Equal("autosave.sav", operation.FileName);
        Assert.Equal("autosave", operation.Slot);
        Assert.Equal("server-hash", operation.ServerContentHash);

        await romm.Server.Received(1).POSTAsync(
            APIPath: "/api/sync/negotiate",
            json: request,
            PublicEndpoint: false);

        var requestJson = JsonSerializer.SerializeToElement(request);
        Assert.Equal(0, requestJson.GetProperty("saves").GetArrayLength());
    }

    [Fact]
    public async Task Negotiate_WithSameSlotOnDifferentRoms_PreservesBothOperations()
    {
        var romm = new RomMServerFake();
        romm.ConfigureJsonPOSTResponse("/api/sync/negotiate", """
        {
            "session_id": 81,
            "operations": [
                {
                    "action": "upload",
                    "rom_id": 42,
                    "save_id": null,
                    "file_name": "autosave.sav",
                    "slot": "autosave",
                    "emulator": null,
                    "reason": "Save exists on client but not on server",
                    "server_updated_at": null,
                    "server_content_hash": null
                },
                {
                    "action": "download",
                    "rom_id": 84,
                    "save_id": 302,
                    "file_name": "autosave.sav",
                    "slot": "autosave",
                    "emulator": null,
                    "reason": "Server save is newer (no sync history)",
                    "server_updated_at": "2026-10-10T12:00:00Z",
                    "server_content_hash": "rom-84-server-hash"
                }
            ],
            "total_upload": 1,
            "total_download": 1,
            "total_conflict": 0,
            "total_no_op": 0,
            "total_delete": 0
        }
        """);

        var request = new RomMNegotiate
        {
            DeviceID = "handheld-device",
            Saves = new List<RomMNegotiateSave>
            {
                new()
                {
                    ROMID = 42,
                    FileName = "autosave.sav",
                    Slot = "autosave",
                    ContentHash = "rom-42-client-hash",
                    UpdatedAt = "2026-10-10T11:00:00Z",
                    FileSize = 1024
                },
                new()
                {
                    ROMID = 84,
                    FileName = "autosave.sav",
                    Slot = "autosave",
                    ContentHash = "rom-84-client-hash",
                    UpdatedAt = "2026-10-10T11:00:00Z",
                    FileSize = 2048
                }
            }
        };

        var negotiator = CreateNegotiator(romm);
        var result = await negotiator.Negotiate(request);

        Assert.NotNull(result);
        Assert.Equal(2, result.Operations.Count);

        var upload = result.Operations[0];
        Assert.Equal(SaveSyncStatus.upload, upload.Action);
        Assert.Equal(42, upload.ROMID);
        Assert.Equal("autosave", upload.Slot);
        Assert.Null(upload.SaveID);

        var download = result.Operations[1];
        Assert.Equal(SaveSyncStatus.download, download.Action);
        Assert.Equal(84, download.ROMID);
        Assert.Equal("autosave", download.Slot);
        Assert.Equal(302, download.SaveID);
        Assert.Equal("rom-84-server-hash", download.ServerContentHash);

        var requestJson = JsonSerializer.SerializeToElement(request);
        var saves = requestJson.GetProperty("saves");
        Assert.Equal(2, saves.GetArrayLength());
        Assert.Equal(42, saves[0].GetProperty("rom_id").GetInt32());
        Assert.Equal(84, saves[1].GetProperty("rom_id").GetInt32());
    }

    [Fact]
    public async Task Negotiate_WithUntrackedServerSave_ParsesNoOpWithoutServerMetadata()
    {
        var romm = new RomMServerFake();
        romm.ConfigureJsonPOSTResponse("/api/sync/negotiate", """
        {
            "session_id": 82,
            "operations": [
                {
                    "action": "no_op",
                    "rom_id": 42,
                    "save_id": 303,
                    "file_name": "autosave.sav",
                    "slot": "autosave",
                    "emulator": null,
                    "reason": "Save is untracked on this device",
                    "server_updated_at": null,
                    "server_content_hash": null
                }
            ],
            "total_upload": 0,
            "total_download": 0,
            "total_conflict": 0,
            "total_no_op": 1,
            "total_delete": 0
        }
        """);

        var request = new RomMNegotiate
        {
            DeviceID = "handheld-device",
            Saves = new List<RomMNegotiateSave>
            {
                new()
                {
                    ROMID = 42,
                    FileName = "autosave.sav",
                    Slot = "autosave",
                    ContentHash = "client-hash",
                    UpdatedAt = "2026-10-10T11:00:00Z",
                    FileSize = 1024
                }
            }
        };

        var negotiator = CreateNegotiator(romm);
        var result = await negotiator.Negotiate(request);

        Assert.NotNull(result);

        var operation = Assert.Single(result.Operations);
        Assert.Equal(SaveSyncStatus.no_op, operation.Action);
        Assert.Equal(303, operation.SaveID);
        Assert.Equal("Save is untracked on this device", operation.Reason);
        Assert.Null(operation.ServerUpdatedAt);
        Assert.Null(operation.ServerContentHash);
    }

    private static SaveNegotiator CreateNegotiator(RomMServerFake romm)
    {
        return new SaveNegotiator(
            plugin: null!,
            playniteAPI: null!,
            logger: null!,
            romMServer: romm.Server);
    }
}
