using Graviton.Tests.Fakes.Settings;

using NSubstitute;

using System.IO;
using System.Net;

using Xunit;

namespace Graviton.Tests.Settings;

[Collection("RomMAuthentication")]
public sealed class RomMAuthenticationUserTests
{
    [Fact]
    public async Task SyncUserData_PopulatesAccountAndUsesDefaultProfile()
    {
        using var test = new AuthenticationTestHarness();
        test.ConfigureUser();

        var result = await test.Authentication.SyncUserData();

        Assert.True(result);
        Assert.Equal("tester", test.Plugin.Settings.AccountState.User);
        Assert.Equal("user", test.Plugin.Settings.AccountState.UserType);
        Assert.Equal(1, test.Plugin.Settings.AccountState.UserID);
        Assert.NotNull(test.Plugin.Settings.AccountState.LastAuthenticated);
        Assert.StartsWith(test.ProfileImagePath, test.Plugin.Settings.ProfilePath);
        await test.RomM.Server.DidNotReceive().RawGETAsync(Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task SyncUserData_WhenServerFails_PreservesExistingAccount()
    {
        using var test = new AuthenticationTestHarness();
        test.Plugin.Settings.AccountState.User = "previous-user";
        test.RomM.ConfigureJsonGETResponse("/api/users/me", null);

        var result = await test.Authentication.SyncUserData();

        Assert.False(result);
        Assert.Equal("previous-user", test.Plugin.Settings.AccountState.User);
    }

    [Fact]
    public async Task SyncUserData_WhenResponseIsNotAnObject_UsesDefaultProfile()
    {
        using var test = new AuthenticationTestHarness();
        test.RomM.ConfigureJsonGETResponse("/api/users/me", "[]");

        var result = await test.Authentication.SyncUserData();

        Assert.False(result);
        Assert.StartsWith(test.ProfileImagePath, test.Plugin.Settings.ProfilePath);
    }

    [Theory]
    [InlineData("png")]
    [InlineData("jpg")]
    [InlineData("jpeg")]
    [InlineData("webp")]
    public async Task SyncUserData_DownloadsSupportedProfileAvatar(string extension)
    {
        using var test = new AuthenticationTestHarness();
        test.ConfigureUser($"users/1/profile/avatar.{extension}");
        var contents = new byte[] { 1, 2, 3, 4, 5 };
        test.RomM.ConfigureRawGETResponse("/api/users/1/avatar", contents);

        var result = await test.Authentication.SyncUserData();

        Assert.True(result);
        Assert.Equal(contents, File.ReadAllBytes(Path.Combine(test.RootDirectory, "avatar.png")));
        Assert.StartsWith(Path.Combine(test.RootDirectory, "avatar.png"), test.Plugin.Settings.ProfilePath);
        await test.RomM.Server.Received(1).RawGETAsync("/api/users/1/avatar", false);
    }

    [Theory]
    [InlineData("https://outside.example.test/avatar.png")]
    [InlineData("../users/1/profile/avatar.png")]
    [InlineData("users/1/profile/avatar.svg")]
    [InlineData("users/1/profile/avatar.png/extra")]
    public async Task SyncUserData_IgnoresAvatarPathsOutsideApprovedPattern(string avatarPath)
    {
        using var test = new AuthenticationTestHarness();
        test.ConfigureUser(avatarPath);

        var result = await test.Authentication.SyncUserData();

        Assert.True(result);
        Assert.StartsWith(test.ProfileImagePath, test.Plugin.Settings.ProfilePath);
        await test.RomM.Server.DidNotReceive().RawGETAsync(Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task SyncUserData_WhenAvatarRequestFails_DoesNotAcceptProfile()
    {
        using var test = new AuthenticationTestHarness();
        test.Plugin.Settings.AccountState.User = "previous-user";
        test.ConfigureUser("users/1/profile/avatar.png");
        test.RomM.ConfigureRawGETResponse("/api/users/1/avatar", [], HttpStatusCode.NotFound);

        var result = await test.Authentication.SyncUserData();

        Assert.False(result);
        Assert.Equal("previous-user", test.Plugin.Settings.AccountState.User);
        Assert.StartsWith(test.ProfileImagePath, test.Plugin.Settings.ProfilePath);
        Assert.False(File.Exists(Path.Combine(test.RootDirectory, "avatar.png")));
    }

    [Fact]
    public async Task SyncUserData_RejectsAvatarsOverTwentyMegabytes()
    {
        using var test = new AuthenticationTestHarness();
        test.ConfigureUser("users/1/profile/avatar.png");
        test.RomM.ConfigureRawGETResponse("/api/users/1/avatar", new byte[20 * 1024 * 1024 + 1]);

        var result = await test.Authentication.SyncUserData();

        Assert.False(result);
        Assert.StartsWith(test.ProfileImagePath, test.Plugin.Settings.ProfilePath);
        Assert.False(File.Exists(Path.Combine(test.RootDirectory, "avatar.png")));
    }
}
