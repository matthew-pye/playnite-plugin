using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using RomM.Downloads;
using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProtoBuf;
using Newtonsoft.Json;

namespace RomM.Games
{
    internal class RomMInstallController : InstallController
    {
        protected readonly IRomM _romM;
        public ILogger Logger => LogManager.GetLogger();

        internal RomMInstallController(Game game, IRomM romM) : base(game)
        {
            Name = "Download";
            _romM = romM;
        }

        public override void Install(InstallActionArgs args)
        {
            var baseinfo = Game.GetRomMGameInfo();

            List<string> gameinfos = new List<string>();
            gameinfos.Add(Game.GameId);

            int romMId;
            if (int.TryParse(Game.Version.Split(':')[1], out romMId))
            {
                string itemPath = $"{_romM.Playnite.Paths.ExtensionsDataPath}/9700aa21-447d-41b4-a989-acd38f407d9f/{romMId}.json";
                if (File.Exists(itemPath))
                {
                    var siblinginfos = JsonConvert.DeserializeObject<List<RomMGameInfo>>(File.ReadAllText(itemPath));

                    foreach (var siblinginfo in siblinginfos)
                    {
                        gameinfos.Add(siblinginfo.AsGameId());
                    }
                }
            }

            

            var dstPath = baseinfo.Mapping?.DestinationPathResolved
                    ?? throw new Exception("Mapped emulator data cannot be found, try removing and re-adding.");

            //Place different game versions in sub-path
            dstPath = Path.Combine(dstPath, Path.GetFileNameWithoutExtension(Game.Name));         

            var req = new DownloadRequest
            {
                GameId = Game.Id,
                GameName = Game.Name,
                AutoExtract = baseinfo.Mapping != null && baseinfo.Mapping.AutoExtract,

                GameInfos = gameinfos,
                DstPath = dstPath,
                Use7z = _romM.Settings.Use7z,
                PathTo7Z = _romM.Settings.PathTo7z,

                // Called by queue AFTER download/extract is done
                BuildRoms = () =>
                {
                    var roms = new List<GameRom>();

                    foreach (var gameinfo in gameinfos)
                    {
                        RomMGameInfo rom = new RomMGameInfo();

                        var gameInfoStr = Convert.FromBase64String(gameinfo.Substring(2));
                        using (var ms = new MemoryStream(gameInfoStr))
                        {
                            rom = Serializer.Deserialize<RomMGameInfo>(ms);
                        }

                        // Paths (same as before)
                        var installDir = gameinfos.Count > 1 ? Path.Combine(dstPath, Path.GetFileNameWithoutExtension(rom.FileName)) : dstPath;

                        // If RomM indicates multiple files, we download as an archive name (zip) into the install folder.
                        // Otherwise we download the single ROM file.
                        var downloadFilePath = rom.HasMultipleFiles
                            ? Path.Combine(installDir, rom.FileName + ".zip")
                            : Path.Combine(installDir, rom.FileName);

                        // If the downloaded file still exists and wasn't extracted -> single file ROM
                        if (File.Exists(downloadFilePath))
                        {
                            roms.Add(new GameRom(Path.GetFileNameWithoutExtension(rom.FileName), downloadFilePath));
                            continue;
                        }

                        // Otherwise, we assume extracted files are in installDir
                        var supported = GetEmulatorSupportedFileTypes(rom);
                        var actualRomFiles = GetRomFiles(installDir, supported);

                        // Prefer .m3u if requested
                        var useM3u = rom.Mapping != null && rom.Mapping.UseM3u;
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
                    }

                    return roms;
                },

                // Callbacks into Playnite install pipeline
                OnInstalled = installedArgs =>
                {
                    var game = _romM.Playnite.Database.Games[Game.Id];
                    game.IsInstalled = true;
                    _romM.Playnite.Database.Games.Update(game);

                    InvokeOnInstalled(installedArgs);
                },

                OnCanceled = () =>
                {
                    var game = _romM.Playnite.Database.Games[Game.Id];
                    game.IsInstalling = false;
                    game.IsInstalled = false;
                    _romM.Playnite.Database.Games.Update(game);

                    InvokeOnInstallationCancelled(new GameInstallationCancelledEventArgs());
                },

                OnFailed = ex =>
                {
                    _romM.Playnite.Notifications.Add(
                        Game.GameId,
                        $"Failed to download {Game.Name}.{Environment.NewLine}{Environment.NewLine}{ex}",
                        NotificationType.Error);

                    Game.IsInstalling = false;
                }
            };

            // Enqueue (non-blocking)
            _romM.DownloadQueueController.Enqueue(req);
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

        private static List<string> GetEmulatorSupportedFileTypes(RomMGameInfo info)
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
