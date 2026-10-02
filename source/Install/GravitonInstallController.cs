using Emunight;

using Graviton.Install.Downloads;
using Graviton.Models;
using Graviton.Models.Install;
using Graviton.Models.Notifications;
using Graviton.Models.RomM.Rom;
using Graviton.Notifications;

using Playnite;

using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;

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

            var response = await GravitonPlugin.RomMServer.GETAsync($"/api/roms/{GameData.Id}");
            if (response == null)
            {
                await CancelInstall();
                return;
            }

            try
            {
                var rom = JsonSerializer.Deserialize<RomMRom>(response);
                if (rom == null)
                    throw new Exception("ROM is null");

                await DownloadInstallROM(rom);

                Task? previousInstallTask = null;

                if (GameData.Mapping?.UpdateInstallStyle != InstallStyles.None)
                    previousInstallTask = await BuildUpdateDLCRequests(rom, "update", previousInstallTask);

                if (GameData.Mapping?.DLCInstallStyle != InstallStyles.None)
                    previousInstallTask = await BuildUpdateDLCRequests(rom, "dlc", previousInstallTask);

            }
            catch (Exception)
            {
                await CancelInstall();
                return;
            }
  
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

            var req = new DownloadRequest
            {
                Id = GameData.Id.ToString(),
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


        private async Task<List<UpdateDLCCandidate>?> SelectCandidatesWindow(List<UpdateDLCCandidate> candidates, InstallMode mode, string category)
        {
            var window = GravitonPlugin.PlayniteApi.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = false,
                ShowMaximizeButton = true,
                ShowCloseButton = true,
                DefaultWidth = 800,
                DefaultHeight = 450
            });

            var selector = new CandidateSelector(candidates, mode, category);

            window.Title = $"Install {category}";
            window.Content = selector;
            window.Owner = GravitonPlugin.PlayniteApi.GetLastActiveWindow();
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();

            if (selector.Cancelled)
                return null;

            return selector.Candidates.ToList();
        }

        private async Task<Task?> BuildUpdateDLCRequests(RomMRom ROM, string category, Task? previousInstall = null)
        {
            if (!ROM.Files.Any(x => x.Category == category))
            {
                _logger.Trace($"No {category} files found, skipping download install");
                return previousInstall;
            }

            if (GameData.Mapping == null)
            {
                _logger.Trace($"Game data mapping is null, skipping {category} download/install");
                return previousInstall;
            }

            var candidates = await InstallUpdateDLC.FindCategoryCandidates(GameData.Mapping, ROM, category);
            if (candidates == null)
            {
                _logger.Trace($"No candidates found, skipping {category} download/install");
                return previousInstall;
            }

            var installmode = category == "update" ? GameData.Mapping.UpdateInstallMode : GameData.Mapping.DLCInstallMode;

            if (installmode != InstallMode.All)
            {
                candidates = await SelectCandidatesWindow(candidates, installmode, category);
                if (candidates == null || !candidates.Any(x => x.IsSelected))
                {
                    _logger.Trace($"No candidates were selected, skipping {category} download/install");
                    return previousInstall;
                }
            }
            else if (installmode == InstallMode.All)
            {
                candidates.ForEach(x => x.IsSelected = true);
            }

            foreach (var candidate in candidates.Where(x => x.IsSelected))
            {
                var reqID = Guid.NewGuid().ToString();
                var installStyle = category == "update" ? GameData.Mapping.UpdateInstallStyle : GameData.Mapping.DLCInstallStyle;

                var req = new DownloadRequest
                {
                    Id = reqID,
                    DisplayName = $"{Game.Name} - {candidate.Name}",

                    DownloadUrl = $"/api/roms/{ROM.Id}/content/{candidate.Name}?file_ids={string.Join(',', candidate.FileIDs)}",
                    DownloadPath = Path.Combine(_plugin.PluginDataPath, "temp", reqID, candidate.FileName),

                    // In CLI mode candidates should be installed one after the other 
                    WaitForInstall = (installStyle == InstallStyles.CLI) || (installmode == InstallMode.Sequential) ? previousInstall : null,

                    OnDownloadComplete = async (item, req) =>
                    {
                        if (req.WaitForInstall != null)
                        {
                            item.SetProgress(0, 1, true);
                            item.SetStatus(DownloadStatus.Waiting, Loc.GetString("DownloadStatusWaiting"));
                            await req.WaitForInstall;
                        }
                           
                        await InstallCandidate(item, req, candidate, installStyle, category);
                    },

                    OnCancelled = async () =>
                    {

                    },

                    OnFailed = async ex =>
                    {
                        GravitonNotify.Notify($"graviton.install.{reqID}.failed", Loc.GetString("DownloadFailed", ("GameName", $"{Game.Name} - {candidate.Name}"), ("Error", ex.Message)), GravitonSeverity.Error, ex);
                    }
                };

                // Set previous install to this request being installed
                previousInstall = req.InstallCompletion.Task;

                _plugin.DownloadQueueController?.Enqueue(req);
            }


            return previousInstall;
        }

        private async Task InstallCandidate(DownloadQueueItem item, DownloadRequest req, UpdateDLCCandidate candidate, InstallStyles style, string category)
        {
            string? installPath = null;

            if (style == InstallStyles.Folder || style == InstallStyles.MappedFolder)
            {
                if (category == "update" && !string.IsNullOrEmpty(GameData.Mapping?.UpdateInstallPath))
                {
                    installPath = Path.Combine(GameData.Mapping.UpdateInstallPath, Game.Name);
                }
                else if (category == "dlc" && !string.IsNullOrEmpty(GameData.Mapping?.DLCInstallPath))
                {
                    installPath = Path.Combine(GameData.Mapping.DLCInstallPath, Game.Name);
                }

                if (string.IsNullOrEmpty(installPath))
                {
                    throw new Exception($"Failed to install candidate as no install path as set/found for {category}");
                }
            }

            string workingPath = Path.Combine(Path.GetDirectoryName(req.DownloadPath)!, "extracted", req.Id);

            switch (style)
            {
                case InstallStyles.None:
                    return;

                case InstallStyles.Folder:

                    if (candidate.FileIDs.Count == 1)
                    {
                        if(!Directory.Exists(installPath))
                            Directory.CreateDirectory(installPath!);
                        
                        File.Copy(req.DownloadPath, Path.Combine(installPath!, Path.GetFileName(req.DownloadPath)), true);
                    }    
                    else
                        ArchiveExtractor.ExtractArchiveWithEntryProgress(req.DownloadPath, installPath!, item, item.Cts.Token);
                    break;

                case InstallStyles.MappedFolder:
                    MappedFolderInstall(item, req, candidate, category, workingPath);
                    break;

                case InstallStyles.CLI:
                    CLIInstall(item, req, candidate, category, workingPath);
                    break;

                default:
                    break;
            }
        }

        #region Installers
        private void CLIInstall(DownloadQueueItem item, DownloadRequest req, UpdateDLCCandidate candidate, string category, string workingPath)
        {
            // Check downloaded file need to be extracted by check the number of files IDs in the download URL
            if(candidate.FileIDs.Count > 1)
                ArchiveExtractor.ExtractArchiveWithEntryProgress(req.DownloadPath, workingPath, item, item.Cts.Token);

            var mapping = GameData.Mapping!;

            CLIInstallDefinition? definition = category == "update" ? GameData.Mapping?.UpdateCLIDefinition : category == "dlc" ? GameData.Mapping?.DLCCLIDefinition : null;
            List<DynamicArgument>? dynamicArgs = category == "update" ? GameData.Mapping?.UpdateCLIUserArgs.ToList() : category == "dlc" ? GameData.Mapping?.DLCCLIUserArgs.ToList() : null;

            if (definition == null)
                throw new Exception("Could not find definition for the category");

            List<string> files;
            // Skip any files that are not supported by the CLI definition
            if (candidate.FileIDs.Count == 1)
                files = new List<string>() { req.DownloadPath };
            else
                files = Directory.GetFiles(workingPath, "*", SearchOption.AllDirectories).Where(x => definition.SupportedExtensions.Any(y => x.EndsWith(y, StringComparison.OrdinalIgnoreCase) || (!Path.HasExtension(x) && string.Compare(y, "<none>", StringComparison.OrdinalIgnoreCase) == 0))).ToList();
           
            if (files == null || files.Count <= 0)
                throw new Exception("No supported files to install");
            
            int progress = 0;
            item.SetProgress(progress, files.Count, false);
            item.SetStatus(DownloadStatus.Installing, Loc.GetString("DownloadStatusInstallingPct", ("$Percent", 0)));

            foreach (var file in files)
            {
                progress++;

                if (GameData.Mapping?.Emulator == null || (mapping.IsImportedEmulator && mapping.Profile == null))
                    throw new Exception("Cannot find emulator and/or profile");

                string exe = "";

                if (mapping.IsImportedEmulator && mapping.Emulator != null)
                {
                    var emu = (ImportedEmulator)mapping.Emulator;
                    var profileSetting = emu.ProfileSettings?.FirstOrDefault(x => x.ProfileId == mapping.Profile?.Id);

                    if (emu == null || profileSetting == null)
                        throw new Exception("Cannot find emulator and/or profile");

                    exe = _plugin.PlayController!.FindEmulatorExecutable(emu.InstallDir ?? "", mapping.Profile!, profileSetting.Executable ?? "") ?? "";
                }
                else if (mapping.IsCustomEmulator)
                {
                    exe = File.Exists(((CustomEmulator)GameData.Mapping!.Emulator).InstallDir) ? ((CustomEmulator)GameData.Mapping!.Emulator).InstallDir! : "";
                }

                if (string.IsNullOrEmpty(exe))
                    throw new Exception("Emulator executable not found");

                var command = definition.RuntimeArgs.Replace("{FilePath}", file);

                if (dynamicArgs != null && dynamicArgs.Count > 0)
                {
                    foreach (var arg in dynamicArgs)
                    {
                        command += $" {arg.Arg.Replace("{Arg}", arg.Value)}";
                    }
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();

                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    process.WaitForExit();

                    if (process.ExitCode != 0)
                        throw new Exception($"CLI installer exited with code {process.ExitCode}" + $": {error}");
                    
                }

                item.SetProgress(progress, files.Count, false);
                item.SetStatus(DownloadStatus.Installing, Loc.GetString("DownloadStatusInstallingRatio", ("Current", progress), ("Max", files.Count)));
            }
        }

        private void MappedFolderInstall(DownloadQueueItem item, DownloadRequest req, UpdateDLCCandidate candidate, string category, string workingPath)
        {
            if (candidate.FileIDs.Count > 1)
                ArchiveExtractor.ExtractArchiveWithEntryProgress(req.DownloadPath, workingPath, item, item.Cts.Token);



            // TODO
        }

        #endregion

        private async Task CancelInstall()
        {
            var game = _playniteAPI.Library.Games.Get(Game.Id) ?? throw new Exception(Loc.GetString("InstallGameDataMissing"));
            game.InstallState = InstallState.Uninstalled;
            await _playniteAPI.Library.Games.UpdateAsync(game);

            await GameInstallationCancelledAsync(new GameInstallationCancelledArgs());
        }
    }
}