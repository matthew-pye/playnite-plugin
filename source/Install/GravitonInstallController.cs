using Graviton.Install.Downloads;
using Graviton.Models;
using Graviton.Models.Install;
using Graviton.Models.Notifications;
using Graviton.Models.ROM;
using Graviton.Models.RomM.Rom;
using Graviton.Notifications;

using Playnite;

using System.IO;
using System.Text.Json;

namespace Graviton.Install
{
    public enum InstallStatus
    {
        Cancelled,
        NotInstalled,
        PartialInstalled,
        Installed,
    }

    internal class GravitonInstallController : InstallController
    {
        private GravitonPlugin _plugin { get => GravitonPlugin.Instance; }
        private IPlayniteApi _playniteAPI { get => GravitonPlugin.PlayniteApi; }
        private GravitonLogger _logger { get => GravitonPlugin.Logger; }

        private GravitonInstallUpdateDLCController UpdateDLCController;

        public GameInstallInfo GameData;

        private Game Game;

        internal GravitonInstallController(Game game, GameInstallInfo gameData) : base(GravitonPlugin.Id, "Download", game.LibraryGameId ?? throw new Exception(Loc.GetString("InstallLibraryGameIdMissing")))
        {
            GameData = gameData;
            Game = game;

            UpdateDLCController = new(game, gameData);
        }

        public override async Task InstallAsync(InstallActionArgs args)
        {
            if (GameData.Id == (int)InstallStatus.Cancelled)
            {
                await CancelInstall(Game);
                return;
            }

            var response = await GravitonPlugin.RomMServer.GETAsync($"/api/roms/{GameData.Id}");
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

                await DownloadInstallROM(rom);

                DownloadRequest? previousInstall = null;

                var localROM = _plugin.ImportedGames[Game.LibraryGameId!];
                await InstallUpdateDLC.RefreshCandidates(GameData.Mapping!, rom, localROM);

                if (GameData.Mapping?.UpdateInstallStyle != InstallStyles.None)
                    previousInstall = await UpdateDLCController.BuildUpdateDLCRequests(rom, localROM.UpdateCandidates, RomMCategory.Update, previousInstall);

                if (GameData.Mapping?.DLCInstallStyle != InstallStyles.None)
                    previousInstall = await UpdateDLCController.BuildUpdateDLCRequests(rom, localROM.DLCCandidates, RomMCategory.DLC, previousInstall);

                localROM.Save();
            }
            catch (Exception)
            {
                await CancelInstall(Game);
                return;
            }
  
        }

        private static async Task CancelInstall(Game Game)
        {
            var game = GravitonPlugin.PlayniteApi.Library.Games.Get(Game.Id) ?? throw new Exception(Loc.GetString("InstallGameDataMissing"));
            game.InstallState = InstallState.Uninstalled;
            await GravitonPlugin.PlayniteApi.Library.Games.UpdateAsync(game);
        }

