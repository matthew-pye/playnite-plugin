using Playnite;

using System.IO;

namespace RomMLibrary.Install.Downloads
{
    internal class RomMUninstallController : UninstallController
    {
        private readonly RomMLibraryPlugin Plugin;
        private Game Game;

        internal RomMUninstallController(Game game, RomMLibraryPlugin romM) : base("", "Uninstall", game.Id)
        {
            Plugin = romM;
            Game = game;
        }

        public override async Task UninstallAsync(UninstallActionArgs args)
        {
            if (Game.InstallDirectory != null && new DirectoryInfo(Game.InstallDirectory).Exists)
            {
                Directory.Delete(Game.InstallDirectory, true);
            }
            else
            {
                RomMLibraryPlugin.PlayniteApi?.Dialogs.ShowErrorMessageAsync($"\"{Game.Name}\" folder could not be found. Marking as uninstalled.", "Game not found");
            }
            Game.Roms.Clear();
            await GameUninstalledAsync(new GameUninstalledArgs());
        }
    }
}
