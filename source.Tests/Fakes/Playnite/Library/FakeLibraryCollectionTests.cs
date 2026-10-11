using Playnite;
using Xunit;

namespace Graviton.Tests.Fakes.Playnite.Library;

public sealed class FakeLibraryCollectionTests
{
    [Fact]
    public async Task AddAsync_StoresGame()
    {
        var playnite = new PlayniteFake();
        var game = new Game("Super Mario World");

        await playnite.Api.Library.Games.AddAsync(game);

        var stored = playnite.Api.Library.Games.Get(game.Id);

        Assert.NotNull(stored);
        Assert.Equal(game.Name, stored.Name);
        Assert.Equal(1, playnite.Api.Library.Games.Count);
    }

    [Fact]
    public void Get_ReturnsCopyOfStoredGame()
    {
        var playnite = new PlayniteFake();
        var game = new Game("Super Mario World");
        playnite.Games.Seed(game);

        var retrieved = playnite.Api.Library.Games.Get(game.Id);
        Assert.NotNull(retrieved);

        retrieved.Name = "Changed Name";

        var stored = playnite.Api.Library.Games.Get(game.Id);

        Assert.NotNull(stored);
        Assert.Equal("Super Mario World", stored.Name);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var playnite = new PlayniteFake();
        var game = new Game("Super Mario World");
        playnite.Games.Seed(game);

        var retrieved = playnite.Api.Library.Games.Get(game.Id);
        Assert.NotNull(retrieved);
        retrieved.Name = "Super Mario World 2";

        await playnite.Api.Library.Games.UpdateAsync(retrieved);

        var stored = playnite.Api.Library.Games.Get(game.Id);

        Assert.NotNull(stored);
        Assert.Equal("Super Mario World 2", stored.Name);
    }

    [Fact]
    public async Task RemoveAsync_DeletesGame()
    {
        var playnite = new PlayniteFake();
        var game = new Game("Super Mario World");
        playnite.Games.Seed(game);

        await playnite.Api.Library.Games.RemoveAsync(game.Id);

        Assert.Null(playnite.Api.Library.Games.Get(game.Id));
        Assert.Empty(playnite.Api.Library.Games);
        Assert.Equal(0, playnite.Api.Library.Games.Count);
    }

    [Fact]
    public async Task UpdateAsync_WhenCallbackReturnsFalse_DoesNotPersistChanges()
    {
        var playnite = new PlayniteFake();
        var game = new Game("Super Mario World");
        playnite.Games.Seed(game);

        var changes = await playnite.Api.Library.Games.UpdateAsync((Game item) =>
        {
            item.Name = "Changed Name";
            return false;
        });

        var stored = playnite.Api.Library.Games.Get(game.Id);

        Assert.Empty(changes);
        Assert.NotNull(stored);
        Assert.Equal("Super Mario World", stored.Name);
    }
}
