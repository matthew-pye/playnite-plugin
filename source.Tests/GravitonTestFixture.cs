using Graviton.Models.RomM.Rom;
using Graviton.Settings;
using Graviton.Tests.Fakes;

using Moq;

using Playnite;

using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

using Xunit;

namespace Graviton.Tests
{
    public static class GravitonCollection
    {
        public const string Name = "GravitonPlugin";
    }

    [CollectionDefinition(GravitonCollection.Name)]
    public class GravitonCollectionDefinition : ICollectionFixture<GravitonTestFixture> { }

    public sealed class GravitonTestFixture : IDisposable
    {
        public string TempDir  { get; }
        public FakePlayniteSetup Playnite { get; }
        public Mock<ILogger> Logger { get; }

        private static void SetStatic(string field, object? value)
        {
            var fi = typeof(GravitonPlugin).GetField(field, BindingFlags.NonPublic | BindingFlags.Static) ?? throw new MissingFieldException(typeof(GravitonPlugin).Name, field);
            fi.SetValue(null, value);
        }

        private static void SetInstance(object target, string field, object? value)
        {
            var fi = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance) ?? throw new MissingFieldException(target.GetType().Name, field);
            fi.SetValue(target, value);
        }

        public GravitonTestFixture()
        {
            TempDir = Path.Combine(Path.GetTempPath(), $"GravitonTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(TempDir, "Games"));
            Directory.CreateDirectory(Path.Combine(TempDir, "Platforms"));

            Playnite = new FakePlayniteSetup(TempDir);

            Logger = new Mock<ILogger>(MockBehavior.Loose);
            
            Loc.Api = Playnite.Api;
            var plugin = (GravitonPlugin)RuntimeHelpers.GetUninitializedObject(typeof(GravitonPlugin));

            SetStatic("<Instance>k__BackingField", plugin);
            SetStatic("<PlayniteApi>k__BackingField", Playnite.Api);
            SetStatic("<Logger>k__BackingField", Logger.Object);

            SetInstance(plugin, "_settings", new GravitonPluginSettings());
            SetInstance(plugin, "<SettingsHandler>k__BackingField", new GravitonSettingsHandler(GravitonPlugin.Instance, Playnite.Api, Logger.Object));
            SetInstance(plugin, "<PluginDataPath>k__BackingField", TempDir);
            SetInstance(plugin, "<PluginDLLPath>k__BackingField", TempDir);
            SetInstance(plugin, "<ImportedGames>k__BackingField", new ConcurrentDictionary<string, RomMRomLocal>());

            HttpClientSingleton.Initialize(plugin);
        }

        public void ApplySettings(Action<GravitonPluginSettings> configure)
        {
            var settings = new GravitonPluginSettings();
            configure(settings);
            SetInstance(GravitonPlugin.Instance, "_settings", settings);
        }

        public void ResetGames()
        {
            Playnite.Games.Clear();
            GravitonPlugin.Instance.ImportedGames?.Clear();
        }

        public void SeedImportedGame(Game game)
        {
            Playnite.AddExistingGame(game);
            GravitonPlugin.Instance.ImportedGames![game.LibraryGameId!] = new RomMRomLocal
            {
                PlayniteID = game.Id
            };
        }

        public void Dispose()
        {
            try 
            { 
                Directory.Delete(TempDir, recursive: true); 
            }
            catch { }
        }
    }
}
