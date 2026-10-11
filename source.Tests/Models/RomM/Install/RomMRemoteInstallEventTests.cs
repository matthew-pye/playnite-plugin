using Graviton.Models.RomM.Install;

using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Models.RomM.Install;

public sealed class RomMRemoteInstallEventTests
{
    [Fact]
    public void ClaimResponse_WithTakenRequest_PreservesIdentifiersAndFileIds()
    {
        const string json = """
            [
              {
                "id": "install-request-123",
                "user_id": 7,
                "device_id": "handheld-01",
                "rom_id": 42,
                "file_ids": [101, 102],
                "status": "taken",
                "reason": null,
                "created_at": "2026-10-10T14:00:00Z",
                "updated_at": "2026-10-10T14:01:00Z"
              }
            ]
            """;

        var requests = JsonSerializer.Deserialize<List<RomMRemoteInstallEvent>>(json);
        var request = Assert.Single(Assert.IsType<List<RomMRemoteInstallEvent>>(requests));

        Assert.Equal("install-request-123", request.Id);
        Assert.Equal(42, request.RomId);
        Assert.Equal(new long[] { 101, 102 }, request.fileIDs);
        Assert.Equal("taken", request.Status);
        Assert.Null(request.Reason);
    }

    [Fact]
    public void ClaimResponse_WithMultipleRequests_PreservesEachRequest()
    {
        const string json = """
            [
              {
                "id": "install-request-a",
                "user_id": 7,
                "device_id": "handheld-01",
                "rom_id": 42,
                "file_ids": [101],
                "status": "taken",
                "reason": null,
                "created_at": "2026-10-10T14:00:00Z",
                "updated_at": "2026-10-10T14:01:00Z"
              },
              {
                "id": "install-request-b",
                "user_id": 7,
                "device_id": "handheld-01",
                "rom_id": 84,
                "file_ids": [201, 202],
                "status": "taken",
                "reason": null,
                "created_at": "2026-10-10T14:02:00Z",
                "updated_at": "2026-10-10T14:03:00Z"
              }
            ]
            """;

        var requests = Assert.IsType<List<RomMRemoteInstallEvent>>(
            JsonSerializer.Deserialize<List<RomMRemoteInstallEvent>>(json));

        Assert.Equal(2, requests.Count);
        Assert.Equal("install-request-a", requests[0].Id);
        Assert.Equal(42, requests[0].RomId);
        Assert.Equal(new long[] { 101 }, requests[0].fileIDs);
        Assert.Equal("install-request-b", requests[1].Id);
        Assert.Equal(84, requests[1].RomId);
        Assert.Equal(new long[] { 201, 202 }, requests[1].fileIDs);
    }

    [Fact]
    public void CancellationEvent_PreservesStatusAndReason()
    {
        const string json = """
            {
              "id": "install-request-123",
              "user_id": 7,
              "device_id": "handheld-01",
              "rom_id": 42,
              "file_ids": [101, 102],
              "status": "cancelled",
              "reason": "Cancelled by user",
              "created_at": "2026-10-10T14:00:00Z",
              "updated_at": "2026-10-10T14:05:00Z"
            }
            """;

        var request = Assert.IsType<RomMRemoteInstallEvent>(
            JsonSerializer.Deserialize<RomMRemoteInstallEvent>(json));

        Assert.Equal("install-request-123", request.Id);
        Assert.Equal(42, request.RomId);
        Assert.Equal("cancelled", request.Status);
        Assert.Equal("Cancelled by user", request.Reason);
    }
}