        private async Task DownloadInstallROM(RomMRom ROM)
        {
            var dstPath = GameData.Mapping?.DestinationPathResolved ?? throw new Exception(Loc.GetString("InstallMappingDataMissing"));

            var installDir = GameData.InstallPath.Replace(EmulatorMapping.InstallPathToken, dstPath);

            var tempDir = Path.Combine(_plugin.PluginDataPath, "temp", Game.Id.ToString());
            var tempPath = Path.Combine(tempDir, (GameData.HasMultipleFiles ? GameData.FileName + ".zip" : GameData.FileName));

            //TODO - Use this to check if already installed ROM needed downloading
            var filetree = RomMFileTree.Build(ROM.FileSystemPath ?? "", ROM.Files.Where(x => x.Category == "game").ToList());

            // Skip download if the game is already installed
            if (!GameData.HasMultipleFiles && File.Exists(Path.Combine(installDir, GameData.FileName)))
            {
                var game = _playniteAPI.Library.Games.Get(Game.Id) ?? throw new Exception(Loc.GetString("InstallGameDataMissing"));
                _plugin.ImportedGames.TryGetValue(game.LibraryGameId ?? "", out var romMLocal);
                if (romMLocal == null)
                    throw new Exception(Loc.GetString("InstallROMDataMissing"));

                if (installDir.CompareTo(dstPath, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    romMLocal.InstalledPath = Path.Combine(installDir, GameData.FileName);
                    romMLocal.IsInstalledPathDirectory = false;
                }
                else
                {
                    romMLocal.InstalledPath = installDir;
                    romMLocal.IsInstalledPathDirectory = true;
                }
                romMLocal.Save();

                game.InstallState = InstallState.Installed;
                await _playniteAPI.Library.Games.UpdateAsync(game);

                await GameInstalledAsync(new()
                {
                    InstallDirectory = installDir,
                    InstallSize = (ulong)(new FileInfo(Path.Combine(installDir, GameData.FileName)).Length),
                });

                return;
            }

            await CreateBaseGameInstallRequest(GameData, tempPath, installDir, Game);
        }

        public static async Task CreateBaseGameInstallRequest(GameInstallInfo GameData, string tempPath, string installDir, Game Game)
        {
            var req = new DownloadRequest
            {
                Id = GameData.Id.ToString(),
                DisplayName = Game.Name,

                DownloadUrl = GameData.DownloadURL,
                DownloadPath = tempPath,

                OnDownloadComplete = async (item, req) =>
                {

                    GravitonPlugin.Instance.ImportedGames.TryGetValue(Game.LibraryGameId ?? "", out var romMLocal);
                    if (romMLocal == null)
                        throw new Exception(Loc.GetString("InstallROMDataMissing"));

                    Directory.CreateDirectory(installDir);

                    // Extract if needed (we treat extract as 0..100 in its own bar)
                    // This check may need changing in the case where a user has multiple archive files 
                    if (romMLocal.HasMultipleFiles || (GameData.Mapping!.AutoExtract && ArchiveExtractor.IsFileCompressed(req.DownloadPath)))
                    {

                        item.SetStatus(DownloadStatus.Extracting, Loc.GetString("DownloadStatusExtracting"));
                        GravitonPlugin.Logger?.Info($"Extracting {req.DownloadPath}...");

                        if (GravitonPlugin.Instance.Settings.Use7z && !string.IsNullOrEmpty(GravitonPlugin.Instance.Settings.PathTo7z) && GravitonPlugin.Instance.Settings.PathTo7z.EndsWith("7z.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            await ArchiveExtractor.ExtractArchiveWith7z(GravitonPlugin.Instance.Settings.PathTo7z, req.DownloadPath, installDir, item, item.Cts.Token);
                        }
                        else
                        {
                            ArchiveExtractor.ExtractArchiveWithEntryProgress(req.DownloadPath, installDir, item, item.Cts.Token);
                        }
                        try { File.Delete(req.DownloadPath); } catch { }

                        romMLocal.InstalledPath = installDir;
                        romMLocal.IsInstalledPathDirectory = true;

                        Game.InstallState = InstallState.Installed;
                         Game.InstallSize = 0;
                        Directory.GetFiles(installDir!).Select(x => (ulong)(new FileInfo(x).Length)).ForEach(y => Game.InstallSize += y);
                        Game.InstallDirectory = installDir;
                    }
                    else if (File.Exists(req.DownloadPath))
                    {
                        var installedPath = Path.Combine(installDir, Path.GetFileName(req.DownloadPath));

                        GravitonInstallHelpers.CopyFileWithProgress(req.DownloadPath, installedPath, item, item.Cts.Token);

                        romMLocal.InstalledPath = installedPath;
                        romMLocal.IsInstalledPathDirectory = false;

                        Game.InstallState = InstallState.Installed;
                        Game.InstallSize = (ulong)(new FileInfo(installedPath).Length);
                        Game.InstallDirectory = installDir;
                    }

                    romMLocal.Save();
                    
                    await GravitonPlugin.PlayniteApi.Library.Games.UpdateAsync(Game);

                    if (File.Exists(req.DownloadPath))
                        File.Delete(req.DownloadPath);

                },

                OnCancelled = async () =>
                {
                    await CancelInstall(Game);
                },

                OnFailed = async ex =>
                {
                    GravitonNotify.Notify("graviton.install.failed", Loc.GetString("DownloadFailed", ("GameName", Game.Name), ("Error", ex.Message)), GravitonSeverity.Error, ex);
                    var game = GravitonPlugin.PlayniteApi.Library.Games.Get(Game.Id) ?? throw new Exception(Loc.GetString("InstallGameDataMissing"));
                    game.InstallState = InstallState.Uninstalled;
                    await GravitonPlugin.PlayniteApi.Library.Games.UpdateAsync(game);
                }
            };

            DownloadRequestBackup backup = new()
            {
                InstallType = InstallType.BaseGame,
                ID = req.Id,
                GameID = GameData.Id.ToString(),
                DownloadPath = tempPath,
                InstallDir = installDir
            };
            GravitonPlugin.Instance.DownloadQueueController?.Enqueue(req, backup);
        }

        public static async Task RecoverBaseGameDownload(DownloadRequestBackup request)
        {
            if (!GravitonPlugin.Instance.ImportedGames.ContainsKey(request.GameID))
                throw new Exception(Loc.GetString("InstallGameIdNotFound", ("GameID", request.GameID ?? "")));

            var localROM = GravitonPlugin.Instance.ImportedGames[request.GameID];
            var mapping = GravitonPlugin.Instance.Settings.Mappings.FirstOrDefault(x => x.MappingId == localROM.MappingID);

            if (mapping == null)
                throw new Exception(Loc.GetString("InstallMappingNotFound"));

            var installInfo = GameInstallInfo.Build(localROM, mapping);
            GravitonPlugin.Logger?.Trace($"Created install info\n{JsonSerializer.Serialize(installInfo, new JsonSerializerOptions { WriteIndented = true })}");

            var game = GravitonPlugin.PlayniteApi.Library.Games.Get(localROM.PlayniteID ?? "") ?? throw new Exception(Loc.GetString("InstallGameDataMissing"));

            await CreateBaseGameInstallRequest(installInfo, request.DownloadPath, request.InstallDir, game);
        }
        public static async Task RecoverUpdateDLCInstall(List<DownloadRequestBackup> requests)
        {
            await GravitonInstallUpdateDLCController.RecoverUpdateDLCInstall(requests);
        }

    }
}