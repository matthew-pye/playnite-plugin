using Graviton.Install.Downloads;
using Graviton.Models;
using Graviton.Models.Install;
using Graviton.Models.Notifications;
using Graviton.Models.ROM;
using Graviton.Models.RomM.Install;
using Graviton.Models.RomM.Rom;
using Graviton.Notifications;

using Playnite;

using SocketIOClient;

using System.IO;
using System.Text;
using System.Text.Json;


namespace Graviton.Install
{
    internal class GravitonRemoteInstallController
    {
        private GravitonPlugin _plugin;
        private IPlayniteApi _playniteAPI;
        private GravitonLogger _logger;
        private IRomMServer _romMServer;

        private readonly SocketIO _socket;
        private readonly SemaphoreSlim _installLock = new(1, 1);

        private bool _socketDisconnected = true;
        private CancellationTokenSource? _installHeartbeatCts;
        private Task? _installHeartbeatTask;

        public static readonly HashSet<string> _remoteCancelledIds = new();

        public GravitonRemoteInstallController(GravitonPlugin plugin, IPlayniteApi playniteAPI, GravitonLogger logger, IRomMServer server)
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;
            _romMServer = server;

            _installHeartbeatCts = new();

            _socket = new SocketIO(new Uri($"{plugin.Settings.Host}/devices"), new SocketIOOptions
            {
                Path = "/ws/socket.io",
                Auth = new { token = plugin.Settings.ClientTokenNP },
                Reconnection = false,
                ConnectionTimeout = TimeSpan.FromSeconds(10)
            });

            _socket.On("install:queued", async _ =>
            {
                _logger.Trace("Socket recieved install:queued");

                try
                {
                   await InstallQueued();
                }
                catch (Exception ex)
                {
                    _logger.Error($"Remote install cancellation failed: {ex}");
                }
            });

            _socket.On("install:cancelled", async response =>
            {
                _logger.Trace("Socket recieved install:cancelled");
                try
                {
                    var installResponse = response.GetValue<RomMRemoteInstallEvent>(0);

                    _logger.Trace($"Socket cancel reason: {installResponse?.Reason}");

                    if (installResponse?.Id != null)
                        CancelInstall(installResponse.Id);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Remote install cancellation failed: {ex}");
                }
            });

            _socket.OnConnected += async (_, _) =>
            {
                _logger.Info("Connected to RomM remote install socket");
                _socketDisconnected = false;
                await InstallQueued();
            };

            _socket.OnDisconnected += (_, reason) =>
            {
                _logger.Warn($"RomM remote install socket disconnected: {reason}");
                _socketDisconnected = true;
            };
            _socket.OnError += (_, error) =>
            {
                _logger.Error($"Socket.IO error: {error}");
            };

            _socket.OnReconnectError += (_, ex) =>
            {
                _logger.Error($"Socket.IO connection attempt failed: {ex}");
            };

            _socket.OnReconnectAttempt += (_, attempt) =>
            {
                _logger.Info($"Socket.IO connection attempt #{attempt}");
            };
        }

        public async Task Connect()
        {
            try
            {
                _logger.Trace("Starting remote install heartbeat");

                if (_installHeartbeatTask != null)
                    return;

                _installHeartbeatCts = new CancellationTokenSource();
                _installHeartbeatTask = StartInstallHeartbeat(_installHeartbeatCts.Token);

                _logger.Trace("Attempting Socket.IO connection");

                try
                {
                    await _socket.ConnectAsync();
                    _logger.Trace("Socket.IO connection completed");
                }
                catch (Exception ex)
                {
                    _socketDisconnected = true;
                    _logger.Error($"Remote install socket connection failed: {ex}");

                }

                _logger.Trace("Claiming remote installs via REST");
                await InstallQueued();
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to start remote install controller: {ex}");
            }
        }

        public async Task Disconnect()
        {
            _installHeartbeatCts?.Cancel();

            await _socket.DisconnectAsync();

            if (_installHeartbeatTask != null)
            {
                await _installHeartbeatTask;
                _installHeartbeatTask = null;
            }

            _installHeartbeatCts?.Dispose();
        }

