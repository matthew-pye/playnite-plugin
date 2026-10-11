using Graviton.Tests.Fakes.Play;

using Playnite;

using Xunit;

namespace Graviton.Tests.Play;

[Collection("GravitonPlayLifecycle")]
public sealed class GameSessionHandlerLifecycleTests
{
    [Fact]
    public async Task GameStarting_WithoutLibraryGameID_CancelsStartup()
    {
        using var test = new PlayTestHarness();
        var game = test.AddGame(gameID: "");
        var args = test.Starting(game);

        await test.Session.GameStarting(args);

        Assert.True(args.CancelStartup);
        Assert.False(test.Session.IsAGameRunning);
    }

    [Fact]
    public async Task GameStarting_WhenGameIsNotImported_CancelsStartup()
    {
        using var test = new PlayTestHarness();
        var args = test.Starting(test.AddGame());

        await test.Session.GameStarting(args);

        Assert.True(args.CancelStartup);
        Assert.False(test.Session.IsAGameRunning);
        Assert.Equal("42", test.Session.GameID);
    }

    [Fact]
    public async Task GameStarting_WhenMappingIsMissing_CancelsStartup()
    {
        using var test = new PlayTestHarness();
        var mapping = test.AddMapping();
        test.AddROM(mapping);
        test.Plugin.Settings.Mappings.Clear();
        var args = test.Starting(test.AddGame());

        await test.Session.GameStarting(args);

        Assert.True(args.CancelStartup);
        Assert.False(test.Session.IsAGameRunning);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task GameStarting_WhenRequiredLaunchPropertiesAreMissing_CancelsStartup(bool includeImagePath, bool includeEmulatorDir)
    {
        using var test = new PlayTestHarness();
        var mapping = test.AddMapping();
        test.AddROM(mapping);
        var args = test.Starting(test.AddGame(), hasImagePath: includeImagePath, hasEmulatorDir: includeEmulatorDir);

        await test.Session.GameStarting(args);

        Assert.True(args.CancelStartup);
        Assert.False(test.Session.IsAGameRunning);
    }

    [Fact]
    public async Task GameStarting_WithValidROMAndProperties_AllowsStartup()
    {
        using var test = new PlayTestHarness();
        var mapping = test.AddMapping();
        test.AddROM(mapping);
        var args = test.Starting(test.AddGame());

        await test.Session.GameStarting(args);

        Assert.False(args.CancelStartup);
        Assert.True(test.Session.IsAGameRunning);
        Assert.Equal("42", test.Session.GameID);
    }

    [Fact]
    public async Task GameStarting_WhileGameIsAlreadyRunning_CancelsSecondStartupWithoutChangingActiveID()
    {
        using var test = new PlayTestHarness();
        var mapping = test.AddMapping();
        test.AddROM(mapping);
        var original = test.Starting(test.AddGame());
        await test.Session.GameStarting(original);

        var second = test.Starting(test.AddGame("43"));
        await test.Session.GameStarting(second);

        Assert.True(second.CancelStartup);
        Assert.True(test.Session.IsAGameRunning);
        Assert.Equal("42", test.Session.GameID);

        await test.Session.GameStopped(test.Stopped(original));
        Assert.False(test.Session.IsAGameRunning);
    }

    [Fact]
    public async Task GameStarted_WithoutPriorStartup_DoesNotMarkGameRunning()
    {
        using var test = new PlayTestHarness();
        var args = test.Starting(test.AddGame());

        await test.Session.GameStarted(test.Started(args));

        Assert.False(test.Session.IsAGameRunning);
        Assert.Null(test.Session.GameID);
    }

    [Fact]
    public async Task GameStopped_WithoutPriorStartup_DoesNothing()
    {
        using var test = new PlayTestHarness();
        var args = test.Starting(test.AddGame());

        await test.Session.GameStopped(test.Stopped(args));

        Assert.False(test.Session.IsAGameRunning);
    }

    [Fact]
    public async Task GameStarted_ForDifferentGame_DoesNotInterruptCurrentGame()
    {
        using var test = new PlayTestHarness();
        var mapping = test.AddMapping();
        test.AddROM(mapping);
        await test.Session.GameStarting(test.Starting(test.AddGame()));

        await test.Session.GameStarted(test.Started(test.Starting(test.AddGame("43"))));

        Assert.True(test.Session.IsAGameRunning);
        Assert.Equal("42", test.Session.GameID);
    }

    [Fact]
    public async Task GameStopped_ForDifferentGame_DoesNotInterruptCurrentGame()
    {
        using var test = new PlayTestHarness();
        var mapping = test.AddMapping();
        test.AddROM(mapping);
        var original = test.Starting(test.AddGame());
        await test.Session.GameStarting(original);

        await test.Session.GameStopped(test.Stopped(test.Starting(test.AddGame("43"))));

        Assert.True(test.Session.IsAGameRunning);
        Assert.Equal("42", test.Session.GameID);
    }

    [Fact]
    public async Task GameStopped_ForCurrentGame_ClearsRunningState()
    {
        using var test = new PlayTestHarness();
        var mapping = test.AddMapping();
        test.AddROM(mapping);
        var args = test.Starting(test.AddGame());
        await test.Session.GameStarting(args);

        await test.Session.GameStopped(test.Stopped(args));

        Assert.False(test.Session.IsAGameRunning);
    }

    [Fact]
    public async Task GameStopped_Twice_DoesNotReactivateSession()
    {
        using var test = new PlayTestHarness();
        var mapping = test.AddMapping();
        test.AddROM(mapping);
        var args = test.Starting(test.AddGame());
        await test.Session.GameStarting(args);
        await test.Session.GameStopped(test.Stopped(args));

        await test.Session.GameStopped(test.Stopped(args));

        Assert.False(test.Session.IsAGameRunning);
    }

    [Fact]
    public async Task GameCancelled_ClearsRunningStateAndAllowsLaterStartup()
    {
        using var test = new PlayTestHarness();
        var mapping = test.AddMapping();
        test.AddROM(mapping);
        var first = test.Starting(test.AddGame());
        await test.Session.GameStarting(first);

        await test.Session.GameCancelled(new Plugin.OnGameStartupCancelledEventArgs(first));

        Assert.False(test.Session.IsAGameRunning);

        var retry = test.Starting(test.AddGame());
        await test.Session.GameStarting(retry);
        Assert.False(retry.CancelStartup);
        Assert.True(test.Session.IsAGameRunning);
    }
}
