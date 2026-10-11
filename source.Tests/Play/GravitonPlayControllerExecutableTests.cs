using Graviton.Tests.Fakes.Play;

using Xunit;

namespace Graviton.Tests.Play;

[Collection("GravitonPlayLifecycle")]
public sealed class GravitonPlayControllerExecutableTests
{
    [Fact]
    public void FindEmulatorExecutable_WhenInstallDirectoryIsAnExecutable_ReturnsThatFile()
    {
        using var test = new PlayTestHarness();
        var executable = test.WriteGameFile("emulator.exe");

        var result = test.Controller.FindEmulatorExecutable(executable, null!);

        Assert.Equal(executable, result);
    }

    [Fact]
    public void FindEmulatorExecutable_WhenInstallDirectoryIsMissing_ReturnsNull()
    {
        using var test = new PlayTestHarness();

        var result = test.Controller.FindEmulatorExecutable(string.Empty, null!);

        Assert.Null(result);
    }

    [Fact]
    public void FindEmulatorExecutable_WhenOverrideIsAbsolutePath_ReturnsOverride()
    {
        using var test = new PlayTestHarness();
        var overrideExecutable = test.WriteGameFile("custom.exe");

        var result = test.Controller.FindEmulatorExecutable(test.Root, null!, overrideExecutable);

        Assert.Equal(overrideExecutable, result);
    }

    [Fact]
    public void FindEmulatorExecutable_WhenOverrideIsFilenameRegex_MatchesIgnoringCase()
    {
        using var test = new PlayTestHarness();
        var executable = test.WriteGameFile("DuckStation.EXE");
        test.WriteGameFile("other.txt");

        var result = test.Controller.FindEmulatorExecutable(test.Root, null!, "^duckstation\\.exe$");

        Assert.Equal(executable, result);
    }

    [Fact]
    public void FindEmulatorExecutable_WhenOverrideDoesNotMatch_ReturnsNull()
    {
        using var test = new PlayTestHarness();
        test.WriteGameFile("other.exe");

        var result = test.Controller.FindEmulatorExecutable(test.Root, null!, "^missing\\.exe$");

        Assert.Null(result);
    }

    [Fact]
    public void FindEmulatorExecutable_OverrideOnlyScansInstallationRoot()
    {
        using var test = new PlayTestHarness();
        test.WriteGameFile("Nested/emulator.exe");

        var result = test.Controller.FindEmulatorExecutable(test.Root, null!, "^emulator\\.exe$");

        Assert.Null(result);
    }
}
