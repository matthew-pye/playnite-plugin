using Graviton.Install.Downloads;
using Graviton.Models;
using Graviton.Models.Notifications;
using Graviton.Models.RomM.Rom;

using Playnite;

using System.IO;

namespace Graviton.Install
{
    enum InstallStatus
    {
        Cancelled = -1
    }

    internal class GravitonInstallController : InstallController
    {
        private GravitonPlugin _plugin { get => GravitonPlugin.Instance; }
        private IPlayniteApi _playniteAPI { get => GravitonPlugin.PlayniteApi; }
        private ILogger _logger { get => GravitonPlugin.Logger; }

        public GameInstallInfo GameData;

        private Game Game;

        internal GravitonInstallController(Game game, GameInstallInfo gameData) : base(GravitonPlugin.Id, "Download", game.LibraryGameId ?? throw new Exception("Game doesn't have libraryID!"))
        {
            GameData = gameData;
            Game = game;
        }

        public override async Task InstallAsync(InstallActionArgs args)
        {
            if (GameData.Id == (int)InstallStatus.Cancelled)
            {
                await CancelInstall();
                return; 
            }   

            var dstPath = GameData.Mapping?.DestinationPathResolved ?? throw new Exception("Mapped emulator data cannot be found, try removing and re-adding.");

            var installDir = GameData.InstallPath.Replace(EmulatorMapping.InstallPathToken, dstPath);

           
            // If RomM indicates multiple files, we download as an archive name (zip) into the install folder.
            // Otherwise we download the single ROM file.
            var downloadFilePath = GameData.HasMultipleFiles
                ? Path.Combine(installDir, GameData.FileName + ".zip")
                : Path.Combine(installDir, GameData.FileName);

            // Skip download if the game is already installed
            if (!GameData.HasMultipleFiles && File.Exists(downloadFilePath))
            {
                var game = _playniteAPI.Library.Games.Get(Game.Id) ?? throw new Exception("Could not get game to set as installed!");
                game.InstallState = InstallState.Installed;
                await _playniteAPI.Library.Games.UpdateAsync(game);

                await GameInstalledAsync(new()
                { 
                    InstallDirectory = installDir,
                    InstallSize = (ulong)(new FileInfo(downloadFilePath).Length),
                });
            }

            var req = new DownloadRequest
            {
                GameId = Game.Id,
                GameName = Game.Name,

                DownloadUrl = GameData.DownloadURL,
                InstallDir = installDir,
                GamePath = downloadFilePath,
                Use7z = _plugin.Settings.Use7z,
                PathTo7Z = _plugin.Settings.PathTo7z,

                HasMultipleFiles = GameData.HasMultipleFiles,
                AutoExtract = GameData.Mapping != null && GameData.Mapping.AutoExtract,

                // Callbacks into Playnite install pipeline
                OnInstalled = async installedArgs =>
                {
                    var game = _playniteAPI.Library.Games.Get(Game.Id) ?? throw new Exception("Could not get game to set as installed!");
                    game.InstallState = InstallState.Installed;
                    await _playniteAPI.Library.Games.UpdateAsync(game);

                    await GameInstalledAsync(installedArgs);
                },

                OnCancelled = async () =>
                {
                    await CancelInstall();
                },

                OnFailed = async ex =>
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.install.failed", Loc.GetString("DownloadFailed", ("GameName", Game.Name), ("Error", ex.Message)), GravitonSeverity.Error, ex));
                    var game = _playniteAPI.Library.Games.Get(Game.Id) ?? throw new Exception("Could not get game to set as installed!");
                    game.InstallState = InstallState.Uninstalled;
                    await _playniteAPI.Library.Games.UpdateAsync(game);

                    await GameInstallationCancelledAsync(new GameInstallationCancelledArgs());
                }
            };

            // Enqueue (non-blocking)
            _plugin.DownloadQueueController?.Enqueue(req);
        }

        private async Task CancelInstall()
        {
            var game = _playniteAPI.Library.Games.Get(Game.Id) ?? throw new Exception("Could not get game to set as installed!");
            game.InstallState = InstallState.Uninstalled;
            await _playniteAPI.Library.Games.UpdateAsync(game);

            await GameInstallationCancelledAsync(new GameInstallationCancelledArgs());
        }
    }
}
