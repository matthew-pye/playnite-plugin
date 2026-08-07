using Graviton.Models;
using Graviton.Models.Notifications;
using Graviton.Models.RomM.Rom;
using Graviton.Saves;

using Playnite;

namespace Graviton
{
    public class GameSessionHandler
    {
        private GravitonPlugin _plugin;
        private IPlayniteApi _playniteAPI;
        private ILogger _logger;

        private SaveController SaveController => _plugin.SaveController!;
        private ScreenshotService ScreenshotCapture;
        private SaveWatcher SaveWatcher;

        private RomMRomLocal? ROM;
        public string? GameID;

        public bool IsAGameRunning { get; private set; } = false;

        public GameSessionHandler(GravitonPlugin plugin, IPlayniteApi playniteAPI, ILogger logger)
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;

            ScreenshotCapture = new();
            SaveWatcher = new(ScreenshotCapture);
        }

        public async Task GameStarting(string gameID)
        {
            if (IsAGameRunning)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.sync.alreadyrunning", Loc.GetString("SyncAlreadyRunning"), GravitonSeverity.Info));
                return;
            }

            ROM = _plugin.ImportedGames.FirstOrDefault(x => x.Key == gameID).Value ?? null;
            if (ROM == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.game.notfound", Loc.GetString("GameNotFoundSkipSync", ("GameId", gameID)), GravitonSeverity.Info));
                return;
            }

            var mapping = _plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == ROM.MappingID);
            if (mapping == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.game.notfound", Loc.GetString("GameNotFoundSkipSync", ("GameId", gameID)), GravitonSeverity.Info));
                return;
            }

            if(mapping.IsImportedEmulator)
            {
                
            }
            else
            {

            }

            if (_plugin.Settings.SyncPlaySession)
                _ = _plugin.StatusController?.StartActivityHeartbeat(gameID);

            if (ROM.LocalSave != null && _plugin.Settings.SaveSyncEnabled)
            {
                if (_plugin.Settings.DownloadSaveOnLaunch)
                {
                    if (!ROM.LocalSave.IsTempRestored)
                        await SaveController.Negotiator.NegotiateSave(ROM);
                }
                else
                    GravitonNotify.Add(new GravitonNotification("graviton.sync.notenabled", Loc.GetString("SyncBeforeGameStartDisabled"), GravitonSeverity.Info));

            }
            
            GameID = gameID;
            IsAGameRunning = true;
        }

        public async Task GameStarted(int processID, string? gameID)
        {
            if (!IsAGameRunning || gameID != GameID)
                return;

            if (ROM == null)
                return;

            if (ROM.LocalSave != null && _plugin.Settings.CaptureScreenshots)
            {
                var mapping = _plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == ROM.MappingID);

                if (ROM.LocalSave.SourceFilePaths.Count > 0 && mapping != null)
                {
                    var paths = ROM.LocalSave.SourceFilePaths.Select(x => x.Replace(EmulatorMapping.SavePathToken, mapping.SavePath)).ToList();

                    SaveWatcher!.Setup(paths);
                    await ScreenshotCapture!.Setup(processID, _plugin.Settings.SecondsBeforeSave);

                    await ScreenshotCapture.Start();
                    await SaveWatcher.Start();
                    
                }
            }

        }

        public async Task GameStopped(string? gameID, uint sessionLength)
        {
            var stoppedTime = DateTime.UtcNow;

            if (!IsAGameRunning || gameID != GameID)
                return;

            IsAGameRunning = false;

            if (_plugin.Settings.SyncPlaySession)
                _ = _plugin.StatusController?.StopActivityHeartbeat(gameID!, stoppedTime, sessionLength * 1000);

            if (ROM == null)
                return;

            if (_plugin.Settings.SaveSyncEnabled && ROM.LocalSave != null)
            {
                if (_plugin.Settings.CaptureScreenshots)
                {
                    _ = SaveWatcher!.Stop();
                    await ScreenshotCapture!.Stop();
                }

                if (_plugin.Settings.UploadSaveOnFinished)
                {
                    if (ROM.LocalSave.IsTempRestored)
                        await SaveController.Manager.CheckRestoredSaveNeedUploading(ROM, SaveWatcher?.NewestSaveScreenshot);
                    else
                        await SaveController.Negotiator.NegotiateSave(ROM, SaveWatcher?.NewestSaveScreenshot);
                }
                else
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.sync.notenabled", Loc.GetString("SyncAfterGameQuitDisabled"), GravitonSeverity.Info));
                }
            } 

        }
    }


}