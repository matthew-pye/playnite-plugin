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

        internal GravitonUninstallController(Game game) : base("", Loc.GetString("Uninstall"), game.Id)
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
                    var collidingROM = _plugin.ImportedGames.Where(x => x.Value.InstalledPath == romMLocal.InstalledPath && x.Value.PlayniteID != romMLocal.PlayniteID).ToList();

                    if (collidingROM.Count > 0)
                    {
                        var result = await GravitonPlugin.PlayniteApi.Dialogs.ShowMessageAsync(Loc.GetString("OverlappingROMInstallsWarn"), button: MessageBoxButtons.YesNoCancel, severity: MessageBoxSeverity.Warning);

                        if (result == MessageBoxResult.Yes)
                        {
                            foreach (var rom in collidingROM)
                            {
                                var game = _playniteAPI.Library.Games.FirstOrDefault(x => x.Id == rom.Value.PlayniteID);
                                if (game == null)
                                    continue;

                                game.InstallState = InstallState.Uninstalled;
                                await _playniteAPI.Library.Games.UpdateAsync(game);

                                rom.Value.InstalledPath = null;
                                rom.Value.IsInstalledPathDirectory = false;
                                rom.Value.Save();
                            }
                        }
                        else if(result == MessageBoxResult.No) { }
                        else
                        {
                            return;
                        } 
                    }

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
