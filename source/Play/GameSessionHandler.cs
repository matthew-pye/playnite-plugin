using Emunight;

using Graviton.Models;
using Graviton.Models.Notifications;
using Graviton.Models.RomM.Rom;
using Graviton.Notifications;
using Graviton.Saves;

using Playnite;

using System.IO;

using static Playnite.Plugin;

namespace Graviton.Play
{
    public class GameSessionHandler
    {
        private GravitonPlugin _plugin;
        private IPlayniteApi _playniteAPI;
        private GravitonLogger _logger;

        private IPowerShellRuntime? _psRuntime;

        private SaveController SaveController => _plugin.SaveController!;
        private ScreenshotService ScreenshotCapture;
        private SaveWatcher SaveWatcher;

        private RomMRomLocal? ROM;
        private EmulatorMapping? Mapping;
        private IReadOnlyDictionary<string, object>? StartProperties;

        public string? GameID;

        public bool IsAGameRunning { get; private set; } = false;

        public GameSessionHandler(GravitonPlugin plugin, IPlayniteApi playniteAPI, GravitonLogger logger)
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;

            ScreenshotCapture = new();
            SaveWatcher = new(ScreenshotCapture);
        }

        public async Task GameStarting(OnGameStartingEventArgs args)
        {
            // Set GameID
            var gameID = args.Game.LibraryGameId;
            GameID = gameID;

            if (IsAGameRunning)
            {
                GravitonNotify.Notify("graviton.sync.alreadyrunning", Loc.GetString("SyncAlreadyRunning"), GravitonSeverity.Info);
                args.CancelStartup = true;
                return;
            }

            if (string.IsNullOrEmpty(gameID))
            {
                GravitonNotify.Notify("graviton.gameID.isEmpty", "Game ID is empty, cannot launch game!", GravitonSeverity.Error);
                args.CancelStartup = true;
                return;
            }

            // Find ROM in ImportedGames
            ROM = _plugin.ImportedGames.FirstOrDefault(x => x.Key == gameID).Value ?? null;
            if (ROM == null)
            {
                GravitonNotify.Notify("graviton.game.notfound", Loc.GetString("GameNotFoundSkipSync", ("GameId", gameID)), GravitonSeverity.Info);
                args.CancelStartup = true;
                return;
            }

            // Find Mapping
            var mapping = _plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == ROM.MappingID);
            if (mapping == null)
            {
                GravitonNotify.Notify("graviton.game.notfound", Loc.GetString("GameNotFoundSkipSync", ("GameId", gameID)), GravitonSeverity.Info);
                args.CancelStartup = true;
                return;
            }
            Mapping = mapping;

            // Check StartProperties contains both keys for the ROM Path and the Emulator Path
            // this means in the 2 other functions GameStarted & GameStopped don't need to check for the keys as the startup will be cancelled if it fails to find them
            if (!args.StartProperties.ContainsKey("ImagePath") || !args.StartProperties.ContainsKey("EmulatorDir"))
            {
                GravitonNotify.Notify("graviton.gamestarting.propertynotfound", "One or more start properties were not found, cannot launch game!", GravitonSeverity.Info);
                args.CancelStartup = true;
                return;
            }
            StartProperties = args.StartProperties;

            // Check if save needs syncing before starting game
            if (ROM.LocalSave != null && _plugin.Settings.SaveSyncEnabled)
            {
                if (_plugin.Settings.DownloadSaveOnLaunch)
                {
                    if (!ROM.LocalSave.IsTempRestored)
                        await SaveController.Negotiator.NegotiateSave(ROM);
                }
                else
                    GravitonNotify.Notify("graviton.sync.notenabled", Loc.GetString("SyncBeforeGameStartDisabled"), GravitonSeverity.Info);

            }

            // Run game start script from Emunight
            if (Mapping.IsImportedEmulator)
            {
                var emulator = Mapping.Emulator as ImportedEmulator;
                var profilesettings = emulator?.ProfileSettings?.FirstOrDefault(x => x.ProfileId == mapping.Profile?.Id);

                if (profilesettings != null)
                {
                    await RunLifecycleScriptAsync(profilesettings.StartingScript, args.Game, StartProperties["ImagePath"] as string ?? "", StartProperties["EmulatorDir"] as string);
                }
                else
                {
                    GravitonNotify.Notify("graviton.gamestarting.profilesettingsnotfound", "Failed to find profile settings for the selected profile, skipping starting script!", GravitonSeverity.Warn);
                }

            }
            else if (Mapping.IsCustomEmulator)
            {
                var emulator = Mapping.Emulator as CustomEmulator;

                if (emulator != null)
                {
                    await RunLifecycleScriptAsync(emulator.StartingScript, args.Game, StartProperties["ImagePath"] as string, StartProperties["EmulatorDir"] as string);
                }
                else
                {
                    GravitonNotify.Notify("graviton.gamestarting.emulatornotfound", "Failed to find emulator for the mapping, skipping starting script!", GravitonSeverity.Warn);
                }
            }

            // Start activity session on RomM
            if (_plugin.Settings.KeepStatusSynced)
                _ = _plugin.StatusController?.StartActivityHeartbeat(gameID!);