        public async Task InstallQueued()
        {
            // Wait for previous install requests to be started
            await _installLock.WaitAsync();

            _logger.Info($"Claiming remote installs for device " + $"{_plugin.Settings.AccountState.DeviceID}");

            try
            {
                var response = await _romMServer.POSTAsync($"/api/devices/{_plugin.Settings.AccountState.DeviceID}/installs/claim", null);
                if (response == null)
                {
                    _logger.Error($"Response from server was null");
                    return;
                }

                _logger.Info($"Remote install claim response: " + $"{response.RootElement.GetRawText()}");

                var installRequests = JsonSerializer.Deserialize<List<RomMRemoteInstallEvent>>(response);
                if (installRequests == null)
                {
                    _logger.Error($"Install Requests was null");
                    return;
                }

                foreach (var request in installRequests)
                {
                    await InstallPending(request);
                }

            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to claim remote installations: {ex}");
            }
            finally
            { 
                _installLock.Release(); 
            }

        }


        private async Task InstallPending(RomMRemoteInstallEvent request)
        {
            try
            {
                if (_plugin.DownloadQueueController?.IsDownloading(Convert.ToBase64String(Encoding.UTF8.GetBytes(request.Id))) ?? false)
                    return;

                if (_plugin.ImportedGames.TryGetValue(request.RomId.ToString(), out RomMRomLocal? localROM))
                {
                    if (request.fileIDs == null || request.fileIDs.Count == 0)
                        throw new Exception("Remote install request contains no files");


                    var mapping = _plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == localROM.MappingID);
                    if (mapping == null)
                        throw new Exception("No mapping found that matches this game");

                    var response = await GravitonPlugin.RomMServer.GETAsync($"/api/roms/{localROM.Id}");
                    if (response == null)
                        throw new Exception("Null response from server");

                    var rom = JsonSerializer.Deserialize<RomMRom>(response);
                    if (rom == null)
                        throw new Exception("ROM is null");

                    foreach (var fileid in request.fileIDs)
                    {
                        var file = rom.Files.FirstOrDefault(x => x.Id == fileid);

                        if (file == null)
                            throw new Exception("ROM files doesn't contain requested ID");

                        if (file.Category != RomMCategory.Game)
                        {
                            await UpdateInstallStatus(request.Id, RomMInstallStatus.Failed, "Client can only install base games");
                            return;
                        }
                    }

                    await InstallGame(request, mapping, rom, localROM);
                }
                else
                {
                    await UpdateInstallStatus(request.Id, RomMInstallStatus.Failed, "Game hasn't been imported into playnite");
                }
            }
            catch (Exception ex)
            {
                GravitonNotify.Notify($"graviton.remote.install.{request.Id}", $"Failed to recover remote installations: {ex.Message}", GravitonSeverity.Error, ex);
                await UpdateInstallStatus(request.Id, RomMInstallStatus.Failed, "An error occured in graviton");
            }

        }

        private void CancelInstall(string id)
        {
            var base64ID = Convert.ToBase64String(Encoding.UTF8.GetBytes(id));

            if (_plugin.DownloadQueueController?.IsDownloading(base64ID) ?? false)
            {
                lock (_remoteCancelledIds)
                {
                    _remoteCancelledIds.Add(id);
                }

                _plugin.DownloadQueueController?.Cancel(base64ID);
            }
            
        }

        public static async Task CancelInstallRequest(string id)
        {
            await GravitonPlugin.RomMServer.DELETEAsync($"/api/devices/{GravitonPlugin.Instance.Settings.AccountState.DeviceID}/installs/{id}");
        }

        public static async Task UpdateInstallStatus(string id, string status, string? reason)
        {
            await GravitonPlugin.RomMServer.PUTAsync($"/api/devices/{GravitonPlugin.Instance.Settings.AccountState.DeviceID}/installs/{id}", new { status = status, reason = reason });
        }


        private async Task InstallGame(RomMRemoteInstallEvent installEvent, EmulatorMapping Mapping, RomMRom ROM, RomMRomLocal romMLocal)
        {
            var Game = _playniteAPI.Library.Games.Get(romMLocal.PlayniteID ?? "") ?? throw new Exception(Loc.GetString("InstallGameDataMissing"));

            if(Game.InstallState == InstallState.Installed)
            {
                await UpdateInstallStatus(installEvent.Id, RomMInstallStatus.AlreadyInstalled, "Game is already installed");
                return;
            }

            if (string.IsNullOrEmpty(romMLocal.FileName))
                throw new Exception("Game has no file name set cannot continue install");

            var dstPath = Mapping.DestinationPathResolved ?? throw new Exception(Loc.GetString("InstallMappingDataMissing"));
            var installDir = romMLocal.InstallPath!.Replace(EmulatorMapping.InstallPathToken, dstPath);

            var tempDir = Path.Combine(_plugin.PluginDataPath, "temp", Game.Id.ToString());
            var tempPath = "";

            string downloadURL = "";

            if (installEvent.fileIDs.Count == 1)
            {
                var file = ROM.Files.FirstOrDefault(x => x.Id == installEvent.fileIDs[0]);
                if (file == null)
                    throw new Exception("Failed to find file with matching fileID");

                downloadURL = $"/api/roms/{romMLocal.Id}/content/{Uri.EscapeDataString(file.FileName)}?file_ids={installEvent.fileIDs[0]}";
 
                installDir = Path.GetDirectoryName(Path.Combine(dstPath, Path.GetRelativePath(ROM.FileSystemPath ?? "", file.FullPath)));
                installDir = installDir!.Replace("/", "\\");

                tempPath = Path.Combine(tempDir, Path.GetFileName(file.FullPath));

                // Skip download if the game is already installed
                if (File.Exists(Path.Combine(installDir!, Path.GetFileName(file.FullPath))))
                {

                    romMLocal.InstalledPath = Path.Combine(installDir!, Path.GetFileName(file.FullPath));
                    romMLocal.IsInstalledPathDirectory = false;
                    romMLocal.Save();

                    Game.InstallState = InstallState.Installed;
                    Game.InstallSize = (ulong)(new FileInfo(Path.Combine(installDir!, Path.GetFileName(file.FullPath))).Length);
                    Game.InstallDirectory = installDir;

                    await _playniteAPI.Library.Games.UpdateAsync(Game);

                    await UpdateInstallStatus(installEvent.Id, RomMInstallStatus.AlreadyInstalled, "Game was found on device, marking as installed");
                    return;
                }

            }
            else
            {
                downloadURL = $"/api/roms/{romMLocal.Id}/content/{Uri.EscapeDataString(romMLocal.Name + ".zip")}?file_ids={string.Join(',', installEvent.fileIDs)}";
                tempPath = Path.Combine(tempDir, romMLocal.Name + ".zip");
            }

            await CreateDownloadRequest(installEvent.Id, Mapping, downloadURL, tempPath, installDir, Game, romMLocal);
        }

        public static async Task RestoreDownloadRequest(DownloadRequestBackup request)
        {
            if (!GravitonPlugin.Instance.ImportedGames.ContainsKey(request.GameID))
                throw new Exception(Loc.GetString("InstallGameIdNotFound", ("GameID", request.GameID ?? "")));

            var localROM = GravitonPlugin.Instance.ImportedGames[request.GameID];
            var game = GravitonPlugin.PlayniteApi.Library.Games.Get(localROM.PlayniteID ?? "");

            if (game == null)
                throw new Exception("Failed to find game in playnite");

            var mapping = GravitonPlugin.Instance.Settings.Mappings.FirstOrDefault(x => x.MappingId == localROM.MappingID);

            if (mapping == null)
                throw new Exception(Loc.GetString("InstallMappingNotFound"));

            await CreateDownloadRequest(request.ID, mapping, request.DownloadURL, request.DownloadPath, request.InstallDir, game, localROM);
        }

        private static async Task CreateDownloadRequest(string ID, EmulatorMapping mapping, string downloadURL, string tempPath, string installDir, Game Game, RomMRomLocal romMLocal)
        {
            var req = new DownloadRequest
            {
                Id = Convert.ToBase64String(Encoding.UTF8.GetBytes(ID)),
                DisplayName = Game.Name,

                DownloadUrl = downloadURL,
                DownloadPath = tempPath,

                OnDownloadComplete = async (item, req) =>
                {
                    Directory.CreateDirectory(installDir!);

                    if (romMLocal.HasMultipleFiles || (mapping.AutoExtract && ArchiveExtractor.IsFileCompressed(req.DownloadPath)))
                    {

                        item.SetStatus(DownloadStatus.Extracting, Loc.GetString("DownloadStatusExtracting"));
                        GravitonPlugin.Logger?.Info($"Extracting {req.DownloadPath}...");

                        if (GravitonPlugin.Instance.Settings.Use7z && !string.IsNullOrEmpty(GravitonPlugin.Instance.Settings.PathTo7z) && GravitonPlugin.Instance.Settings.PathTo7z.EndsWith("7z.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            await ArchiveExtractor.ExtractArchiveWith7z(GravitonPlugin.Instance.Settings.PathTo7z, req.DownloadPath, installDir!, item, item.Cts.Token);
                        }
                        else
                        {
                            ArchiveExtractor.ExtractArchiveWithEntryProgress(req.DownloadPath, installDir!, item, item.Cts.Token);
                        }
                        try { File.Delete(req.DownloadPath); } catch { }

                        romMLocal.InstalledPath = installDir;
                        romMLocal.IsInstalledPathDirectory = true;

                        Game.InstallSize = 0;
                        Directory.GetFiles(installDir!).Select(x => (ulong)(new FileInfo(x).Length)).ForEach(y => Game.InstallSize += y);

                        Game.InstallState = InstallState.Installed;
                        Game.InstallDirectory = installDir;
                    }
                    else if (File.Exists(req.DownloadPath))
                    {
                        var installedPath = Path.Combine(installDir!, Path.GetFileName(req.DownloadPath));

                        GravitonInstallHelpers.CopyFileWithProgress(req.DownloadPath, installedPath, item, item.Cts.Token);

                        romMLocal.InstalledPath = installedPath;
                        romMLocal.IsInstalledPathDirectory = false;

                        Game.InstallState = InstallState.Installed;
                        Game.InstallSize = (ulong)(new FileInfo(installedPath).Length);
                        Game.InstallDirectory = installDir;
                    }

                    romMLocal.Save();
                    Game.InstallState = InstallState.Installed;
                    await GravitonPlugin.PlayniteApi.Library.Games.UpdateAsync(Game);

                    await UpdateInstallStatus(ID, RomMInstallStatus.Done, null);

                    if (File.Exists(req.DownloadPath))
                        File.Delete(req.DownloadPath);
                },

                OnCancelled = async () =>
                {
                    bool remoteCancelled = false;

                    lock (_remoteCancelledIds)
                    {
                        // Don't send cancel request if cancel came from server
                        if (_remoteCancelledIds.Contains(ID))
                        {
                            _remoteCancelledIds.Remove(ID);
                            remoteCancelled = true;
                        }
                    }

                    if (!remoteCancelled)
                        await CancelInstallRequest(ID);

                    Game.InstallState = InstallState.Uninstalled;
                    await GravitonPlugin.PlayniteApi.Library.Games.UpdateAsync(Game);
                },

                OnFailed = async ex =>
                {
                    GravitonNotify.Notify("graviton.install.failed", Loc.GetString("DownloadFailed", ("GameName", Game.Name), ("Error", ex.Message)), GravitonSeverity.Error, ex);
                    var game = GravitonPlugin.PlayniteApi.Library.Games.Get(Game.Id) ?? throw new Exception(Loc.GetString("InstallGameDataMissing"));
                    game.InstallState = InstallState.Uninstalled;
                    await GravitonPlugin.PlayniteApi.Library.Games.UpdateAsync(game);

                    await UpdateInstallStatus(ID, RomMInstallStatus.Failed, "An error occured in graviton");
                }
            };

            DownloadRequestBackup backup = new()
            {
                InstallType = InstallType.Remote,
                ID = ID,
                GameID = romMLocal.Id.ToString(),
                DownloadPath = tempPath,
                InstallDir = installDir,
                DownloadURL = downloadURL
            };


            // Enqueue (non-blocking)
            GravitonPlugin.Instance.DownloadQueueController?.Enqueue(req, backup);
        }

        private async Task StartInstallHeartbeat(CancellationToken token)
        {

            using PeriodicTimer timer = new(TimeSpan.FromSeconds(30));
            var lastRecovery = DateTimeOffset.UtcNow;

            try
            {
                while (await timer.WaitForNextTickAsync(token))
                {
                    var interval = _socketDisconnected ? TimeSpan.FromSeconds(30) : TimeSpan.FromMinutes(5);

                    if (DateTimeOffset.UtcNow - lastRecovery < interval)
                        continue;

                    lastRecovery = DateTimeOffset.UtcNow;

                    try
                    {
                        _logger.Trace("Heartbeat running InstallQueued");
                        await InstallQueued();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Remote install recovery failed: {ex}");
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Something..
            }
        }
    }
}