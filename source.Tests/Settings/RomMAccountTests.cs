using Graviton.Settings;

using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

using Xunit;

namespace Graviton.Tests.Settings
{
    [Collection(GravitonCollection.Name)]
    public class RomMAccountTests(GravitonTestFixture fixture)
    {
        private RomMAuthentication MakeAccount() => new RomMAuthentication(GravitonPlugin.Instance, fixture.Playnite.Api, fixture.Logger.Object);

        private void SetActiveAccountState()
        {
            var state = GravitonPlugin.Instance.Settings.AccountState;
            state.User              = "alice";
            state.UserType          = "admin";
            state.ServerVersion     = "5.0.0";
            state.LastAuthenticated = DateTime.UtcNow;
        }

        private static Regex IconPathRegex => (Regex)typeof(RomMAuthentication).GetField("_iconPathRegex", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

        #region Login

        [Fact]
        public async Task Login_HostNotSet()
        {
            fixture.ApplySettings(s => s.Host = "");
            Assert.False(await MakeAccount().Login());
        }

        [Fact]
        public async Task Login_HostNotSet_ResetsAccountState()
        {
            fixture.ApplySettings(s => s.Host = "");
            SetActiveAccountState();

            await MakeAccount().Login();

            var state = GravitonPlugin.Instance.Settings.AccountState;
            Assert.Equal("----", state.User);
            Assert.Equal("----", state.UserType);
            Assert.Equal("---",  state.ServerVersion);
            Assert.Null(state.LastAuthenticated);
        }

        [Fact]
        public async Task Login_HostNotSet_SetsProfilePathToDefault()
        {
            fixture.ApplySettings(s => s.Host = "");

            await MakeAccount().Login();

            var expectedPrefix = Path.Combine(GravitonPlugin.Instance.PluginDLLPath, "profile.png");
            Assert.StartsWith(expectedPrefix, GravitonPlugin.Instance.Settings.ProfilePath);
        }

        [Fact]
        public async Task Login_BasicAuth_UsernameNotSet()
        {
            fixture.ApplySettings(s =>
            {
                s.Host         = "http://romm.local";
                s.UseBasicAuth = true;
            });

            Assert.False(await MakeAccount().Login());
        }

        [Fact]
        public async Task Login_BasicAuth_PasswordNotSet()
        {
            fixture.ApplySettings(s =>
            {
                s.Host         = "http://romm.local";
                s.UseBasicAuth = true;
                s.UsernameNP   = "alice";
            });

            Assert.False(await MakeAccount().Login());
        }

        [Fact]
        public async Task Login_BasicAuth_CredentialsMissing_ResetsAccountState()
        {
            fixture.ApplySettings(s =>
            {
                s.Host         = "http://romm.local";
                s.UseBasicAuth = true;
            });
            SetActiveAccountState();

            await MakeAccount().Login();

            var state = GravitonPlugin.Instance.Settings.AccountState;
            Assert.Equal("----", state.User);
            Assert.Equal("----", state.UserType);
            Assert.Null(state.LastAuthenticated);
        }

        [Fact]
        public async Task Login_TokenAuth_TokenNotSet()
        {
            fixture.ApplySettings(s =>
            {
                s.Host         = "http://romm.local";
                s.UseBasicAuth = false;
            });

            Assert.False(await MakeAccount().Login());
        }

        [Fact]
        public async Task Login_TokenAuth_TokenMissing_ResetsAccountState()
        {
            fixture.ApplySettings(s =>
            {
                s.Host         = "http://romm.local";
                s.UseBasicAuth = false;
            });
            SetActiveAccountState();

            await MakeAccount().Login();

            var state = GravitonPlugin.Instance.Settings.AccountState;
            Assert.Equal("----", state.User);
            Assert.Null(state.LastAuthenticated);
        }

        #endregion

        #region SyncUserData

        [Fact]
        public async Task SyncUserData_NotAuthenticated()
        {
            fixture.ApplySettings(s => s.Host = "http://romm.local");

            Assert.False(await MakeAccount().SyncUserData());
        }

        [Fact]
        public async Task SyncUserData_NotAuthenticated_ResetsAccountState()
        {
            fixture.ApplySettings(s => s.Host = "http://romm.local");
            SetActiveAccountState();

            GravitonPlugin.Instance.Settings.AccountState.LastAuthenticated = null;

            await MakeAccount().SyncUserData();

            var state = GravitonPlugin.Instance.Settings.AccountState;
            Assert.Equal("----", state.User);
            Assert.Equal("----", state.UserType);
            Assert.Null(state.LastAuthenticated);
        }

        #endregion

        #region SyncPlatforms

        [Fact]
        public async Task SyncPlatforms_ImportControllerIsNull()
        {
            fixture.ApplySettings(s => s.Host = "http://romm.local");

            Assert.False(await MakeAccount().SyncPlatforms());
        }

        #endregion

        #region InitDevicePair

        [Fact]
        public async Task InitDevicePair_HostNotSet()
        {
            fixture.ApplySettings(s => s.Host = "");

            Assert.Null(await MakeAccount().InitDevicePair());
        }

        #endregion

        #region IconPathRegex

        [Theory]
        [InlineData("users/alice/profile/avatar.png")]
        [InlineData("users/alice/profile/avatar.jpg")]
        [InlineData("users/alice/profile/avatar.jpeg")]
        [InlineData("users/alice/profile/avatar.webp")]
        [InlineData("users/bob_99/profile/avatar.png")]
        [InlineData("users/user.name/profile/avatar.jpg")]
        public void IconPathRegex_ValidAvatarPath_Matches(string path)
        {
            Assert.Matches(IconPathRegex.ToString(), path);
        }

        [Theory]
        [InlineData("",                                          "empty string")]
        [InlineData("avatar.png",                               "no directory segments")]
        [InlineData("users/alice/avatar.png",                   "missing /profile/ segment")]
        [InlineData("users/alice/profile/avatar.bmp",           "unsupported extension")]
        [InlineData("users/alice/profile/avatar.png.exe",       "double extension")]
        [InlineData("/users/alice/profile/avatar.png",          "leading slash makes it absolute")]
        [InlineData("users//profile/avatar.png",                "empty username segment")]
        [InlineData("assets/alice/profile/avatar.png",          "wrong root segment")]
        [InlineData("users/alice/profile/avatar.png/../../etc", "path traversal attempt")]
        public void IconPathRegex_InvalidPath_DoesNotMatch(string path, string _reason)
        {
            Assert.DoesNotMatch(IconPathRegex.ToString(), path);
        }

        #endregion
    }
}
