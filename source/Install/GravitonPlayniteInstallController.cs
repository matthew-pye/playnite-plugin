using Graviton.Install.Downloads;
using Graviton.Models.Install;
using Graviton.Models.ROM;
using Graviton.Models.RomM.Rom;
using Graviton.Notifications;

using Playnite;

using System.Text.Json;

namespace Graviton.Install
{

    internal class GravitonPlayniteInstallController : InstallController
    {
        private IGravitonContext _plugin;
        private IPlayniteApi _playniteAPI;
        private GravitonLogger _logger;
        private IRomMServer _romMServer;

        public GameInstallInfo GameData;
        private Game Game;

        internal GravitonPlayniteInstallController(IGravitonContext plugin, IPlayniteApi playniteAPI, GravitonLogger logger, IRomMServer server, Game game, GameInstallInfo gameData) : base(GravitonPlugin.Id, "Download", game.LibraryGameId ?? throw new Exception(Loc.GetString("InstallLibraryGameIdMissing")))
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;
            _romMServer = server;

            GameData = gameData;
            Game = game;
        }
        public override async Task InstallAsync(InstallActionArgs args)
        {
            if (GameData.Id == (int)InstallStatus.Cancelled)
            {
                await CancelInstall(Game);
                return;
            }

            var response = await _romMServer.GETAsync($"/api/roms/{GameData.Id}");
            if (response == null)
            {
                await CancelInstall(Game);
                return;
            }

            try
            {
                var rom = JsonSerializer.Deserialize<RomMRom>(response);
                if (rom == null)
                    throw new Exception("ROM is null");

                await _plugin.InstallController!.DownloadInstallROM(GameData, Game, rom, async (dir, size) =>
                {
                    await GameInstalledAsync(new()
                    {
                        InstallDirectory = dir,
                        InstallSize = size,
                    });
                });

                DownloadRequest? previousInstall = null;

                var localROM = _plugin.ImportedGames[Game.LibraryGameId!];
                await InstallUpdateDLC.RefreshCandidates(GameData.Mapping!, rom, localROM);

                if (GameData.Mapping?.UpdateInstallStyle != InstallStyles.None)
                    previousInstall = await _plugin.InstallController.UpdateDLCController.BuildUpdateDLCRequests(GameData, rom, localROM.UpdateCandidates, RomMCategory.Update, previousInstall);

                if (GameData.Mapping?.DLCInstallStyle != InstallStyles.None)
                    previousInstall = await _plugin.InstallController.UpdateDLCController.BuildUpdateDLCRequests(GameData, rom, localROM.DLCCandidates, RomMCategory.DLC, previousInstall);

                localROM.Save();
            }
            catch (Exception)
            {
                await CancelInstall(Game);
                return;
            }
  
        }

        private async Task CancelInstall(Game Game)
        {
            var game = _playniteAPI.Library.Games.Get(Game.Id) ?? throw new Exception(Loc.GetString("InstallGameDataMissing"));
            game.InstallState = InstallState.Uninstalled;
            await _playniteAPI.Library.Games.UpdateAsync(game);
        }

    }
}