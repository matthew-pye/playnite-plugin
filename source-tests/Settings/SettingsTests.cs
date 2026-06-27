using Graviton.Settings;
using Xunit;

using System.IO;

namespace Graviton.Tests.Settings
{
    [Collection(GravitonCollection.Name)]
    public class SettingsTests(GravitonTestFixture fixture)
    {
        [Theory]
        [InlineData("http://romm.local")]
        [InlineData("http://192.168.1.50:8080")]
        [InlineData("https://romm.example.com")]
        [InlineData("https://romm.example.com:443/path")]
        public void Host_ValidHttpOrHttps_IsStored(string url)
        {
            var s = new GravitonPluginSettings();
            s.Host = url;
            Assert.Equal(url, s.Host);
        }

        [Fact]
        public void Host_SingleTrailingSlash_IsStripped()
        {
            var s = new GravitonPluginSettings();
            s.Host = "http://romm.local/";
            Assert.Equal("http://romm.local", s.Host);
        }

        [Fact]
        public void Host_MultipleTrailingSlashes_AreAllStripped()
        {
            var s = new GravitonPluginSettings();
            s.Host = "https://romm.example.com///";
            Assert.Equal("https://romm.example.com", s.Host);
        }

        [Theory]
        [InlineData("ftp://romm.local")]
        [InlineData("file:///etc/passwd")]
        [InlineData("not-a-url")]
        [InlineData("romm.local")]
        [InlineData("//romm.local")]
        public void Host_InvalidSchemeOrFormat_StoredAsEmpty(string bad)
        {
            var s = new GravitonPluginSettings();
            s.Host = bad;
            Assert.Equal(string.Empty, s.Host);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Host_BlankInput_StoredAsEmpty(string blank)
        {
            var s = new GravitonPluginSettings();
            s.Host = blank;
            Assert.Equal(string.Empty, s.Host);
        }

        [Fact]
        public void ProfilePath_QueryString_IsStrippedBeforeStorage()
        {
            var s = new GravitonPluginSettings();
            s.ProfilePath = "http://romm.local/api/users/me/avatar?size=large";
            Assert.DoesNotContain("size=large", s.ProfilePath);
        }

        [Fact]
        public void ProfilePath_WithoutQueryString_StoredWithCacheBuster()
        {
            var s = new GravitonPluginSettings();
            s.ProfilePath = "http://romm.local/api/users/me/avatar";
            Assert.Contains("http://romm.local/api/users/me/avatar?", s.ProfilePath);
        }

        [Fact]
        public void SaveAndLoad_Host_RoundTrips()
        {
            var dir = fixture.TempDir;
            var s   = new GravitonPluginSettings();
            s.Host  = "http://romm.local";

            GravitonSettingsHandler.SaveSettings(dir, s);
            var loaded = GravitonSettingsHandler.LoadSettings(dir);

            Assert.Equal("http://romm.local", loaded.Host);
        }

        [Fact]
        public void SaveAndLoad_BoolFlags_AllRoundTrip()
        {
            var dir = fixture.TempDir;
            var s   = new GravitonPluginSettings
            {
                MergeRevisions       = true,
                KeepDeletedGames     = true,
                KeepStatusSynced     = true,
                KeepFavouritesSynced = true
            };

            GravitonSettingsHandler.SaveSettings(dir, s);
            var loaded = GravitonSettingsHandler.LoadSettings(dir);

            Assert.True(loaded.MergeRevisions);
            Assert.True(loaded.KeepDeletedGames);
            Assert.True(loaded.KeepStatusSynced);
            Assert.True(loaded.KeepFavouritesSynced);
        }

        [Fact]
        public void LoadSettings_MissingFile_ReturnsDefaultSettings()
        {
            var emptyDir = Path.Combine(fixture.TempDir, $"empty_{Guid.NewGuid():N}");
            Directory.CreateDirectory(emptyDir);

            var loaded = GravitonSettingsHandler.LoadSettings(emptyDir);

            Assert.Equal(string.Empty, loaded.Host);
            Assert.Null(loaded.AccountState.LastAuthenticated);
            Assert.False(loaded.MergeRevisions);
            Assert.False(loaded.KeepDeletedGames);
        }
    }
}
