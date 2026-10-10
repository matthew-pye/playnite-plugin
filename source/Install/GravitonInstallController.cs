using ExCSS;

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

    public class GravitonInstallController
    {
        private IGravitonContext _plugin;
        private IPlayniteApi _playniteAPI;
        private GravitonLogger _logger;
        private IRomMServer _romMServer;

        public GravitonInstallUpdateDLCController UpdateDLCController;


        internal GravitonInstallController(IGravitonContext plugin, IPlayniteApi playniteAPI, GravitonLogger logger, IRomMServer server)
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;
            _romMServer = server;

            UpdateDLCController = new(plugin, playniteAPI, logger, server);
        }

        internal async Task RecoverDownloads()
        {
            // Recover downloads after playnite exit / crash
            var backupDir = Path.Combine(GravitonPlugin.Instance.PluginDataPath, "temp", "downloads");
            if (Directory.Exists(backupDir))
            {
                GravitonPlugin.Logger.Trace("Download backup directory found!");

                var downloadRequests = ReadDownloadBackups(backupDir,
                    file =>
                    {
                        GravitonPlugin.Logger.Trace($"Successfully deseralized {Path.GetFileName(file)}");
                    },
                    (file, exception) =>
                    {
                        GravitonPlugin.Logger.Error($"Failed to restore download request for {Path.GetFileName(file)}: {exception}");
                    });

                try
                {
                    if (downloadRequests.Any(x => x.InstallType == InstallType.UpdateDLC))
                        await RecoverUpdateDLCInstall(downloadRequests.Where(x => x.InstallType == InstallType.UpdateDLC).ToList());
                }
                catch (Exception ex)
                {
                    GravitonPlugin.Logger.Error($"Failed to restore download request for Updates/DLCs: {ex}");
                }

                foreach (var request in downloadRequests.Where(x => x.InstallType != InstallType.UpdateDLC))
                {
                    GravitonPlugin.Logger.Trace($"Trying to restart download for {request.ID}");

                    try
                    {
                        if (request.InstallType == InstallType.BaseGame)
                            await RecoverBaseGameDownload(request);
                        else if (request.InstallType == InstallType.Remote)
                            await GravitonRemoteInstallController.RestoreDownloadRequest(request);
                    }
                    catch (Exception ex)
                    {
                        GravitonPlugin.Logger.Error($"Failed to restore download request for {request.ID}: {ex}");
                    }
                }
            }
            else
            {
                Directory.CreateDirectory(backupDir);
            }
        }

        internal List<DownloadRequestBackup> ReadDownloadBackups(string backupDir, Action<string>? onLoaded = null, Action<string, Exception>? onFailed = null)
        {
            List<DownloadRequestBackup> requests = new();

            foreach (var file in Directory.GetFiles(backupDir, "*.tmp"))
            {
                try
                {
                    var request = JsonSerializer.Deserialize<DownloadRequestBackup>(File.ReadAllText(file));

                    if (request == null)
                    {
                        File.Delete(file);
                        continue;
                    }

                    requests.Add(request);
                    onLoaded?.Invoke(file);
                }
                catch (Exception ex)
                {
                    onFailed?.Invoke(file, ex);
                }
            }

            return requests;
        }

        // Returns true if the game is already installed
        internal async Task DownloadInstallROM(GameInstallInfo GameData, Game game, RomMRom ROM, Func<string, ulong, Task>? onInstalledCallback = null)
        {
            var dstPath = GameData.Mapping?.DestinationPathResolved ?? throw new Exception(Loc.GetString("InstallMappingDataMissing"));

            var installDir = GameData.InstallPath.Replace(EmulatorMapping.InstallPathToken, dstPath);

            var tempDir = Path.Combine(_plugin.PluginDataPath, "temp", game.Id.ToString());
            var tempPath = Path.Combine(tempDir, (GameData.HasMultipleFiles ? GameData.FileName + ".zip" : GameData.FileName));

            //TODO - Use this to check if already installed ROM needed downloading
            var filetree = RomMFileTree.Build(ROM.FileSystemPath ?? "", ROM.Files.Where(x => x.Category == "game").ToList());

            // Skip download if the game is already installed
            if (!GameData.HasMultipleFiles && File.Exists(Path.Combine(installDir, GameData.FileName)))
            {
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

                if (onInstalledCallback != null)
                {
                    await onInstalledCallback.Invoke(installDir, (ulong)(new FileInfo(Path.Combine(installDir, GameData.FileName)).Length));
                }
                else
                {
                    game.InstallState = InstallState.Installed;
                    await _playniteAPI.Library.Games.UpdateAsync(game);
                }

                return;
            }

            await CreateBaseGameInstallRequest(GameData, tempPath, installDir, game, onInstalledCallback);
        }

        internal async Task CreateBaseGameInstallRequest(GameInstallInfo GameData, string tempPath, string installDir, Game Game, Func<string, ulong, Task>? onInstalledCallback = null)
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

                    string finalDir = "";
                    ulong installSize = 0;

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

                        Directory.GetFiles(installDir!).Select(x => (ulong)(new FileInfo(x).Length)).ForEach(y => installSize += y);
                        finalDir = installDir;
                    }
                    else if (File.Exists(req.DownloadPath))
                    {
                        var installedPath = Path.Combine(installDir, Path.GetFileName(req.DownloadPath));

                        GravitonInstallHelpers.CopyFileWithProgress(req.DownloadPath, installedPath, item, item.Cts.Token);

                        romMLocal.InstalledPath = installedPath;
                        romMLocal.IsInstalledPathDirectory = false;

                        installSize = (ulong)(new FileInfo(installedPath).Length);
                        finalDir = installDir;
                    }

                    if(onInstalledCallback != null)
                    {
                        await onInstalledCallback.Invoke(finalDir, installSize);
                    }
                    else
                    {
                        romMLocal.Save();

                        Game.InstallState = InstallState.Installed;
                        Game.InstallSize = installSize;
                        Game.InstallDirectory = finalDir;

                        await GravitonPlugin.PlayniteApi.Library.Games.UpdateAsync(Game);
                    }

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

        internal async Task RecoverBaseGameDownload(DownloadRequestBackup request)
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
        internal async Task RecoverUpdateDLCInstall(List<DownloadRequestBackup> requests)
        {
            await UpdateDLCController.RecoverUpdateDLCInstall(requests);
        }


        private async Task CancelInstall(Game Game)
        {
            var game = GravitonPlugin.PlayniteApi.Library.Games.Get(Game.Id) ?? throw new Exception(Loc.GetString("InstallGameDataMissing"));
            game.InstallState = InstallState.Uninstalled;
            await GravitonPlugin.PlayniteApi.Library.Games.UpdateAsync(game);
        }
    }
}