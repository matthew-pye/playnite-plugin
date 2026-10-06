using Emunight;

using Graviton.Install.Downloads;
using Graviton.Models;
using Graviton.Models.Install;
using Graviton.Models.Notifications;
using Graviton.Models.ROM;
using Graviton.Models.RomM.Rom;
using Graviton.Notifications;

using Playnite;

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace Graviton.Install
{
    public enum InstallStatus
    {
        Cancelled,
        NotInstalled,
        PartialInstalled,
        Installed,
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

                var localROM = _plugin.ImportedGames[Game.LibraryGameId!];
                await InstallUpdateDLC.RefreshCandidates(GameData.Mapping!, rom, localROM);

                if (GameData.Mapping?.UpdateInstallStyle != InstallStyles.None)
                    previousInstallTask = await BuildUpdateDLCRequests(rom, localROM.UpdateCandidates, RomMCategory.Update, previousInstallTask);

                if (GameData.Mapping?.DLCInstallStyle != InstallStyles.None)
                    previousInstallTask = await BuildUpdateDLCRequests(rom, localROM.DLCCandidates, RomMCategory.DLC, previousInstallTask);

                localROM.Save();
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
                            await ArchiveExtractor.ExtractArchiveWith7z(_plugin.Settings.PathTo7z, req.DownloadPath, installDir, item, item.Cts.Token);
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

                        CopyFileWithProgress(req.DownloadPath, installedPath, item, item.Cts.Token);

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

        #region UpdateDLC

        public static async Task InstallSingleCandidate(GameInstallInfo installInfo, UpdateDLCCandidate candidate, string category)
        {

            var reqID = Guid.NewGuid().ToString();
            var installStyle = category == RomMCategory.Update ? installInfo.Mapping!.UpdateInstallStyle : installInfo.Mapping!.DLCInstallStyle;

            // Remove any invaild characters from candidate filename
            var candidateFilename = SanitizeString(candidate.FileName);

            var req = new DownloadRequest
            {
                Id = reqID,
                DisplayName = $"{candidate.Name}",

                DownloadUrl = $"/api/roms/{installInfo.Id}/content/{Uri.EscapeDataString(candidate.Name)}?file_ids={string.Join(',', candidate.FileIDs)}",
                DownloadPath = Path.Combine(GravitonPlugin.Instance.PluginDataPath, "temp", reqID, candidateFilename),

                OnDownloadComplete = async (item, req) =>
                {
                    await InstallCandidate(item, req, candidate, installStyle, installInfo, category);

                    candidate.InstalledFileIDs = candidate.FileIDs.ToList();
                    candidate.InstalledSize = candidate.Size;
                    candidate.Status = InstallStatus.Installed;
                    candidate.PreviousInstallStyle = installStyle;

                    if(GravitonPlugin.Instance.ImportedGames.ContainsKey(installInfo.Id.ToString()))
                        GravitonPlugin.Instance.ImportedGames[installInfo.Id.ToString()].Save();
                },

                OnFailed = async ex =>
                {
                    GravitonNotify.Notify($"graviton.install.{reqID}.failed", Loc.GetString("DownloadFailed", ("GameName", $"{installInfo.FileName} - {candidate.Name}"), ("Error", ex.Message)), GravitonSeverity.Error, ex);
                }
            };


            GravitonPlugin.Instance.DownloadQueueController?.Enqueue(req);
        }

        private async Task<ObservableCollection<UpdateDLCCandidate>> SelectCandidatesWindow(ObservableCollection<UpdateDLCCandidate> candidates, InstallMode mode, string category)
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
                candidates.ForEach(x => x.IsSelected = false);

            return candidates;
        }

        private async Task<Task?> BuildUpdateDLCRequests(RomMRom ROM, ObservableCollection<UpdateDLCCandidate> candidates, string category, Task? previousInstall = null)
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

            if (candidates.Count < 1)
            {
                _logger.Trace($"No candidates found, skipping {category} download/install");
                return previousInstall;
            }

            var installmode = category == RomMCategory.Update ? GameData.Mapping.UpdateInstallMode : GameData.Mapping.DLCInstallMode;

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

            foreach (var candidate in candidates.Where(x => x.IsSelected && x.Status != InstallStatus.Installed))
            {
                var reqID = Guid.NewGuid().ToString();
                var installStyle = category == RomMCategory.Update ? GameData.Mapping.UpdateInstallStyle : GameData.Mapping.DLCInstallStyle;

                // Remove any invaild characters from candidate filename
                var candidateFilename = SanitizeString(candidate.FileName);

                var req = new DownloadRequest
                {
                    Id = reqID,
                    DisplayName = $"{Game.Name} - {candidate.Name}",

                    DownloadUrl = $"/api/roms/{ROM.Id}/content/{Uri.EscapeDataString(candidate.Name)}?file_ids={string.Join(',', candidate.FileIDs)}",
                    DownloadPath = Path.Combine(_plugin.PluginDataPath, "temp", reqID, candidateFilename),

                    // In CLI mode candidates should be installed one after the other 
                    WaitForInstall = (installStyle == InstallStyles.CLI) || (installmode == InstallMode.Sequential) ? previousInstall : null,

                    OnDownloadComplete = async (item, req) =>
                    {
                        await InstallCandidate(item, req, candidate, installStyle, GameData, category);
                        
                        candidate.InstalledFileIDs = candidate.FileIDs.ToList();
                        candidate.InstalledSize = candidate.Size;
                        candidate.Status = InstallStatus.Installed;
                        candidate.PreviousInstallStyle = installStyle;

                        if (GravitonPlugin.Instance.ImportedGames.ContainsKey(GameData.Id.ToString()))
                            GravitonPlugin.Instance.ImportedGames[GameData.Id.ToString()].Save();
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

        private static async Task InstallCandidate(DownloadQueueItem item, DownloadRequest req, UpdateDLCCandidate candidate, InstallStyles style, GameInstallInfo installInfo, string category)
        {
            
            string workingPath = Path.Combine(Path.GetDirectoryName(req.DownloadPath)!, "extracted", req.Id);

            switch (style)
            {
                case InstallStyles.None:
                    return;

                case InstallStyles.Folder:

                    string? installPath = null;

                    if (style == InstallStyles.Folder || style == InstallStyles.MappedFolder)
                    {
                        if (category == RomMCategory.Update && !string.IsNullOrEmpty(installInfo.Mapping?.UpdateInstallPath))
                        {
                            installPath = Path.Combine(installInfo.Mapping.UpdateInstallPath, SanitizeString(installInfo.GameName));
                        }
                        else if (category == RomMCategory.DLC && !string.IsNullOrEmpty(installInfo.Mapping?.DLCInstallPath))
                        {
                            installPath = Path.Combine(installInfo.Mapping.DLCInstallPath, SanitizeString(installInfo.GameName));
                        }

                        if (string.IsNullOrEmpty(installPath))
                        {
                            throw new Exception($"Failed to install candidate as no install path as set/found for {category}");
                        }
                    }

                    if (candidate.FileIDs.Count == 1)
                    {
                        if(!Directory.Exists(installPath))
                            Directory.CreateDirectory(installPath!);

                        var filePath = Path.Combine(installPath!, Path.GetFileName(req.DownloadPath));

                        CopyFileWithProgress(req.DownloadPath, filePath, item, item.Cts.Token);
                        candidate.InstalledTopPaths = [filePath];
                    }    
                    else
                    {
                        ArchiveExtractor.ExtractArchiveWithEntryProgress(req.DownloadPath, workingPath, item, item.Cts.Token);

                        CopyDirectoryWithProgress(workingPath, installPath!, item, item.Cts.Token);
                        candidate.InstalledTopPaths = GetInstalledTopPaths(workingPath, installPath!);
                    }
                        
                    break;

                case InstallStyles.MappedFolder:
                    MappedFolderInstall(item, req, candidate, installInfo, category, workingPath);
                    break;

                case InstallStyles.CLI:
                    await CLIInstall(item, req, candidate, installInfo, category, workingPath);
                    break;

                default:
                    break;
            }
        }

        private static async Task CLIInstall(DownloadQueueItem item, DownloadRequest req, UpdateDLCCandidate candidate, GameInstallInfo installInfo, string category, string workingPath)
        {
            // Check downloaded file need to be extracted by check the number of files IDs in the download URL
            if(candidate.FileIDs.Count > 1)
                ArchiveExtractor.ExtractArchiveWithEntryProgress(req.DownloadPath, workingPath, item, item.Cts.Token);

            var mapping = installInfo.Mapping!;

            CLIInstallDefinition? definition = category == RomMCategory.Update ? installInfo.Mapping?.UpdateCLIDefinition : category == RomMCategory.DLC ? installInfo.Mapping?.DLCCLIDefinition : null;
            List<DynamicArgument>? dynamicArgs = category == RomMCategory.Update ? installInfo.Mapping?.UpdateCLIUserArgs.ToList() : category == RomMCategory.DLC ? installInfo.Mapping?.DLCCLIUserArgs.ToList() : null;

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

                if (installInfo.Mapping?.Emulator == null || (mapping.IsImportedEmulator && mapping.Profile == null))
                    throw new Exception("Cannot find emulator and/or profile");

                string exe = "";

                if (mapping.IsImportedEmulator && mapping.Emulator != null)
                {
                    var emu = (ImportedEmulator)mapping.Emulator;
                    var profileSetting = emu.ProfileSettings?.FirstOrDefault(x => x.ProfileId == mapping.Profile?.Id);

                    if (emu == null || profileSetting == null)
                        throw new Exception("Cannot find emulator and/or profile");

                    exe = GravitonPlugin.Instance.PlayController!.FindEmulatorExecutable(emu.InstallDir ?? "", mapping.Profile!, profileSetting.Executable ?? "") ?? "";
                }
                else if (mapping.IsCustomEmulator)
                {
                    exe = File.Exists(((CustomEmulator)installInfo.Mapping!.Emulator).InstallDir) ? ((CustomEmulator)installInfo.Mapping!.Emulator).InstallDir! : "";
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

                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();

                    while (!process.HasExited)
                    {
                        if (item.Cts.IsCancellationRequested)
                        {
                            try
                            {
                                process.Kill(true);
                            }
                            catch {}

                            item.Cts.Token.ThrowIfCancellationRequested();
                        }

                        await Task.Delay(100, item.Cts.Token);
                    }

                    var output = await outputTask;
                    var error = await errorTask;

                    process.WaitForExit();

                    if (process.ExitCode != 0)
                        throw new Exception($"CLI installer exited with code {process.ExitCode}" + $": {error}");
                    
                }

                item.SetProgress(progress, files.Count, false);
                item.SetStatus(DownloadStatus.Installing, Loc.GetString("DownloadStatusInstallingRatio", ("Current", progress), ("Max", files.Count)));
            }
        }

        private static void MappedFolderInstall(DownloadQueueItem item, DownloadRequest req, UpdateDLCCandidate candidate, GameInstallInfo installInfo, string category, string workingPath)
        {
            TitleIDInstallDefinition? definition = category == RomMCategory.Update ? installInfo.Mapping?.UpdateTitleIDDefinition : category == RomMCategory.DLC ? installInfo.Mapping?.DLCTitleIDDefinition : null;
            string? installPath = category == RomMCategory.Update ? installInfo.Mapping?.UpdateInstallPath : category == RomMCategory.DLC ? installInfo.Mapping?.DLCInstallPath : null;

            if (definition == null)
                throw new Exception("Could not find definition for the category");

            if (installPath == null)
                throw new Exception("No install path set for the category");

            var installLocation = definition.RuntimeArgs.Replace("{FolderPath}", installPath)
                                                        .Replace("{TitleID}", installInfo.TitleID)
                                                        .Replace("{SaveTarget}", installInfo.SaveTarget)
                                                        .Replace("{CandidateName}", SanitizeString(candidate.Name));

            if (string.IsNullOrEmpty(candidate.SingleFileRelativePath))
            {
                ArchiveExtractor.ExtractArchiveWithEntryProgress(req.DownloadPath, workingPath, item, item.Cts.Token);

                if (!string.IsNullOrEmpty(candidate.CandidateRoot))
                    workingPath = Path.Combine(workingPath, candidate.CandidateRoot);

                if (!Directory.Exists(workingPath))
                    throw new DirectoryNotFoundException($"Candidate root was not found after extraction: {workingPath}");

                CopyDirectoryWithProgress(workingPath, installLocation, item, item.Cts.Token);
                candidate.InstalledTopPaths = GetInstalledTopPaths(workingPath, installLocation);
            }
            else
            {
                installLocation = Path.Combine(installLocation, candidate.SingleFileRelativePath);

                if (!Directory.Exists(Path.GetDirectoryName(installLocation)))
                    Directory.CreateDirectory(Path.GetDirectoryName(installLocation)!);

                CopyFileWithProgress(req.DownloadPath, installLocation, item, item.Cts.Token);
                candidate.InstalledTopPaths = new([installLocation]);
            }
        }

        #endregion

        private static void CopyFileWithProgress(string source, string destination, DownloadQueueItem item, CancellationToken token)
        {
            item.SetStatus(DownloadStatus.Installing, Loc.GetString("DownloadStatusInstalling"));

            const int bufferSize = 1024 * 1024;

            var destinationExisted = File.Exists(destination);

            if (!string.IsNullOrEmpty(Path.GetDirectoryName(destination))) 
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            try
            {
                using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan);
                using var destinationStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.SequentialScan);

                var buffer = new byte[bufferSize];

                long totalBytes = sourceStream.Length;
                long copiedBytes = 0;

                int read;

                while ((read = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();

                    destinationStream.Write(buffer, 0, read);
                    copiedBytes += read;

                    item.SetProgress(copiedBytes, totalBytes, false);
                    item.SetStatus(DownloadStatus.Installing, Loc.GetString("DownloadStatusInstallingPct", ("$Percent", (double)copiedBytes / totalBytes * 100)));
                }
            }
            catch
            {
                // Only delete it if this operation created the file.
                if (!destinationExisted)
                {
                    try
                    {
                        if (File.Exists(destination))
                            File.Delete(destination);
                    }
                    catch {}
                }

                throw;
            }
        }

        private static void CopyDirectoryWithProgress(string sourceDirectory, string destinationDirectory, DownloadQueueItem item, CancellationToken token)
        {
            if (!Directory.Exists(sourceDirectory))
                throw new DirectoryNotFoundException($"Source directory does not exist: {sourceDirectory}");
            
            item.SetStatus(DownloadStatus.Installing, Loc.GetString("DownloadStatusInstalling"));

            var files = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories).ToList();

            var createdFiles = new List<string>();

            try
            {
                int currentFileCount = 0;
                item.SetProgress(currentFileCount, files.Count, false);

                foreach (var sourceFile in files)
                {
                    token.ThrowIfCancellationRequested();

                    var relativePath = Path.GetRelativePath(sourceDirectory, sourceFile);

                    var destinationFile = Path.Combine(destinationDirectory, relativePath);

                    if (!string.IsNullOrEmpty(Path.GetDirectoryName(destinationFile)))
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
                    
                    var existed = File.Exists(destinationFile);

                    File.Copy(sourceFile, destinationFile, true);

                    if (!existed)
                        createdFiles.Add(destinationFile);

                    currentFileCount++;
                    item.SetProgress(currentFileCount, files.Count, false);
                    item.SetStatus(DownloadStatus.Installing, Loc.GetString("DownloadStatusInstallingRatio", ("$Current", currentFileCount), ("$Max", files.Count)));
                }
            }
            catch
            {
                ArchiveExtractor.CleanupCreatedFiles(createdFiles);
                throw;
            }
        }

        private static List<string> GetInstalledTopPaths(string sourceDirectory, string destinationDirectory)
        {
            var paths = new List<string>();

            foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                paths.Add(Path.Combine(destinationDirectory, Path.GetFileName(directory)));
            }

            foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                paths.Add(Path.Combine(destinationDirectory, Path.GetFileName(file)));
            }

            return paths;
        }

        private static string SanitizeString(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = string.Concat(value.Where(c => !invalid.Contains(c))).Trim();

            return string.IsNullOrWhiteSpace(sanitized) ? "Unnamed" : sanitized;
        }
    }
}