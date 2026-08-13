using Graviton.Models.Notifications;

using Playnite;

using System.IO;

namespace Graviton.Install.Downloads
{
    internal class GravitonUninstallController : UninstallController
    {
        private GravitonPlugin _plugin { get => GravitonPlugin.Instance; }
        private IPlayniteApi _playniteAPI { get => GravitonPlugin.PlayniteApi; }
        private ILogger _logger { get => GravitonPlugin.Logger; }

        private Game Game;

        internal GravitonUninstallController(Game game) : base("", "Uninstall", game.Id)
        {
            Game = game;
        }

        public override async Task UninstallAsync(UninstallActionArgs args)
        {
            try
            {
                _plugin.ImportedGames.TryGetValue(Game.LibraryGameId ?? "", out var romMLocal);
                if (romMLocal == null || romMLocal.InstalledPath == null)
                    throw new Exception("Could not get game to be installed!");

                if (romMLocal.IsInstalledPathDirectory && Directory.Exists(romMLocal.InstalledPath))
                {
                    Directory.Delete(romMLocal.InstalledPath, true);
                    romMLocal.InstalledPath = null;
                    romMLocal.IsInstalledPathDirectory = false;
                }
                else if(!romMLocal.IsInstalledPathDirectory && File.Exists(romMLocal.InstalledPath))
                {
                    File.Delete(romMLocal.InstalledPath);
                    romMLocal.InstalledPath = null;
                }
                else
                {
                    GravitonPlugin.PlayniteApi?.Dialogs.ShowErrorMessageAsync($"\"{Game.Name}\" folder could not be found. Marking as uninstalled.", "Game not found");
                    romMLocal.InstalledPath = null;
                    romMLocal.IsInstalledPathDirectory = false;
                }

                romMLocal.Save();
            }
            catch (Exception ex)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.uninstall.failed", Loc.GetString("UninstallFailed", ("Error", ex.Message)), GravitonSeverity.Error, ex));
                return;
            }
            

            //Game.Roms.Clear();

            await GameUninstalledAsync(new GameUninstalledArgs());
        }
    }
}
