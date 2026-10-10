using Graviton.Models.ROM;
using Graviton.Tests.Fakes.Graviton;

using System.IO;
using System.Text.Json;

using Xunit;

namespace Graviton.Tests.Models.ROM;

public sealed class RomMRomLocalTests : IDisposable
{
    private readonly string _pluginDataDirectory = Path.Combine(
        Path.GetTempPath(),
        "Graviton.Tests",
        Guid.NewGuid().ToString("N"));

    public RomMRomLocalTests()
    {
        Directory.CreateDirectory(Path.Combine(_pluginDataDirectory, "Games"));
    }

    [Fact]
    public void Save_WritesGameDataToConfiguredDirectory()
    {
        var plugin = CreateContext();
        var rom = new RomMRomLocal
        {
            Id = 42,
            Name = "Example Game",
            InstalledPath = "C:/Games/Example Game"
        };

        rom.Save(plugin.Context);

        var savePath = Path.Combine(_pluginDataDirectory, "Games", "42.json");
        Assert.True(File.Exists(savePath));

        var saved = JsonSerializer.Deserialize<RomMRomLocal>(File.ReadAllText(savePath));
        Assert.NotNull(saved);
        Assert.Equal(rom.Id, saved.Id);
        Assert.Equal(rom.Name, saved.Name);
        Assert.Equal(rom.InstalledPath, saved.InstalledPath);
    }

    [Fact]
    public void Save_AddsGameToImportedGames()
    {
        var plugin = CreateContext();
        var rom = new RomMRomLocal
        {
            Id = 42,
            Name = "Example Game"
        };

        rom.Save(plugin.Context);

        Assert.True(plugin.ImportedGames.TryGetValue("42", out var imported));
        Assert.Same(rom, imported);
    }

    [Fact]
    public void Save_ReplacesExistingImportedGame()
    {
        var plugin = CreateContext();
        var original = new RomMRomLocal
        {
            Id = 42,
            Name = "Old Name"
        };
        var replacement = new RomMRomLocal
        {
            Id = 42,
            Name = "New Name"
        };

        plugin.ImportedGames.TryAdd("42", original);

        replacement.Save(plugin.Context);

        Assert.True(plugin.ImportedGames.TryGetValue("42", out var imported));
        Assert.Same(replacement, imported);
    }

    private GravitonContextFake CreateContext()
    {
        var plugin = new GravitonContextFake();
        plugin.ConfigurePluginDataDirectory(_pluginDataDirectory);
        return plugin;
    }

    public void Dispose()
    {
        if (Directory.Exists(_pluginDataDirectory))
        {
            Directory.Delete(_pluginDataDirectory, recursive: true);
        }
    }
}
