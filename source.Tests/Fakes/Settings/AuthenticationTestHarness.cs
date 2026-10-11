using Graviton.Notifications;
using Graviton.Settings;
using Graviton.Tests.Fakes.Graviton;
using Graviton.Tests.Fakes.Playnite;
using Graviton.Tests.Fakes.RomM;

using NSubstitute;

using Playnite;

using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Graviton.Tests.Fakes.Settings;

internal sealed class AuthenticationTestHarness : IDisposable
{
    private readonly IPlayniteApi _previousLocalizationApi;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "GravitonAuthTests", Guid.NewGuid().ToString("N"));

    public GravitonContextFake Plugin { get; } = new();
    public PlayniteFake Playnite { get; } = new();
    public RomMServerFake RomM { get; } = new();
    public RomMAuthentication Authentication { get; }
    public string RootDirectory => _root;
    public string ProfileImagePath => Path.Combine(_root, "profile.png");

    public AuthenticationTestHarness()
    {
        Directory.CreateDirectory(_root);
        Plugin.ConfigurePluginDataDirectory(_root);
        Plugin.Context.PluginDLLPath.Returns(_root);

        _previousLocalizationApi = Loc.Api;
        Loc.Api = Playnite.Api;

        Plugin.Settings.Host = "https://romm.example.test";
        Authentication = new RomMAuthentication(
            Plugin.Context,
            Playnite.Api,
            new GravitonLogger(Plugin.Settings),
            RomM.Server);
    }

    public void ConfigureHeartbeat(string version = "5.4.0")
    {
        var response = JsonSerializer.Serialize(new
        {
            SYSTEM = new
            {
                VERSION = version,
                SHOW_SETUP_WIZARD = false
            }
        });

        RomM.ConfigureJsonGETResponse("/api/heartbeat", response);
    }

    public void ConfigureDeviceRegistration(string deviceID = "device-123")
    {
        var response = JsonSerializer.Serialize(new
        {
            device_id = deviceID,
            name = "Graviton",
            created_at = "2026-10-10T10:00:00Z"
        });

        RomM.ConfigureJsonPOSTResponse("/api/devices", response);
    }

    public void ConfigureUser(string? avatarPath = null)
    {
        var user = JsonNode.Parse(RomMRomFixtures.CreateUserResponse())!.AsObject();
        if (avatarPath is not null)
        {
            user["avatar_path"] = avatarPath;
        }

        RomM.ConfigureJsonGETResponse("/api/users/me", user.ToJsonString());
    }

    public void ConfigureToken(string token = "test-access-token")
    {
        Plugin.Settings.UseBasicAuth = false;
        Plugin.Settings.ClientTokenNP = token;
    }

    public void ConfigureBasicAuth(string username = "tester", string password = "password")
    {
        Plugin.Settings.UseBasicAuth = true;
        Plugin.Settings.UsernameNP = username;
        Plugin.Settings.PasswordNP = password;
    }

    public void Dispose()
    {
        Loc.Api = _previousLocalizationApi;

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
