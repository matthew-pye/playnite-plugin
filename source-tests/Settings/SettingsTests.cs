using Graviton.Models;
using Graviton.Models.RomM.Platform;
using Graviton.Settings;

using Xunit;

using System.IO;

namespace Graviton.Tests.Settings
{
    [Collection(GravitonCollection.Name)]
    public class SettingsTests(GravitonTestFixture fixture)
    {
        #region Host

        [Theory]
        [InlineData("http://romm.local")]
        [InlineData("http://192.168.1.50:8080")]
        [InlineData("https://romm.example.com")]
        [InlineData("https://romm.example.com:443/path")]
        public void Host_ValidHttpOrHttps_IsStored(string url)
        {
            var settings = new GravitonPluginSettings();
            settings.Host = url;
            Assert.Equal(url, settings.Host);
        }

        [Fact]
        public void Host_SingleTrailingSlash_IsStripped()
        {
            var settings = new GravitonPluginSettings();
            settings.Host = "http://romm.local/";
            Assert.Equal("http://romm.local", settings.Host);
        }

        [Fact]
        public void Host_MultipleTrailingSlashes_AreStripped()
        {
            var settings = new GravitonPluginSettings();
            settings.Host = "https://romm.example.com///";
            Assert.Equal("https://romm.example.com", settings.Host);
        }

