using Emunight;

using Graviton.Models;
using Graviton.Models.Notifications;
using Graviton.Notifications;

using Playnite;

using System.IO;
using System.Text.RegularExpressions;

using static Playnite.Plugin;

namespace Graviton.Play
{
    internal class GravitonPlayController
    {
        private GravitonPlugin _plugin;
        private IPlayniteApi _playniteAPI;
        private GravitonLogger _logger;
        private IEmunightAPI _emunightAPI;

        public GravitonPlayController(GravitonPlugin plugin, IPlayniteApi playniteAPI, GravitonLogger logger, IEmunightAPI emunightAPI)
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;
            _emunightAPI = emunightAPI;
        }

        public async Task<List<PlayController>> GetPlayActionsAsync(GetPlayActionsArgs args)
        {
            if (args.Game.LibraryId == GravitonPlugin.Id && _plugin.ImportedGames.ContainsKey(args.Game.LibraryGameId ?? "-1"))
            {
                var mapping = _plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == _plugin.ImportedGames[args.Game.LibraryGameId!].MappingID);
                if (mapping == null)
                {
                    GravitonNotify.Notify("graviton.play.nomapping", "Failed to find emulator mapping associated with this game!", GravitonSeverity.Error);
                    return [];
                }

                if(mapping.IsImportedEmulator)
                {
                    return GetEmulatorPlayActions(args, mapping);
                }
                else if(mapping.IsCustomEmulator)
                {
                    return GetCustomEmulatorPlayActions(args, mapping);
                }
                else
                {
                    GravitonNotify.Notify("graviton.play.noemulatorset", "No emulator is set in the mapping for this game!", GravitonSeverity.Error);

                    var profileSettings = (mapping.Emulator as ImportedEmulator)?.ProfileSettings?.FirstOrDefault(x => x.ProfileId == mapping.Profile?.Id);
                    var customEmu = (mapping.Emulator as CustomEmulator);
                    _logger.Info($"Imported Emulator\n" +
                        $"\tIsImportedEmulator: {mapping.IsImportedEmulator}\n" +
                        $"\tEmulator ID: {mapping.Emulator?.Id ?? "NULL"}\n" +
                        $"\tEmulator Name: {mapping.Emulator?.Name ?? "NULL"}\n" +
                        $"\tProfile ID: {mapping.Profile?.Id ?? "NULL"}\n" +
                        $"\tProfile Name: {mapping.Profile?.Name ?? "NULL"}" +
                        $"\tProfile Override Args: {profileSettings?.OverrideArguments.ToString() ?? "NULL"} - {profileSettings?.Arguments ?? "NULL"}\n" +
                        $"\tProfile Override Executable: {profileSettings?.OverrideExecutable.ToString() ?? "NULL"} - {profileSettings?.Executable ?? "NULL"}"
                        );
                    _logger.Info($"Custom Emulator\n" +
                        $"\tIsCustomEmulator: {mapping.IsCustomEmulator}\n" +
                        $"\tEmulator ID: {mapping.Emulator?.Id ?? "NULL"}\n" +
                        $"\tEmulator Name: {mapping.Emulator?.Name ?? "NULL"}\n" +
                        $"\tEmulator Startup Path: {customEmu?.StartupPath ?? "NULL"}\n" +
                        $"\tEmulator Args: {customEmu?.Arguments ?? "NULL"}"
                        );
                    return [];
                }
            }
            return [];
        }


        private List<PlayController> GetEmulatorPlayActions(GetPlayActionsArgs args, EmulatorMapping mapping)
        {
            var emulator = mapping.Emulator as ImportedEmulator;

            if (!mapping.IsSetup || emulator == null || mapping.Profile == null)
            {
                GravitonNotify.Notify("graviton.play.mappingnotsetup", "The mapping associated with this game has not been setup, cannot launch game!", GravitonSeverity.Error);
                return [];
            }

            var roms = GetPossibleLaunchFiles(args.Game, mapping.Profile!.FileTypes);

            if (roms == null || roms.Count <= 0)
                return [];

            return GenerateEmulatorPlayControllers(args.Game, roms, emulator, mapping.Profile);
        }

        private List<PlayController> GetCustomEmulatorPlayActions(GetPlayActionsArgs args, EmulatorMapping mapping)
        {
            var emulator = mapping.Emulator as CustomEmulator;

            if(emulator == null)
            {
                return [];
            }

            if(emulator.FileTypes == null)
            {
                return [];
            }

            var roms = GetPossibleLaunchFiles(args.Game, emulator.FileTypes);

            if (roms == null || roms.Count <= 0)
                return [];


            return GenerateCustomEmulatorPlayControllers(args.Game, roms, emulator);
        }

        private List<PlayController> GetStreamingPlayActions()
        {
            //TODO setup RomM emulator streaming
            return [];
        }

        private List<string>? GetPossibleLaunchFiles(Game game, HashSet<string> fileTypes)
        {
            if (File.Exists(_plugin.ImportedGames[game.LibraryGameId!].InstalledPath))
            {
                // Check if installed game is supported by the selected emulator
                if (fileTypes.Any(x => _plugin.ImportedGames[game.LibraryGameId!].InstalledPath!.EndsWith(x.Contains('.') ? x : $".{x}", StringComparison.OrdinalIgnoreCase)))
                {
                    return new List<string> { _plugin.ImportedGames[game.LibraryGameId!].InstalledPath! };
                }

                // Check to see if emulator supports no extention and if the installed game has no extention
                if(fileTypes.Contains("\u003Cnone\u003E") && !_plugin.ImportedGames[game.LibraryGameId!].InstalledPath!.Contains('.'))
                {
                    return new List<string> { _plugin.ImportedGames[game.LibraryGameId!].InstalledPath! };
                }

                GravitonNotify.Notify("graviton.getromfiles.unsupported", "Installed game has a filetype that is not supported by the selected emulator/profile", GravitonSeverity.Error);
                _logger.Info($"Emulator only supports {string.Join(", ", fileTypes)} file extentions");
                return null;
            }
            else if (_plugin.ImportedGames[game.LibraryGameId!].IsInstalledPathDirectory && Directory.Exists(_plugin.ImportedGames[game.LibraryGameId!].InstalledPath))
            {
                // Scan all files in a directory for files that are supported by the emulator
                List<string> roms;

                // If supported filetypes contains <none> check for extention with scan
                if (fileTypes.Contains("\u003Cnone\u003E"))
                {
                    roms = Directory.EnumerateFiles(_plugin.ImportedGames[game.LibraryGameId!].InstalledPath!, "*.*", SearchOption.AllDirectories)
                        .Where(x => fileTypes.Any(y => x.EndsWith($".{y}", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(Path.GetExtension(x)))).ToList();
                }
                else
                {
                    roms = Directory.EnumerateFiles(_plugin.ImportedGames[game.LibraryGameId!].InstalledPath!, "*.*", SearchOption.AllDirectories)
                        .Where(x => fileTypes.Any(y => x.EndsWith($".{y}", StringComparison.OrdinalIgnoreCase))).ToList();
                }

                if (roms == null || roms.Count <= 0)
                {
                    GravitonNotify.Notify("graviton.getromfiles.unsupported", "Could not find a sutiable file that is supported by the selected emulator/profile", GravitonSeverity.Error);
                    _logger.Info($"Selected emulator only supports {string.Join(", ", fileTypes)} file extentions");
                }

                return roms;
            }
            else
            {
                // Game isn't installed or has been deleted outside of playnite
                GravitonNotify.Notify("graviton.getromfiles.notinstalled", "Game data not found, please reinstall", GravitonSeverity.Error);
                game.InstallState = InstallState.Uninstalled;
                _ = _playniteAPI.Library.Games.UpdateAsync(game);
                return null;
            }
        }

        private List<PlayController> GenerateEmulatorPlayControllers(Game game, List<string> files, ImportedEmulator emulator, EmulatorProfile profile)
        {
            var profileSettings = emulator.ProfileSettings?.FirstOrDefault(x => x.ProfileId == profile.Id);
            if(profileSettings == null)
            {
                GravitonNotify.Notify("graviton.playcontrollers.profilesettingsnotfound", "Failed to find emulator profile settings, cannot launch game!", GravitonSeverity.Error);
                return [];
            }

            var executablePath = FindEmulatorExecutable(emulator.InstallDir ?? "", profile, profileSettings.Executable ?? "");
            if (string.IsNullOrEmpty(executablePath))
                return [];

            string args = profileSettings.OverrideArguments ? profileSettings.Arguments ?? "" : profile.WindowsData?.StartupArguments ?? "";
            if(string.IsNullOrEmpty(args))
            {
                GravitonNotify.Notify("graviton.playcontrollers.argsnotfound", "Failed to find startup arguments, please check settings in Emunight!", GravitonSeverity.Error);
                return [];
            }

            List<PlayController> controllers = new List<PlayController>();

            foreach (var file in files)
            {
                var controller = new AutomaticFilePlayController(new FileGameAction
                {
                    Name = files.Count > 1 ? $"Launch {Path.GetFileName(file)}" : "Launch",
                    Path = executablePath,
                    IsPlayAction = true,
                    Arguments = _emunightAPI.ExpandVariables(args, game, Path.GetDirectoryName(executablePath), file, true),
                    TrackingOptions =
                    {
                        Mode = TrackingMode.OriginalProcess,
                        TrackingValue = executablePath
                    }
                });

                controller.StartProperties.Add("ImagePath", file);
                controller.StartProperties.Add("EmulatorDir", Path.GetDirectoryName(executablePath) ?? "");
                controller.StartProperties.Add("PlayniteApi", _playniteAPI);
                controller.StartProperties.Add("Game", game);
                controller.StartProperties.Add("ImportedEmulator", emulator);
                controller.StartProperties.Add("Emulator", emulator);
                controller.StartProperties.Add("ImportedEmulatorProfile", profile);

                controllers.Add(controller);
            }

            return controllers;

        }
        
        private List<PlayController> GenerateCustomEmulatorPlayControllers(Game game, List<string> files, CustomEmulator emulator)
        {
            if (string.IsNullOrEmpty(emulator.StartupPath))
                return [];

            string? args = emulator.Arguments;
            if (string.IsNullOrEmpty(args))
            {
                GravitonNotify.Notify("graviton.playcontrollers.argsnotfound", "Failed to find startup arguments, please check settings in Emunight!", GravitonSeverity.Error);
                return [];
            }

            List<PlayController> controllers = new List<PlayController>();

            foreach (var file in files)
            {
                var controller = new AutomaticFilePlayController(new FileGameAction
                {
                    Name = files.Count > 1 ? $"Launch {Path.GetFileName(file)}" : "Launch",
                    Path = emulator.StartupPath,
                    Arguments = _emunightAPI.ExpandVariables(args, game, Path.GetDirectoryName(emulator.StartupPath), file, true),
                    TrackingOptions =
                    {
                        Mode = TrackingMode.OriginalProcess,
                        TrackingValue = emulator.StartupPath
                    }
                });

                controller.StartProperties.Add("ImagePath", file);
                controller.StartProperties.Add("EmulatorDir", Path.GetDirectoryName(emulator.StartupPath) ?? "");
                controller.StartProperties.Add("PlayniteApi", _playniteAPI);
                controller.StartProperties.Add("Game", game);
                controller.StartProperties.Add("Emulator", emulator);

                controllers.Add(controller);
            }

            return controllers;
        }
        
        private string? FindEmulatorExecutable(string installDir, EmulatorProfile profile, string overrideExecutable = "")
        {
            if (string.IsNullOrEmpty(installDir))
            {
                GravitonNotify.Notify("graviton.findemulator.noinstalldir", "Install directory is empty cannot find emulator, please check settings in Emunight", GravitonSeverity.Error);
                return null;
            }
                
            // Return install directory if user has put the executable as the install directory
            if (File.Exists(installDir))
                return installDir;

            // Find emulator executable
            if (string.IsNullOrEmpty(overrideExecutable))
            {
                if (string.IsNullOrEmpty(profile.WindowsData?.StartupLookupRegex))
                {
                    GravitonNotify.Notify("graviton.findemulator.nostartupregex", "Selected profile has no execuatable regex, cannot find emulator", GravitonSeverity.Error);
                    return null;
                }

                var foundExecutable = Directory.EnumerateFiles(installDir, "*.*", SearchOption.TopDirectoryOnly).FirstOrDefault(file => Regex.IsMatch(Path.GetFileName(file), profile.WindowsData.StartupLookupRegex, RegexOptions.IgnoreCase));
                if (string.IsNullOrEmpty(foundExecutable))
                {
                    GravitonNotify.Notify("graviton.findemulator.notfound", "No emulator matching the profile's executable regex was found!", GravitonSeverity.Error);
                    return null;
                }

                return foundExecutable;
            }
            else
            {
                // Check if user has put the full path to the executable in the override box
                if (File.Exists(overrideExecutable))
                    return overrideExecutable;

                // Find executable based on the filename.ext or regex that the user has put into the override box
                var foundExecutable = Directory.EnumerateFiles(installDir, "*.*", SearchOption.TopDirectoryOnly).FirstOrDefault(file => Regex.IsMatch(Path.GetFileName(file), overrideExecutable, RegexOptions.IgnoreCase));
                if (string.IsNullOrEmpty(foundExecutable))
                {
                    GravitonNotify.Notify("graviton.findemulator.notfound", "No emulator matching the profile's executable regex was found!", GravitonSeverity.Error);
                    return null;
                }

                return foundExecutable;

            }
        }

    }
}
