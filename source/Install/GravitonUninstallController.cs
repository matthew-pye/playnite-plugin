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
                if (Game.InstallDirectory != null && new DirectoryInfo(Game.InstallDirectory).Exists)
                {
                    Directory.Delete(Game.InstallDirectory, true);
                }
                else
                {
                    GravitonPlugin.PlayniteApi?.Dialogs.ShowErrorMessageAsync($"\"{Game.Name}\" folder could not be found. Marking as uninstalled.", "Game not found");
                }
            }
            catch (Exception ex)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.uninstall.failed", $"Failed to delete ROM from filesystem, Marking as uninstalled - {ex.Message}", GravitonSeverity.Error, ex));
            }
            

            //Game.Roms.Clear();

            await GameUninstalledAsync(new GameUninstalledArgs());
        }
    }
}
