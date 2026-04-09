using Playnite;

using RomMLibrary.Install.Downloads;
using RomMLibrary.Models.RomM.Rom;

using System.IO;

namespace RomMLibrary.Install
{
    enum InstallStatus
    {
        Cancelled = -1
    }

    internal class RomMInstallController : InstallController
    {
        protected readonly RomMLibraryPlugin Plugin;
        public ILogger Logger => LogManager.GetLogger();
        public GameInstallInfo GameData;

        private Game Game;

        internal RomMInstallController(RomMLibraryPlugin romM, Game game, GameInstallInfo gameData) : base(RomMLibraryPlugin.Id, "Download", game.LibraryGameId ?? throw new Exception("Game doesn't have libraryID!"))
        {
            Plugin = romM;
            GameData = gameData;
            Game = game;
        }

        public override async Task InstallAsync(InstallActionArgs args)
        {
            if (GameData.Id == (int)InstallStatus.Cancelled)
            {
                CancelInstall();
                await Task.CompletedTask;
                return; 
            }   

            //var dstPath = GameData.Mapping?.DestinationPathResolved
            //    ?? throw new Exception("Mapped emulator data cannot be found, try removing and re-adding.");

            // Paths (same as before)
            //var installDir = Path.Combine(dstPath, Path.GetFileNameWithoutExtension(GameData.FileName));

            // If RomM indicates multiple files, we download as an archive name (zip) into the install folder.
            // Otherwise we download the single ROM file.
            //var downloadFilePath = _gameData.HasMultipleFiles
            //    ? Path.Combine(installDir, _gameData.FileName + ".zip")
            //    : Path.Combine(installDir, _gameData.FileName);

            var req = new DownloadRequest
            {
                GameId = Game.Id,
                GameName = Game.Name,

                DownloadUrl = GameData.DownloadURL,
                //InstallDir = installDir,
                //GamePath = downloadFilePath,
                Use7z = Plugin.Settings.Use7z,
                PathTo7Z = Plugin.Settings.PathTo7z,

                HasMultipleFiles = GameData.HasMultipleFiles,
                AutoExtract = GameData.Mapping != null && GameData.Mapping.AutoExtract,

                // Called by queue AFTER download/extract is done
                BuildRoms = () =>
                {
                    var roms = new List<GameRom>();

                    // If the downloaded file still exists and wasn't extracted -> single file ROM
                    if (File.Exists(downloadFilePath))
                    {
                        roms.Add(new GameRom(Game.Name, downloadFilePath));
                        return roms;
                    }

                    // Otherwise, we assume extracted files are in installDir
                    var supported = GetEmulatorSupportedFileTypes(_gameData);
                    var actualRomFiles = GetRomFiles(installDir, supported);

                    // Prefer .m3u if requested
                    var useM3u = GameData.Mapping != null && GameData.Mapping.UseM3U && supported.Any(x => x.ToLower() == "m3u");
                    if (useM3u)
                    {
                        var m3uFile = actualRomFiles.FirstOrDefault(m =>
                            m.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase));

                        if (!string.IsNullOrEmpty(m3uFile))
                        {
                            roms.Add(new GameRom(Game.Name, m3uFile));
                            return roms;
                        }
                    }

                    // Otherwise add all rom files except m3u (we don’t want duplicates)
                    foreach (var f in actualRomFiles.Where(f => !f.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase)))
                    {
                        roms.Add(new GameRom(Game.Name, f));
                    }

                    return roms;
                },

                // Callbacks into Playnite install pipeline
                OnInstalled = installedArgs =>
                {
                    var game = PlayniteApi.Library.Games[Game.Id];
                    game.IsInstalled = true;
                    PlayniteApi.Library.Games.Update(game);

                    GameInstalledAsync(installedArgs);
                },

                OnCancelled = () =>
                {
                    var game = PlayniteApi.Library.Games[Game.Id];
                    game.IsInstalling = false;
                    game.IsInstalled = false;
                    PlayniteApi.Library.Games.Update(game);

                    GameInstallationCancelledAsync(new GameInstallationCancelledArgs());
                },

                OnFailed = ex =>
                {
                    PlayniteApi.Notifications.Add(new NotificationMessage(
                        Game.LibraryGameId ?? RomMLibraryPlugin.Id,
                        $"Failed to download {Game.Name}.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                        NotificationSeverity.Error)); 

                    Game.IsInstalling = false;
                }
            };

            // Enqueue (non-blocking)
            Plugin.DownloadQueueController.Enqueue(req);
        }

        private void CancelInstall()
        {
            var game = PlayniteApi.Library.Games[Game.Id];
            game.IsInstalling = false;
            PlayniteApi.Library.Games.Update(game);

            GameInstallationCancelledAsync(new GameInstallationCancelledArgs());
        }

        private static string[] GetRomFiles(string installDir, List<string> supportedFileTypes)
        {
            // NOTE: this traversal check is weak; containment checks should be done via GetFullPath
            // against a trusted root. Keeping your existing checks as-is for now.
            if (installDir == null || installDir.Contains("../") || installDir.Contains(@"..\"))
            {
                throw new ArgumentException("Invalid file path");
            }

            if (supportedFileTypes == null || supportedFileTypes.Count == 0)
            {
                return Directory.GetFiles(installDir, "*", SearchOption.AllDirectories)
                    .ToArray();
            }

            return supportedFileTypes.SelectMany(fileType =>
            {
                if (fileType == null || fileType.Contains("../") || fileType.Contains(@"..\"))
                {
                    throw new ArgumentException("Invalid file path");
                }

                return Directory.GetFiles(installDir, "*." + fileType, SearchOption.AllDirectories);
            }).ToArray();
        }

        private static List<string> GetEmulatorSupportedFileTypes(GameInstallInfo info)
        {
            if (info.Mapping.EmulatorProfile is CustomEmulatorProfile)
            {
                var customProfile = info.Mapping.EmulatorProfile as CustomEmulatorProfile;
                return customProfile.ImageExtensions;
            }
            else if (info.Mapping.EmulatorProfile is BuiltInEmulatorProfile)
            {
                var builtInProfile = (info.Mapping.EmulatorProfile as BuiltInEmulatorProfile);
                return API.Instance.Emulation.Emulators
                    .FirstOrDefault(e => e.Id == info.Mapping.Emulator.BuiltInConfigId)?
                    .Profiles
                    .FirstOrDefault(p => p.Name == builtInProfile.Name)?
                    .ImageExtensions;
            }
        
            return null;
        }

        private static bool IsFileCompressed(string filePath)
        {
            // Exclude disk images which aren't handled by sharpcompress
            if (Path.GetExtension(filePath).Equals(".iso", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return ArchiveFactory.IsArchive(filePath, out var type);
        }
    }
}
