using Graviton.Models;
using Graviton.Models.Notifications;
using Graviton.Models.RomM.Rom;
using Graviton.Saves;

using Playnite;

namespace Graviton
{
    public static class GameSessionHandler
    {
        private static GravitonPlugin _plugin => GravitonPlugin.Instance;
        private static IPlayniteApi PlayniteAPI => GravitonPlugin.PlayniteApi;

        private static ScreenshotService? ScreenshotCapture = new();
        private static SaveWatcher? SaveWatcher = new(ScreenshotCapture);
        private static RomMRomLocal? ROM;
        private static string? GameID;

        public static bool IsAGameRunning { get; private set; } = false;

        public static async Task GameStarting(string gameID)
        {
            if (IsAGameRunning)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.sync.alreadyrunning", "A game is already running cannot, start sync!", GravitonSeverity.Info));
                return;
            }

            ROM = _plugin.ImportedGames.FirstOrDefault(x => x.Key == gameID).Value ?? null;
            if (ROM == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.game.notfound", $"Game with {gameID} not found, skipping sync!", GravitonSeverity.Info));
                return;
            }

            if(_plugin.Settings.SyncPlaySession)
                _ = _plugin.StatusController?.StartActivityHeartbeat(gameID);

            if (ROM.LocalSave != null && _plugin.Settings.SaveSyncEnabled)
            {
                if (_plugin.Settings.DownloadSaveOnLaunch)
                {
                    if (!ROM.LocalSave.IsTempRestored)
                        await SaveNegotiator.NegotiateSave(ROM);
                }
                else
                    GravitonNotify.Add(new GravitonNotification("graviton.sync.notenabled", "'Sync before game start' disabled, skipping sync!", GravitonSeverity.Info));

            }
            
            GameID = gameID;
            IsAGameRunning = true;
        }

        public static async Task GameStarted(int processID, string? gameID)
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
                    var paths = ROM.LocalSave.SourceFilePaths.Select(x => x.Replace(EmulatorMapping.MappingPathToken, mapping.SavePath)).ToList();

                    SaveWatcher!.Setup(paths);
                    await ScreenshotCapture!.Setup(processID, _plugin.Settings.SecondsBeforeSave);

                    await ScreenshotCapture.Start();
                    await SaveWatcher.Start();
                    
                }
            }

        }

        public static async Task GameStopped(string? gameID, uint sessionLength)
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
                        await SaveManager.CheckRestoredSaveNeedUploading(ROM, SaveWatcher?.NewestSaveScreenshot);
                    else
                        await SaveNegotiator.NegotiateSave(ROM, SaveWatcher?.NewestSaveScreenshot);
                }
                else
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.sync.notenabled", "'Sync after game quit' disabled, skipping sync!", GravitonSeverity.Info));
                }
            } 

        }
    }


}