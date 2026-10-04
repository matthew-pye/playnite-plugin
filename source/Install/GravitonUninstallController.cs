using Graviton.Models.Install;
using Graviton.Models.Notifications;
using Graviton.Notifications;

using Playnite;

using System.IO;

namespace Graviton.Install.Downloads
{
    internal class GravitonUninstallController : UninstallController
    {
        private GravitonPlugin _plugin { get => GravitonPlugin.Instance; }
        private IPlayniteApi _playniteAPI { get => GravitonPlugin.PlayniteApi; }
        private GravitonLogger _logger { get => GravitonPlugin.Logger; }

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
                    throw new Exception(Loc.GetString("InstallROMDataMissing"));

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
                    GravitonPlugin.PlayniteApi?.Dialogs.ShowErrorMessageAsync(Loc.GetString("GameFolderNotFound", ("GameName", Game.Name)), Loc.GetString("GameNotFoundTitle"));
                    romMLocal.InstalledPath = null;
                    romMLocal.IsInstalledPathDirectory = false;
                }

                romMLocal.Save();
            }
            catch (Exception ex)
            {
                GravitonNotify.Notify("graviton.uninstall.failed", Loc.GetString("UninstallFailed", ("Error", ex.Message)), GravitonSeverity.Error, ex);
                return;
            }

            await GameUninstalledAsync(new GameUninstalledArgs());
        }

        public static async Task UninstallCandidate(UpdateDLCCandidate candidate)
        {
            try
            {
                foreach (var path in candidate.InstalledTopPaths.ToList())
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, true);
                    }
                    else if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }

                candidate.InstalledTopPaths.Clear();
                candidate.InstalledFileIDs.Clear();
                candidate.Status = InstallStatus.NotInstalled;
                candidate.InstalledSize = 0;

            }
            catch (Exception ex)
            {
                GravitonNotify.Notify("graviton.uninstall.failed", Loc.GetString("UninstallFailed", ("Error", ex.Message)), GravitonSeverity.Error, ex);
                return;
            }


        }

    }
}
