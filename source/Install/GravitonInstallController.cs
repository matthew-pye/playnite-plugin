using Graviton.Install.Downloads;
using Graviton.Models;
using Graviton.Models.Notifications;
using Graviton.Models.RomM.Rom;
using Graviton.Notifications;

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
        private GravitonLogger _logger { get => GravitonPlugin.Logger; }

        public GameInstallInfo GameData;

        private Game Game;

        internal GravitonInstallController(Game game, GameInstallInfo gameData) : base(GravitonPlugin.Id, "Download", game.LibraryGameId ?? throw new Exception(Loc.GetString("InstallLibraryGameIdMissing")))
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

            await DownloadInstallROM();
        }

        private async Task DownloadInstallROM()
        {
            var dstPath = GameData.Mapping?.DestinationPathResolved ?? throw new Exception(Loc.GetString("InstallMappingDataMissing"));

            var installDir = GameData.InstallPath.Replace(EmulatorMapping.InstallPathToken, dstPath);

            var tempDir = Path.Combine(_plugin.PluginDataPath, "temp", Game.Id.ToString());
            var tempPath = Path.Combine(tempDir, (GameData.HasMultipleFiles ? GameData.FileName + ".zip" : GameData.FileName));

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

            var req = new DownloadRequest
            {
                Id = Game.Id,
                DisplayName = Game.Name,

                DownloadUrl = GameData.DownloadURL,
                DownloadPath = tempPath,

                OnDownloadComplete = async (item, req) =>
                {

                    var game = _playniteAPI.Library.Games.Get(req.Id) ?? throw new Exception(Loc.GetString("InstallROMDataMissing"));
                    _plugin.ImportedGames.TryGetValue(game.LibraryGameId ?? "", out var romMLocal);
                    if (romMLocal == null)
                        throw new Exception(Loc.GetString("InstallROMDataMissing"));

                    Directory.CreateDirectory(installDir);

                    // Extract if needed (we treat extract as 0..100 in its own bar)
                    // This check may need changing in the case where a user has multiple archive files 
                    if (romMLocal.HasMultipleFiles || (GameData.Mapping.AutoExtract && ArchiveExtractor.IsFileCompressed(req.DownloadPath)))
                    {

                        item.SetStatus(DownloadStatus.Extracting, Loc.GetString("DownloadStatusExtracting"));
                        _logger?.Info($"Extracting {req.DownloadPath}...");

                        if (_plugin.Settings.Use7z && !string.IsNullOrEmpty(_plugin.Settings.PathTo7z) && _plugin.Settings.PathTo7z.EndsWith("7z.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            ArchiveExtractor.ExtractArchiveWith7z(_plugin.Settings.PathTo7z, req.DownloadPath, installDir, item, item.Cts.Token);
                        }
                        else
                        {
                            ArchiveExtractor.ExtractArchiveWithEntryProgress(req.DownloadPath, installDir, item, item.Cts.Token);
                        }
                        try { File.Delete(req.DownloadPath); } catch { }

                        romMLocal.InstalledPath = installDir;
                        romMLocal.IsInstalledPathDirectory = true;
                    }
                    else if (File.Exists(req.DownloadPath))
                    {
                        var installedPath = Path.Combine(installDir, Path.GetFileName(req.DownloadPath));

                        File.Copy(req.DownloadPath, installedPath, true);

                        romMLocal.InstalledPath = installedPath;
                        romMLocal.IsInstalledPathDirectory = false;
                    }

                    romMLocal.Save();
                    game.InstallState = InstallState.Installed;
                    await _playniteAPI.Library.Games.UpdateAsync(game);

                    if (File.Exists(req.DownloadPath))
                        File.Delete(req.DownloadPath);

                    await GameInstalledAsync(new()
                    {
                        InstallDirectory = romMLocal.InstalledPath,
                    });

                },

                OnCancelled = async () =>
                {
                    await CancelInstall();
                },

                OnFailed = async ex =>
                {
                    GravitonNotify.Notify("graviton.install.failed", Loc.GetString("DownloadFailed", ("GameName", Game.Name), ("Error", ex.Message)), GravitonSeverity.Error, ex);
                    var game = _playniteAPI.Library.Games.Get(Game.Id) ?? throw new Exception(Loc.GetString("InstallGameDataMissing"));
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
            var game = _playniteAPI.Library.Games.Get(Game.Id) ?? throw new Exception(Loc.GetString("InstallGameDataMissing"));
            game.InstallState = InstallState.Uninstalled;
            await _playniteAPI.Library.Games.UpdateAsync(game);

            await GameInstallationCancelledAsync(new GameInstallationCancelledArgs());
        }
    }
}
