using Emunight;

using Graviton.Models;
using Graviton.Models.ROM;
using Graviton.Models.RomM.Saves;
using Graviton.Models.Saves;
using Graviton.Models.Saves.Discovery;
using Graviton.Notifications;

using Playnite;

using System.IO;
using System.IO.Enumeration;
using System.Text.Json;

namespace Graviton.Saves
{
    public class SaveDiscoverer
    {

        private IGravitonContext _plugin;
        private IPlayniteApi _playniteAPI;
        private GravitonLogger _logger;
        private IRomMServer _romMServer;


        internal SaveDiscoverer(IGravitonContext plugin, IPlayniteApi playniteAPI, GravitonLogger logger, IRomMServer romMServer)
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;
            _romMServer = romMServer;
        }

        public async Task<bool> RefreshPlaythroughs(List<RomMRomLocal> ROMs)
        {
            try
            {
                // Return if non of the roms have a set channel file id
                if (!ROMs.Any(x => x.ChannelFileId != null))
                    return true;

                List<RomMChannel> playthroughs = new();
                foreach (var roms in ROMs.Where(x => x.ChannelFileId != null).Distinct().Chunk(96))
                {
                    var urlPath = $"/api/channels?rom_file_id={string.Join("&rom_file_id=", roms.Select(y => y.ChannelFileId))}";
                    var result = await _romMServer.GETAsync(urlPath);

                    if (result == null)
                        return false;

                    var channels = JsonSerializer.Deserialize<List<RomMChannel>>(result);
                    if (channels == null)
                        return false;

                    playthroughs.AddRange(channels);
                }


                foreach (var rom in ROMs)
                {
                    if (rom.ChannelFileId == null)
                        continue;

                    // Check for missing channels on the server
                    var deletedPlaythroughs = rom.Playthroughs.Where(x => !playthroughs.Any(y => y.Id == x.ChannelId));

                    foreach (var playthrough in deletedPlaythroughs)
                    {
                        // Skip channels that are pending to be created
                        if (playthrough.Status == ChannelStatus.PendingCreate)
                            continue;

                        playthrough.Status = ChannelStatus.Deleted;
                        playthrough.IsActive = false;

                        playthrough.LastSeenRemoteCurrentId = null;
                        playthrough.LastSeenRemoteCurrentCreatedAt = null;

                        playthrough.LastSeenRemoteUpdatedAt = null;
                        playthrough.LastSeenRemoteAt = null;
                    }

                    foreach (var playthrough in playthroughs.Where(x => x.RomFileId == rom.ChannelFileId))
                    {
                        var romPlaythrough = rom.Playthroughs.FirstOrDefault(x => x.ChannelId == playthrough.Id);
                        var romPendingPlaythrough = rom.Playthroughs.FirstOrDefault(x => x.Status == ChannelStatus.PendingCreate && x.Name.Equals(playthrough.Label, StringComparison.OrdinalIgnoreCase));

                        if (romPlaythrough != null)
                        {
                            romPlaythrough.Name = playthrough.Label;
                            romPlaythrough.Status = ChannelStatus.Available;

                            romPlaythrough.LastSeenRemoteCurrentId = playthrough.Current?.Id;
                            romPlaythrough.LastSeenRemoteCurrentCreatedAt = playthrough.Current?.CreatedAt;

                            romPlaythrough.LastSeenRemoteUpdatedAt = playthrough.UpdatedAt;
                            romPlaythrough.LastSeenRemoteAt = DateTimeOffset.UtcNow;
                        }
                        else if(romPendingPlaythrough != null)
                        {
                            // TODO add update for pending push to use the new channel id 

                            romPendingPlaythrough.ChannelId = playthrough.Id;
                            romPendingPlaythrough.Name = playthrough.Label;
                            romPendingPlaythrough.Status = ChannelStatus.Available;

                            romPendingPlaythrough.LastSeenRemoteCurrentId = playthrough.Current?.Id;
                            romPendingPlaythrough.LastSeenRemoteCurrentCreatedAt = playthrough.Current?.CreatedAt;

                            romPendingPlaythrough.LastSeenRemoteUpdatedAt = playthrough.UpdatedAt;
                            romPendingPlaythrough.LastSeenRemoteAt = DateTimeOffset.UtcNow;
                        }
                        else
                        {
                            var newPlaythrough = new PlaythroughLocal
                            {
                                ChannelId = playthrough.Id,
                                Name = playthrough.Label,
                                Status = ChannelStatus.Available,

                                HeldSnapshotId = null,
                                HeldHash = null,
                                UnresolvedBranchId = null,

                                LastSeenRemoteCurrentId = playthrough.Current?.Id,
                                LastSeenRemoteCurrentCreatedAt = playthrough.Current?.CreatedAt,

                                LastSeenRemoteUpdatedAt = playthrough.UpdatedAt,
                                LastSeenRemoteAt = DateTimeOffset.UtcNow
                            };

                            rom.Playthroughs.Add(newPlaythrough);
                        }
                    }

                    // Set a default active playthrough on refresh based on last update at (Might remove this for more control)
                    if (!rom.Playthroughs.Any(x => x.IsActive))
                    {
                        var playthrough = rom.Playthroughs.OrderByDescending(x => x.LastSeenRemoteUpdatedAt).Where(y => y.Status != ChannelStatus.Deleted).FirstOrDefault();
                        playthrough?.IsActive = true;
                    }

                    // Check if current save for current playthrough is in sync
                    //if(rom.CurrentSave.)
                    //{
                    //
                    //}

                    rom.Save();

                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to refresh playthroughs: {ex}");
                return false;
            }
        }

        public List<DiscoveredSave>? DiscoverLocalSaves(EmulatorMapping mapping)
        {
            if (string.IsNullOrEmpty(mapping.SaveLocation))
                return null;

            if(!Directory.Exists(mapping.SaveLocation))
                return null;

            var saveType = mapping.SaveType != SaveLayoutTypes.Auto ? mapping.SaveType : mapping.FoundSaveType;

            if (saveType == SaveLayoutTypes.Folder)
                return FindFolderSaves(mapping);

            if (saveType == SaveLayoutTypes.Single || saveType == SaveLayoutTypes.Multi)
                return FindFileSaves(mapping);

            if (saveType == SaveLayoutTypes.Card)
                return FindCardSaves(mapping);

            return null;
        }

        public SaveLayoutTypes? FindLayoutType(EmulatorMapping mapping)
        {
            var fileSaves = FindFileSaves(mapping);
            var folderSaves = FindFolderSaves(mapping);
            var cardsSaves = FindCardSaves(mapping);

            var fileSavesLastWrite = fileSaves?.Select(x => x.LatestFileWrite).OrderByDescending(y => y).FirstOrDefault();
            var folderSavesLastWrite = folderSaves?.Select(x => x.LatestFileWrite).OrderByDescending(y => y).FirstOrDefault();
            var cardSavesLastWrite = cardsSaves?.Select(x => x.LatestFileWrite).OrderByDescending(y => y).FirstOrDefault();

            if (cardSavesLastWrite > folderSavesLastWrite && cardSavesLastWrite > fileSavesLastWrite)
                return SaveLayoutTypes.Card;


            if (folderSavesLastWrite > fileSavesLastWrite && folderSavesLastWrite > cardSavesLastWrite)
                return SaveLayoutTypes.Folder;


            var singleSaveCount = fileSaves?.Where(x => x.Type == SaveLayoutTypes.Single).Count() ?? 0;
            var multiSaveCount = fileSaves?.Where(x => x.Type == SaveLayoutTypes.Multi).Count() ?? 0;

            if (multiSaveCount > 0)
                return SaveLayoutTypes.Multi;

            if (singleSaveCount > 0)
                return SaveLayoutTypes.Single;

            return null;
        }


        private List<DiscoveredSave>? FindFolderSaves(EmulatorMapping mapping)
        {
            var ROMs = _plugin.ImportedGames.Where(x => x.Value.MappingID == mapping.MappingId).Select(y => y.Value).ToList();

            List<DiscoveredSave> discoveredSaves = new();

            foreach (var folder in Directory.GetDirectories(mapping.SaveLocation!, "*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true }))
            {
                var folderName = Path.GetFileName(folder);

                var matchedROMs = ROMs.Where(x => (x.SaveTargetLayout == "folder-exact" && !string.IsNullOrEmpty(x.SaveTarget) && folderName.Equals(x.SaveTarget, StringComparison.OrdinalIgnoreCase)) ||
                                                  (x.SaveTargetLayout == "folder-prefix" && !string.IsNullOrEmpty(x.SaveTarget) && folderName.StartsWith(x.SaveTarget, StringComparison.OrdinalIgnoreCase)) ||
                                                  (x.SaveTargetLayout == "folder-split" && !string.IsNullOrEmpty(x.SaveTarget) && folder.EndsWith(x.SaveTarget.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase)))
                                      .Select(y => new DiscoveryCandidate { Rom = y, MatchedStyle = SaveMatchStyle.SaveTarget }).ToList();

                var filenameMatchedROMs = ROMs.Where(x => folderName.Equals(Path.GetFileNameWithoutExtension(x.FileName), StringComparison.OrdinalIgnoreCase))
                                              .Select(y => new DiscoveryCandidate { Rom = y, MatchedStyle = SaveMatchStyle.Filename }).ToList();

                foreach (var candidate in filenameMatchedROMs)
                {
                    var duplicateCandidates = matchedROMs.Where(x => x.Rom.Id == candidate.Rom.Id);

                    if (duplicateCandidates == null || duplicateCandidates.Count() < 1)
                    {
                        matchedROMs.Add(candidate);
                    }     
                    else
                    {
                        foreach (var duplicateCandidate in duplicateCandidates)
                        {
                            duplicateCandidate.MatchedStyle = SaveMatchStyle.Both;
                        }
                    }
                }

                DiscoveredSave? discoveredFolderSave = null;

                if (matchedROMs.Count() > 0)
                {
                    discoveredFolderSave = new DiscoveredSave
                    {
                        MappingId = mapping.MappingId,
                        Paths = { folder },
                        Type = SaveLayoutTypes.Folder,
                        SizeBytes = 0,
                        FileCount = 0,
                        Candidates = matchedROMs.ToList(),
                        Status = DiscoveryStatus.Pending
                    };

                    foreach (var file in Directory.EnumerateFiles(folder, "*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true }))
                    {
                        var fileInfo = new FileInfo(file);
                        discoveredFolderSave.FileCount++;
                        discoveredFolderSave.SizeBytes += fileInfo.Length;
                        
                        if(fileInfo.LastWriteTimeUtc > discoveredFolderSave.LatestFileWrite)
                            discoveredFolderSave.LatestFileWrite = fileInfo.LastWriteTimeUtc;
                    }

                    discoveredSaves.Add(discoveredFolderSave);
                }
            }

            return discoveredSaves;
        }

        private List<DiscoveredSave>? FindFileSaves(EmulatorMapping mapping)
        {
            List<DiscoveredSave> discoveredSaves = new();

            var ROMs = _plugin.ImportedGames.Where(x => x.Value.MappingID == mapping.MappingId).Select(y => y.Value).ToList();
            var files = Directory.EnumerateFiles(mapping.SaveLocation!, "*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true }).ToList();

            if (mapping.WhitelistExtensions.Count() > 0)
                files = files.Where(x => mapping.WhitelistExtensions.Any(y => Path.GetExtension(x).Equals($".{y.TrimStart('.')}", StringComparison.OrdinalIgnoreCase))).ToList();

            foreach (var rom in ROMs)
            {
                var matchedTargetFiles = files.Where(x => (rom.SaveTargetLayout == "file-exact" && !string.IsNullOrEmpty(rom.SaveTarget) && Path.GetFileNameWithoutExtension(x).Equals(rom.SaveTarget, StringComparison.OrdinalIgnoreCase)) ||
                                                       (rom.SaveTargetLayout == "file-prefix" && !string.IsNullOrEmpty(rom.SaveTarget) && Path.GetFileName(x).StartsWith(rom.SaveTarget, StringComparison.OrdinalIgnoreCase))).ToList();

                var matchedNameFiles = files.Where(x => Path.GetFileNameWithoutExtension(x).Equals(Path.GetFileNameWithoutExtension(rom.FileName), StringComparison.OrdinalIgnoreCase)).ToList();

                if (matchedTargetFiles.Count == 1 && matchedNameFiles.Count == 1 && matchedTargetFiles.SequenceEqual(matchedNameFiles, StringComparer.OrdinalIgnoreCase))
                {
                    discoveredSaves.Add(new DiscoveredSave
                    {
                        MappingId = mapping.MappingId,
                        Paths = matchedNameFiles,
                        Type = SaveLayoutTypes.Single,
                        SizeBytes = new FileInfo(matchedNameFiles[0]).Length,
                        FileCount = 1,
                        LatestFileWrite = new FileInfo(matchedNameFiles[0]).LastWriteTimeUtc,
                        Candidates = new List<DiscoveryCandidate> { new DiscoveryCandidate { Rom = rom, MatchedStyle = SaveMatchStyle.Both } },
                        Status = DiscoveryStatus.Pending
                    });
                }
                else if (matchedTargetFiles.Count == 1 && matchedNameFiles.Count == 0)
                {
                    discoveredSaves.Add(new DiscoveredSave 
                    { 
                        MappingId = mapping.MappingId,
                        Paths = matchedTargetFiles,
                        Type = SaveLayoutTypes.Single,
                        SizeBytes = new FileInfo(matchedTargetFiles[0]).Length,
                        FileCount = 1,
                        LatestFileWrite = new FileInfo(matchedTargetFiles[0]).LastWriteTimeUtc,
                        Candidates = new List<DiscoveryCandidate> { new DiscoveryCandidate{ Rom = rom, MatchedStyle = SaveMatchStyle.SaveTarget } },
                        Status = DiscoveryStatus.Pending
                    });
                }
                else if(matchedTargetFiles.Count == 0 && matchedNameFiles.Count == 1)
                {
                    discoveredSaves.Add(new DiscoveredSave
                    {
                        MappingId = mapping.MappingId,
                        Paths = matchedNameFiles,
                        Type = SaveLayoutTypes.Single,
                        SizeBytes = new FileInfo(matchedNameFiles[0]).Length,
                        FileCount = 1,
                        LatestFileWrite = new FileInfo(matchedNameFiles[0]).LastWriteTimeUtc,
                        Candidates = new List<DiscoveryCandidate> { new DiscoveryCandidate { Rom = rom, MatchedStyle = SaveMatchStyle.Filename } },
                        Status = DiscoveryStatus.Pending
                    });
                }
                else
                {
                    var dirs = matchedTargetFiles.Concat(matchedNameFiles).Select(f => Path.GetDirectoryName(f)!).Distinct(StringComparer.OrdinalIgnoreCase);

                    foreach (var dir in dirs)
                    {
                        var target = matchedTargetFiles.Where(x => Path.GetDirectoryName(x)!.Equals(dir, StringComparison.OrdinalIgnoreCase)).OrderBy(y => y, StringComparer.OrdinalIgnoreCase).ToList();
                        var name = matchedNameFiles.Where(x => Path.GetDirectoryName(x)!.Equals(dir, StringComparison.OrdinalIgnoreCase)).OrderBy(y => y, StringComparer.OrdinalIgnoreCase).ToList();

                        var groups = new List<(List<string> Files, SaveMatchStyle Style)>();
                        if (target.Count > 0 && target.SequenceEqual(name, StringComparer.OrdinalIgnoreCase))
                        {
                            groups.Add((target, SaveMatchStyle.Both));
                        }   
                        else
                        {
                            if (target.Count > 0)
                                groups.Add((target, SaveMatchStyle.SaveTarget));

                            if (name.Count > 0)
                                groups.Add((name, SaveMatchStyle.Filename));
                        }

                        foreach (var group in groups)
                        {
                            DateTime lastWrite = DateTime.MinValue;
                            long bytes = 0;

                            foreach (var file in group.Files)
                            {
                                var fileInfo = new FileInfo(file);
                                bytes += fileInfo.Length;

                                if(fileInfo.LastWriteTimeUtc > lastWrite)
                                    lastWrite = fileInfo.LastWriteTimeUtc;
                            }

                            discoveredSaves.Add(new DiscoveredSave
                            {
                                MappingId = mapping.MappingId,
                                Paths = group.Files,
                                Type = group.Files.Count > 1 ? SaveLayoutTypes.Multi : SaveLayoutTypes.Single,
                                SizeBytes = bytes,
                                FileCount = group.Files.Count,
                                LatestFileWrite = lastWrite,
                                Candidates = new List<DiscoveryCandidate> { new DiscoveryCandidate { Rom = rom, MatchedStyle = group.Style } },
                                Status = DiscoveryStatus.Pending
                            });
                        }     
                    }
                }
            }

            var merged = new Dictionary<string, DiscoveredSave>();
            foreach (var save in discoveredSaves)
            {
                var key = save.Type + "|" + string.Join("|", save.Paths.Select(path => Path.GetFullPath(path).ToLowerInvariant()).OrderBy(paths => paths, StringComparer.Ordinal)); ;
                if (!merged.TryGetValue(key, out var existing)) 
                { 
                    merged[key] = save; 
                    continue; 
                }

                foreach (var candidate in save.Candidates)
                {
                    var same = existing.Candidates.FirstOrDefault(x => x.Rom.Id == candidate.Rom.Id);
                    if (same == null)
                    {
                        existing.Candidates.Add(candidate);
                    }
                    else
                    {
                        same.MatchedStyle |= candidate.MatchedStyle;
                    }
                }
            }

            return merged.Values.ToList();
        }

        private List<DiscoveredSave>? FindCardSaves(EmulatorMapping mapping)
        {
            List<DiscoveredSave> discoveredSaves = new();

            var ROMs = _plugin.ImportedGames.Where(x => x.Value.MappingID == mapping.MappingId).Select(y => y.Value);
            var files = Directory.EnumerateFiles(mapping.SaveLocation!, "*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true }).ToList();

            List<string> patterns = new();

            if(mapping.Emulator is ImportedEmulator emu)
            {
                var definitions = SaveCardDefinitions.Find(emu.EmulatorId, mapping.CoreSaveName, mapping.Profile?.PlatformSpecIds.ToList());

                if (definitions == null)
                    return null;

                patterns = definitions.SelectMany(x => x.FilePatterns).ToList();
            }

            // TODO Add custom emulator support via UI

            if (patterns == null || patterns.Count < 1)
                return new();

            var foundFiles = files.Where(x => patterns.Any(y => FileSystemName.MatchesSimpleExpression(y, Path.GetFileName(x), ignoreCase: true)));
            List<DiscoveryCandidate> candidates = ROMs.Select(x => new DiscoveryCandidate { Rom = x, MatchedStyle = SaveMatchStyle.None }).ToList();

            foreach (var file in foundFiles)
            {
                discoveredSaves.Add(new DiscoveredSave 
                {
                    MappingId = mapping.MappingId,
                    Paths = [file],
                    Type = SaveLayoutTypes.Card,
                    SizeBytes = new FileInfo(file).Length,
                    FileCount = 1,
                    LatestFileWrite = new FileInfo(file).LastWriteTimeUtc,
                    Candidates = candidates.ToList(),
                    Status = DiscoveryStatus.Pending
                });
            }

            return discoveredSaves;
        }

    }
}