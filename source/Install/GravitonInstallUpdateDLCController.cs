using Emunight;

using Graviton.Install.Downloads;
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

    public class GravitonInstallUpdateDLCController
    {
        private IGravitonContext _plugin;
        private IPlayniteApi _playniteAPI;
        private GravitonLogger _logger;
        private IRomMServer _romMServer;

        internal GravitonInstallUpdateDLCController(IGravitonContext plugin, IPlayniteApi playniteAPI, GravitonLogger logger, IRomMServer server)
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;
            _romMServer = server;

        }

        internal async Task InstallSingleCandidate(GameInstallInfo installInfo, UpdateDLCCandidate candidate, string category)
        {

            var reqID = Guid.NewGuid().ToString();
            var installStyle = category == RomMCategory.Update ? installInfo.Mapping!.UpdateInstallStyle : installInfo.Mapping!.DLCInstallStyle;

            // Remove any invaild characters from candidate filename
            var candidateFilename = GravitonInstallHelpers.SanitizeString(candidate.FileName);

            DownloadRequestBackup backup = new()
            {
                InstallType = InstallType.UpdateDLC,
                GameID = installInfo.Id.ToString(),
                ID = reqID,
                DownloadPath = Path.Combine(GravitonPlugin.Instance.PluginDataPath, "temp", reqID, candidateFilename),

                Candidate = candidate,
                Category = category,
                InstallStyle = installStyle,
            };

            var req = CreateUpdateDLCRequest(installInfo, candidate, category, installStyle, backup);

            GravitonPlugin.Instance.DownloadQueueController?.Enqueue(req, backup);
        }

        internal async Task RecoverUpdateDLCInstall(List<DownloadRequestBackup> requests)
        {
            List<(DownloadRequest req, DownloadRequestBackup backup)> downloadRequests = new();

            foreach (var request in requests)
            {
                if (!GravitonPlugin.Instance.ImportedGames.ContainsKey(request.GameID))
                    throw new Exception(Loc.GetString("InstallGameIdNotFound", ("GameID", request.GameID ?? "")));

                var localROM = GravitonPlugin.Instance.ImportedGames[request.GameID];
                var mapping = GravitonPlugin.Instance.Settings.Mappings.FirstOrDefault(x => x.MappingId == localROM.MappingID);

                if (mapping == null)
                    throw new Exception(Loc.GetString("InstallMappingNotFound"));

                var installInfo = GameInstallInfo.Build(localROM, mapping);
                GravitonPlugin.Logger?.Trace($"Created install info\n{JsonSerializer.Serialize(installInfo, new JsonSerializerOptions { WriteIndented = true })}");

                if (request.Candidate == null)
                    throw new Exception($"No candidate found in {request.ID}.tmp");

                if (request.InstallStyle == null)
                    throw new Exception($"No install style found in {request.ID}.tmp");

                if (string.IsNullOrEmpty(request.Category))
                    throw new Exception($"No category set in {request.ID}.tmp");

                var candidates = request.Category == RomMCategory.Update ? localROM.UpdateCandidates : localROM.DLCCandidates;
                var candidate = candidates.FirstOrDefault(x => x.Category == request.Category &&
                                                               x.Name == request.Candidate.Name &&
                                                               x.CandidateRoot == request.Candidate.CandidateRoot &&
                                                               x.FileIDs.OrderBy(id => id).SequenceEqual(request.Candidate.FileIDs.OrderBy(id => id)));

                if (candidate == null)
                    throw new Exception($"Cannot restore candidate for {request.ID}");

                downloadRequests.Add((CreateUpdateDLCRequest(installInfo, candidate, request.Category, request.InstallStyle ?? InstallStyles.None, request), request));
            }

            foreach (var request in downloadRequests)
            {
                var previousInstall = downloadRequests.FirstOrDefault(x => x.req.Id == request.backup.PreviousRequestId);

                if(previousInstall.req != null)
                    request.req.WaitForInstall = previousInstall.req.InstallCompletion.Task;

                GravitonPlugin.Instance.DownloadQueueController?.Enqueue(request.req, request.backup);
            } 
        }

        internal async Task<DownloadRequest?> BuildUpdateDLCRequests(GameInstallInfo GameData, RomMRom ROM, ObservableCollection<UpdateDLCCandidate> candidates, string category, DownloadRequest? previousInstall = null)
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

                previousInstall = installmode == InstallMode.Sequential || installStyle == InstallStyles.CLI ? previousInstall : null;

                // Remove any invaild characters from candidate filename
                var candidateFilename = GravitonInstallHelpers.SanitizeString(candidate.FileName);

                DownloadRequestBackup backup = new()
                {
                    InstallType = InstallType.UpdateDLC,
                    GameID = GameData.Id.ToString(),
                    ID = reqID,
                    DownloadPath = Path.Combine(_plugin.PluginDataPath, "temp", reqID, candidateFilename),

                    Candidate = candidate,
                    Category = category,
                    InstallStyle = installStyle,

                    PreviousRequestId = previousInstall?.Id
                };

                var req = CreateUpdateDLCRequest(GameData, candidate, category, installStyle, backup, previousInstall?.InstallCompletion.Task);

                // Set previous install to this request being installed
                previousInstall = req;

                _plugin.DownloadQueueController?.Enqueue(req, backup);
            }

            return previousInstall;
        }
        private DownloadRequest CreateUpdateDLCRequest(GameInstallInfo installInfo, UpdateDLCCandidate candidate, string category, InstallStyles style, DownloadRequestBackup backup, Task? previousInstall = null)
        {
            var req = new DownloadRequest
            {
                Id = backup.ID,
                DisplayName = $"{installInfo.GameName} - {candidate.Name}",

                DownloadUrl = $"/api/roms/{installInfo.Id}/content/{Uri.EscapeDataString(candidate.Name)}?file_ids={string.Join(',', candidate.FileIDs)}",
                DownloadPath = backup.DownloadPath,

                WaitForInstall = previousInstall,

                OnDownloadComplete = async (item, request) =>
                {
                    await InstallCandidate(item, request, candidate, style, installInfo, category);

                    candidate.InstalledFileIDs = candidate.FileIDs.ToList();
                    candidate.InstalledSize = candidate.Size;
                    candidate.Status = InstallStatus.Installed;
                    candidate.PreviousInstallStyle = style;

                    if (_plugin.ImportedGames.TryGetValue(backup.GameID, out var localROM))
                    {
                        localROM.Save(_plugin);
                    }
                },

                OnFailed = async ex =>
                {
                    GravitonNotify.Notify($"graviton.install.{backup.ID}.failed", Loc.GetString("DownloadFailed", ("GameName", $"{installInfo.GameName} - {candidate.Name}"), ("Error", ex.Message)), GravitonSeverity.Error, ex);
                }
            };

            return req;
        }

        private async Task<ObservableCollection<UpdateDLCCandidate>> SelectCandidatesWindow(ObservableCollection<UpdateDLCCandidate> candidates, InstallMode mode, string category)
        {
            var window = _playniteAPI.CreateWindow(new WindowCreationOptions
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
            window.Owner = _playniteAPI.GetLastActiveWindow();
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();

            if (selector.Cancelled)
                candidates.ForEach(x => x.IsSelected = false);

            return candidates;
        }

        private async Task InstallCandidate(DownloadQueueItem item, DownloadRequest req, UpdateDLCCandidate candidate, InstallStyles style, GameInstallInfo installInfo, string category)
        {
            item.Cts.Token.ThrowIfCancellationRequested();

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
                            installPath = Path.Combine(installInfo.Mapping.UpdateInstallPath, GravitonInstallHelpers.SanitizeString(installInfo.GameName));
                        }
                        else if (category == RomMCategory.DLC && !string.IsNullOrEmpty(installInfo.Mapping?.DLCInstallPath))
                        {
                            installPath = Path.Combine(installInfo.Mapping.DLCInstallPath, GravitonInstallHelpers.SanitizeString(installInfo.GameName));
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

                        GravitonInstallHelpers.CopyFileWithProgress(req.DownloadPath, filePath, item, item.Cts.Token);
                        candidate.InstalledTopPaths = [filePath];
                    }    
                    else
                    {
                        ArchiveExtractor.ExtractArchiveWithEntryProgress(req.DownloadPath, workingPath, item, item.Cts.Token);

                        GravitonInstallHelpers.CopyDirectoryWithProgress(workingPath, installPath!, item, item.Cts.Token);
                        candidate.InstalledTopPaths = GravitonInstallHelpers.GetInstalledTopPaths(workingPath, installPath!);
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
        private async Task CLIInstall(DownloadQueueItem item, DownloadRequest req, UpdateDLCCandidate candidate, GameInstallInfo installInfo, string category, string workingPath)
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
        private void MappedFolderInstall(DownloadQueueItem item, DownloadRequest req, UpdateDLCCandidate candidate, GameInstallInfo installInfo, string category, string workingPath)
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
                                                        .Replace("{CandidateName}", GravitonInstallHelpers.SanitizeString(candidate.Name));

            if (string.IsNullOrEmpty(candidate.SingleFileRelativePath))
            {
                ArchiveExtractor.ExtractArchiveWithEntryProgress(req.DownloadPath, workingPath, item, item.Cts.Token);

                if (!string.IsNullOrEmpty(candidate.CandidateRoot))
                    workingPath = Path.Combine(workingPath, candidate.CandidateRoot);

                if (!Directory.Exists(workingPath))
                    throw new DirectoryNotFoundException($"Candidate root was not found after extraction: {workingPath}");

                GravitonInstallHelpers.CopyDirectoryWithProgress(workingPath, installLocation, item, item.Cts.Token);
                candidate.InstalledTopPaths = GravitonInstallHelpers.GetInstalledTopPaths(workingPath, installLocation);
            }
            else
            {
                installLocation = Path.Combine(installLocation, candidate.SingleFileRelativePath);

                if (!Directory.Exists(Path.GetDirectoryName(installLocation)))
                    Directory.CreateDirectory(Path.GetDirectoryName(installLocation)!);

                GravitonInstallHelpers.CopyFileWithProgress(req.DownloadPath, installLocation, item, item.Cts.Token);
                candidate.InstalledTopPaths = new([installLocation]);
            }
        }
    }
}