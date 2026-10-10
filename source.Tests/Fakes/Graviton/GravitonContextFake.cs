using Graviton.Models.ROM;
using Graviton.Settings;

using NSubstitute;

using System.Collections.Concurrent;

namespace Graviton.Tests.Fakes.Graviton;

internal sealed class GravitonContextFake
{
    private string? _pluginDataDirectory;

    public IGravitonContext Context { get; } = Substitute.For<IGravitonContext>();
    public GravitonPluginSettings Settings { get; } = new();
    public ConcurrentDictionary<string, RomMRomLocal> ImportedGames { get; } = new();

    public GravitonContextFake()
    {
        Context.PluginDataPath.Returns(call =>
        {
            return _pluginDataDirectory ?? throw new InvalidOperationException("No plugin data directory has been configured.");
        });

        Context.Settings.Returns(Settings);
        Context.ImportedGames.Returns(ImportedGames);
    }

    public void ConfigurePluginDataDirectory(string path)
    {
        _pluginDataDirectory = path;
    }
}
