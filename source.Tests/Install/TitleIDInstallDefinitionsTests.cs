using Graviton.Models.Install;
using Graviton.Tests.Fakes.Install;

using Xunit;

namespace Graviton.Tests.Install;

[Collection("GravitonUpdateDLC")]
public sealed class TitleIDInstallDefinitionsTests
{
    [Fact]
    public void Initialize_WhenCalledRepeatedly_DoesNotDuplicateDefinitions()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        TitleIDInstallDefinitions.Initialize();
        var original = TitleIDInstallDefinitions.All.ToList();

        TitleIDInstallDefinitions.Initialize();

        Assert.Equal(original.Count, TitleIDInstallDefinitions.All.Count);
        Assert.Equal(original, TitleIDInstallDefinitions.All);
        Assert.Equal(original.Count, original.Select(x => x.ID).Distinct().Count());
    }

    [Fact]
    public void Get_NullID_ResolvesDefaultFolderTemplate()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        TitleIDInstallDefinitions.Initialize();

        var definition = TitleIDInstallDefinitions.Get(null);

        Assert.NotNull(definition);
        Assert.Null(definition.ID);
        Assert.Contains("{FolderPath}", definition.RuntimeArgs);
        Assert.Contains("{TitleID}", definition.RuntimeArgs);
    }

    [Theory]
    [InlineData("CEMU.UPDATE", "cemu.update")]
    [InlineData("Cemu.DLC", "cemu.dlc")]
    [InlineData("XENIA.TUUPDATE", "xenia.tuupdate")]
    [InlineData("SHADPS4.DLC", "shadps4.dlc")]
    public void Get_MatchesDefinitionIDWithoutCaseSensitivity(string searchID, string expectedID)
    {
        using var harness = new UpdateDLCInstallTestHarness();
        TitleIDInstallDefinitions.Initialize();

        Assert.Equal(expectedID, TitleIDInstallDefinitions.Get(searchID)?.ID);
    }

    [Fact]
    public void Get_UnknownID_ReturnsNull()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        TitleIDInstallDefinitions.Initialize();

        Assert.Null(TitleIDInstallDefinitions.Get("not-an-emulator-layout"));
    }

    [Theory]
    [InlineData("cemu", "cemu.update", "cemu.dlc")]
    [InlineData("xenia_edge", "xenia.tuupdate", null)]
    [InlineData("shadps4", "shadps4.update", "shadps4.dlc")]
    public void Find_ByEmulatorID_OffersApplicableLayouts(string emulatorID, string firstID, string? secondID)
    {
        using var harness = new UpdateDLCInstallTestHarness();
        TitleIDInstallDefinitions.Initialize();

        var found = TitleIDInstallDefinitions.Find(EmulatorID: emulatorID);

        Assert.NotNull(found);
        Assert.Contains(found, x => x.ID == firstID);
        if (secondID != null)
            Assert.Contains(found, x => x.ID == secondID);
        Assert.DoesNotContain(found, x => x.ID == "not-an-emulator-layout");
    }

    [Theory]
    [InlineData("CEMU.EXE", "cemu.update")]
    [InlineData("xenia_canary.exe", "xenia.tuupdate")]
    [InlineData("shadPS4QtLauncher.exe", "shadps4.dlc")]
    public void Find_ByExecutableName_OffersApplicableLayouts(string executable, string expectedID)
    {
        using var harness = new UpdateDLCInstallTestHarness();
        TitleIDInstallDefinitions.Initialize();

        var found = TitleIDInstallDefinitions.Find(ExecutableName: executable);

        Assert.NotNull(found);
        Assert.Contains(found, x => x.ID == expectedID);
    }

    [Fact]
    public void Find_WithNoSelector_ReturnsNull()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        TitleIDInstallDefinitions.Initialize();

        Assert.Null(TitleIDInstallDefinitions.Find());
    }

    [Fact]
    public void Find_WithUnknownSelectors_ReturnsEmptyCollection()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        TitleIDInstallDefinitions.Initialize();

        var result = TitleIDInstallDefinitions.Find(EmulatorID: "unknown", ExecutableName: "unknown.exe");

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Mapping_UpdateAndDLCDefinitionsRemainIndependent()
    {
        using var harness = new UpdateDLCInstallTestHarness();
        TitleIDInstallDefinitions.Initialize();
        var update = TitleIDInstallDefinitions.Get("cemu.update");
        var dlc = TitleIDInstallDefinitions.Get("cemu.dlc");

        harness.Mapping.UpdateTitleIDDefinition = update;
        harness.Mapping.DLCTitleIDDefinition = dlc;

        Assert.Equal("cemu.update", harness.Mapping.UpdateTitleIDDefinitionID);
        Assert.Equal("cemu.dlc", harness.Mapping.DLCTitleIDDefinitionID);
        Assert.Same(update, harness.Mapping.UpdateTitleIDDefinition);
        Assert.Same(dlc, harness.Mapping.DLCTitleIDDefinition);
    }
}