            IsAGameRunning = true;
        }

        public async Task GameStarted(OnGameStartedEventArgs args)
        {
            if (!IsAGameRunning || args.StartingArgs.Game.LibraryGameId != GameID)
                return;

            if (ROM == null)
                return;

            // Run game started script from Emunight
            if (Mapping!.IsImportedEmulator)
            {
                var emulator = Mapping.Emulator as ImportedEmulator;
                var profilesettings = emulator?.ProfileSettings?.FirstOrDefault(x => x.ProfileId == Mapping.Profile?.Id);

                if (profilesettings != null)
                {
                    await RunLifecycleScriptAsync(profilesettings.StartedScript, args.StartingArgs.Game, StartProperties!["ImagePath"] as string, StartProperties!["EmulatorDir"] as string);
                }
                else
                {
                    GravitonNotify.Notify("graviton.gamestarting.profilesettingsnotfound", "Failed to find profile settings for the selected profile, skipping starting script!", GravitonSeverity.Warn);
                }

            }
            else if (Mapping!.IsCustomEmulator)
            {
                var emulator = Mapping.Emulator as CustomEmulator;

                if (emulator != null)
                {
                    await RunLifecycleScriptAsync(emulator.StartedScript, args.StartingArgs.Game, StartProperties!["ImagePath"] as string, StartProperties!["EmulatorDir"] as string);
                }
                else
                {
                    GravitonNotify.Notify("graviton.gamestarting.emulatornotfound", "Failed to find emulator for the mapping, skipping starting script!", GravitonSeverity.Warn);
                }
            }

            // Start screenshot capture for save screenshots
            if (ROM.LocalSave != null && _plugin.Settings.CaptureScreenshots)
            {
                if (ROM.LocalSave.SourceFilePaths.Count > 0 && Mapping != null)
                {
                    var paths = ROM.LocalSave.SourceFilePaths.Select(x => x.Replace(EmulatorMapping.SavePathToken, Mapping.SavePath)).ToList();

                    SaveWatcher!.Setup(paths);
                    await ScreenshotCapture!.Setup(args.StartedArgs.StartedProcessId, _plugin.Settings.SecondsBeforeSave);

                    await ScreenshotCapture.Start();
                    await SaveWatcher.Start();
                    
                }
            }

        }

        public async Task GameStopped(OnGameStoppedEventArgs args)
        {
            var stoppedTime = DateTime.UtcNow;

            if (!IsAGameRunning || args.StartingArgs.Game.LibraryGameId != GameID)
                return;

            IsAGameRunning = false;

            // Stop activity session on RomM
            if (_plugin.Settings.KeepStatusSynced)
                _ = _plugin.StatusController?.StopActivityHeartbeat(args.StartingArgs.Game.LibraryGameId!, stoppedTime, args.StoppedArgs.SessionLength * 1000);

            if (ROM == null)
                return;

            // Run stopped game script from Emunight
            if (Mapping!.IsImportedEmulator)
            {
                var emulator = Mapping.Emulator as ImportedEmulator;
                var profilesettings = emulator?.ProfileSettings?.FirstOrDefault(x => x.ProfileId == Mapping.Profile?.Id);

                if (profilesettings != null)
                {
                    await RunLifecycleScriptAsync(profilesettings.StoppedScript, args.StartingArgs.Game, StartProperties!["ImagePath"] as string, StartProperties!["EmulatorDir"] as string);
                }
                else
                {
                    GravitonNotify.Notify("graviton.gamestarting.profilesettingsnotfound", "Failed to find profile settings for the selected profile, skipping starting script!", GravitonSeverity.Warn);
                }

            }
            else if (Mapping!.IsCustomEmulator)
            {
                var emulator = Mapping.Emulator as CustomEmulator;

                if (emulator != null)
                {
                    await RunLifecycleScriptAsync(emulator.StoppedScript, args.StartingArgs.Game, StartProperties!["ImagePath"] as string, StartProperties!["EmulatorDir"] as string);
                }
                else
                {
                    GravitonNotify.Notify("graviton.gamestarting.emulatornotfound", "Failed to find emulator for the mapping, skipping starting script!", GravitonSeverity.Warn);
                }
            }

            // Stop screenshot capture and check to see if save needs uploading
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
                    GravitonNotify.Notify("graviton.sync.notenabled", Loc.GetString("SyncAfterGameQuitDisabled"), GravitonSeverity.Info);
                }
            }

            _psRuntime?.Dispose();

        }

        public async Task GameCancelled(OnGameStartupCancelledEventArgs args)
        {
            _psRuntime?.Dispose();
        }

        private async Task RunLifecycleScriptAsync(string? script, Game game, string? romPath, string? emulatorDir)
        {
            if (string.IsNullOrWhiteSpace(script))
                return;

            _psRuntime ??= _playniteAPI.CreatePowerShellRuntime(new CreatePowerShellrutimeArgs
            {
                RuntimeName = $"Graviton_{game.LibraryGameId}"
            });

            var vars = StartProperties!.ToDictionary();
            var workDir = Path.GetDirectoryName(romPath) ?? emulatorDir ?? string.Empty;
            var result = await _psRuntime.ExecuteAsync(script, workDir, vars);

            if (result.Error != null)
                GravitonNotify.Notify("graviton.script.error", $"Emulator script failed: {result.Error.Message}", GravitonSeverity.Error);
            
        }

    }
}