        [Theory]
        [InlineData("ftp://romm.local")]
        [InlineData("file:///etc/passwd")]
        [InlineData("not-a-url")]
        [InlineData("romm.local")]
        [InlineData("//romm.local")]
        public void Host_InvalidSchemeOrFormat_StoredAsEmpty(string bad)
        {
            var settings = new GravitonPluginSettings();
            settings.Host = bad;
            Assert.Equal(string.Empty, settings.Host);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Host_BlankInput_StoredAsEmpty(string blank)
        {
            var settings = new GravitonPluginSettings();
            settings.Host = blank;
            Assert.Equal(string.Empty, settings.Host);
        }

        #endregion

        #region ProfilePath

        [Fact]
        public void ProfilePath_QueryString_IsStrippedBeforeStorage()
        {
            var settings = new GravitonPluginSettings();
            settings.ProfilePath = "http://romm.local/api/users/me/avatar?size=large";
            Assert.DoesNotContain("size=large", settings.ProfilePath);
        }

        [Fact]
        public void ProfilePath_WithoutQueryString_StoredWithCacheBuster()
        {
            var settings = new GravitonPluginSettings();
            settings.ProfilePath = "C:/Users/Matt/Playnite 11/UserData/ExtensionsData/Matthew-Pye.Graviton/avatar.png";
            Assert.Contains("C:/Users/Matt/Playnite 11/UserData/ExtensionsData/Matthew-Pye.Graviton/avatar.png?", settings.ProfilePath);
        }

        [Fact]
        public void ProfilePath_EmptyValue_DefaultsToBundledProfileImage()
        {
            var settings = new GravitonPluginSettings();
            settings.ProfilePath = "";

            Assert.Equal(Path.Combine(GravitonPlugin.Instance.PluginDLLPath, "profile.png"), settings.ProfilePath);
        }

        #endregion

        #region Credentials

        [Fact]
        public void UsernameNP_PlainTextValue()
        {
            var settings = new GravitonPluginSettings();
            settings.UsernameNP = "alice";

            Assert.Equal("alice", settings.UsernameNP);
        }

        [Fact]
        public void UsernameNP_EmptyValue_StoredAsEmpty()
        {
            var settings = new GravitonPluginSettings();
            settings.UsernameNP = "";

            Assert.Equal(string.Empty, settings.Username);
            Assert.Equal(string.Empty, settings.UsernameNP);
        }

        [Fact]
        public void PasswordNP_PlainTextValue()
        {
            var settings = new GravitonPluginSettings();
            settings.PasswordNP = "s3cr3t";

            Assert.Equal("s3cr3t", settings.PasswordNP);
        }

        [Fact]
        public void ClientTokenNP_PlainTextValue()
        {
            var settings = new GravitonPluginSettings();
            settings.ClientTokenNP = "rmm_token123";

            Assert.Equal("rmm_token123", settings.ClientTokenNP);
        }

        [Fact]
        public void Username_ValueIsNotPlainText()
        {
            var settings = new GravitonPluginSettings();
            settings.UsernameNP = "alice";

            Assert.NotEqual("alice", settings.Username);
        }

        [Fact]
        public void Password_ValueIsNotPlainText()
        {
            var settings = new GravitonPluginSettings();
            settings.PasswordNP = "s3cr3t";

            Assert.NotEqual("s3cr3t", settings.Password);
        }

        [Fact]
        public void ClientToken_ValueIsNotPlainText()
        {
            var settings = new GravitonPluginSettings();
            settings.ClientTokenNP = "rmm_token123";

            Assert.NotEqual("rmm_token123", settings.ClientToken);
        }

        #endregion

        #region Clone

        [Fact]
        public void Clone_CopiesSettings()
        {
            var original = new GravitonPluginSettings
            {
                Host = "http://romm.local",
                UseBasicAuth = true,
                ExcludeGenres = "Adult",
                MergeRevisions = true,
                SkipMissingFiles = true,
                KeepDeletedGames = true,
                Use7z = true,
                PathTo7z = "C:\\Tools\\7z.exe",
                KeepStatusSynced = true,
                KeepFavouritesSynced = true,
                KeepPrivateNotesSynced = true,
                KeepPublicNotesSynced = true
            };

            var clone = original.Clone();

            Assert.Equal(original.Host, clone.Host);
            Assert.Equal(original.UseBasicAuth, clone.UseBasicAuth);
            Assert.Equal(original.ExcludeGenres, clone.ExcludeGenres);
            Assert.Equal(original.MergeRevisions, clone.MergeRevisions);
            Assert.Equal(original.SkipMissingFiles, clone.SkipMissingFiles);
            Assert.Equal(original.KeepDeletedGames, clone.KeepDeletedGames);
            Assert.Equal(original.Use7z, clone.Use7z);
            Assert.Equal(original.PathTo7z, clone.PathTo7z);
            Assert.Equal(original.KeepStatusSynced, clone.KeepStatusSynced);
            Assert.Equal(original.KeepFavouritesSynced, clone.KeepFavouritesSynced);
            Assert.Equal(original.KeepPrivateNotesSynced, clone.KeepPrivateNotesSynced);
            Assert.Equal(original.KeepPublicNotesSynced, clone.KeepPublicNotesSynced);
        }

        [Fact]
        public void Clone_CopiesCredentialsViaPlainTextProperties()
        {
            var original = new GravitonPluginSettings();
            original.UsernameNP = "alice";
            original.PasswordNP = "s3cr3t";
            original.ClientTokenNP = "rmm_token123";

            var clone = original.Clone();

            Assert.Equal("alice", clone.UsernameNP);
            Assert.Equal("s3cr3t", clone.PasswordNP);
            Assert.Equal("rmm_token123", clone.ClientTokenNP);
        }

        [Fact]
        public void Clone_CopiesMappingsAsANewList()
        {
            var original = new GravitonPluginSettings();
            original.Mappings.Add(new EmulatorMapping { MappingId = Guid.NewGuid() });

            var clone = original.Clone();

            Assert.NotSame(original.Mappings, clone.Mappings);
            Assert.Single(clone.Mappings);
        }

        [Fact]
        public void Clone_ProfilePath_PreservesPathButRegeneratesCacheBuster()
        {
            var original = new GravitonPluginSettings();
            original.ProfilePath = "C:/Users/Matt/Playnite 11/UserData/ExtensionsData/Matthew-Pye.Graviton/avatar.png";

            var clone = original.Clone();

            Assert.Equal(original.ProfilePath.Split('?')[0], clone.ProfilePath.Split('?')[0]);
        }

        [Fact]
        public void Clone_SharesAccountState()
        {
            var original = new GravitonPluginSettings();

            var clone = original.Clone();
            clone.AccountState.User = "bob";

            Assert.Same(original.AccountState, clone.AccountState);
            Assert.Equal("bob", original.AccountState.User);
        }

        #endregion

        #region Persistence

        [Fact]
        public void SaveAndLoad_Host()
        {
            var dir = fixture.TempDir;
            var settings   = new GravitonPluginSettings();
            settings.Host  = "http://romm.local";

            GravitonSettingsHandler.SaveSettings(dir, settings);
            var loaded = GravitonSettingsHandler.LoadSettings(dir);

            Assert.Equal("http://romm.local", loaded.Host);
        }

        [Fact]
        public void SaveAndLoad_BoolFlags()
        {
            var dir = fixture.TempDir;
            var settings   = new GravitonPluginSettings
            {
                MergeRevisions       = true,
                KeepDeletedGames     = true,
                KeepStatusSynced     = true,
                KeepFavouritesSynced = true
            };

            GravitonSettingsHandler.SaveSettings(dir, settings);
            var loaded = GravitonSettingsHandler.LoadSettings(dir);

            Assert.True(loaded.MergeRevisions);
            Assert.True(loaded.KeepDeletedGames);
            Assert.True(loaded.KeepStatusSynced);
            Assert.True(loaded.KeepFavouritesSynced);
        }

        [Fact]
        public void SaveAndLoad_Mappings_UpdatesAvailablePlatformsFromAccountState()
        {
            var dir = Path.Combine(fixture.TempDir, $"wiring_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);

            var settings = new GravitonPluginSettings();
            settings.AccountState.RomMPlatforms.Add(new RomMPlatform { Id = 1, Slug = "gba", Name = "Game Boy Advance" });
            settings.Mappings.Add(new EmulatorMapping { MappingId = Guid.NewGuid(), RomMPlatformId = 1 });

            GravitonSettingsHandler.SaveSettings(dir, settings);
            var loaded = GravitonSettingsHandler.LoadSettings(dir);

            var loadedMapping = Assert.Single(loaded.Mappings);
            Assert.Same(loaded.AccountState.RomMPlatforms, loadedMapping.AvailablePlatforms);
            Assert.Equal("Game Boy Advance", loadedMapping.RomMPlatform?.Name);
        }

        [Fact]
        public void LoadSettings_MissingSettingsFile_ReturnsDefaultSettings()
        {
            var emptyDir = Path.Combine(fixture.TempDir, $"empty_{Guid.NewGuid():N}");
            Directory.CreateDirectory(emptyDir);

            var loaded = GravitonSettingsHandler.LoadSettings(emptyDir);

            Assert.Equal(string.Empty, loaded.Host);
            Assert.Null(loaded.AccountState.LastAuthenticated);
            Assert.False(loaded.MergeRevisions);
            Assert.False(loaded.KeepDeletedGames);
        }

        [Fact]
        public void LoadSettings_CorruptedSettingsFile_ReturnsDefaultSettings()
        {
            var dir = Path.Combine(fixture.TempDir, $"corrupt_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "settings.json"), "{ this is not valid json");

            var loaded = GravitonSettingsHandler.LoadSettings(dir);

            Assert.Equal(string.Empty, loaded.Host);
        }

        [Fact]
        public void SaveSettings_DirectoryDoesNotExist_DoesNotThrow()
        {
            var missingDir = Path.Combine(fixture.TempDir, $"missing_{Guid.NewGuid():N}");
            var settings = new GravitonPluginSettings();

            var ex = Record.Exception(() => GravitonSettingsHandler.SaveSettings(missingDir, settings));

            Assert.Null(ex);
        }

        #endregion
    }
}
