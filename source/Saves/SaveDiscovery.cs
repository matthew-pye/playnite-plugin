using Graviton.Models;
using Graviton.Models.Notifications;
using Graviton.Models.RomM.Rom;
using Graviton.Models.RomM.Saves;
using Graviton.Models.Saves;
using Graviton.Notifications;

using Playnite;

using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Graviton.Saves
{
    internal class SaveDiscovery
    {
        private GravitonPlugin _plugin;
        private IPlayniteApi _playniteAPI;
        private GravitonLogger _logger;
        private IRomMServer _romMServer;

        private SaveController SaveController => _plugin.SaveController!;

        private readonly Regex ServerTimestampTagPattern = new(@"[ _]\[\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}\]", RegexOptions.Compiled);

        private EmulatorMapping? Mapping;
        private List<RomMRomLocal>? ROMs;

        public SaveDiscovery(GravitonPlugin plugin, IPlayniteApi playniteAPI, GravitonLogger logger, IRomMServer romMServer)
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;
            _romMServer = romMServer;
        }

        public async Task<List<GravitonSave>?> DiscoverSaves(EmulatorMapping mapping)
        {
            Mapping = mapping;
            return await DiscoverSaves(true);
        }

        public async Task<List<GravitonSave>?> DiscoverSaves(List<RomMRomLocal> roms)
        {
            ROMs = roms;
            return await DiscoverSaves(true);
        }

        public async Task<List<GravitonSave>?> DiscoverSaves(bool setup = false)
        {
            if(!setup)
            {
                Mapping = null;
                ROMs = null;
            }

            var localroms = await GetLocalSaves();
            if (localroms == null)
            {
                _logger.Debug($"Local roms scan failed, exiting save scan!");
                return null;
            }

            _logger.Debug($"Found {localroms.Count} local saves!");

            List<GravitonSave> saves = new();

            // Process remote saves
            var remotesaves = await GetRemoteSaves();
            if (remotesaves != null)
            {
                _logger.Debug($"Found {remotesaves.Count} remote saves!");

                // First pass: Remove and combine remote saves
                foreach (var remotesave in remotesaves.ToList())
                {
                    // Remove archival saves
                    if (string.IsNullOrEmpty(remotesave.Slot))
                    {
                        remotesaves.Remove(remotesave);
                        continue;
                    }

                    var matchinglocal = localroms.FirstOrDefault(x => x.Id == remotesave.ROMID && x.LocalSave?.Slot == remotesave.Slot);
                    if(matchinglocal != null && matchinglocal.LocalSave != null)
                    {
                        // Remove exact save match
                        if(matchinglocal.LocalSave?.SaveID == remotesave.ID)
                        {
                            remotesaves.Remove(remotesave);
                            continue;
                        }
                        else
                        {
                            if (matchinglocal.LocalSave == null)
                                matchinglocal.LocalSave = new();

                            // Add to historic saves list for that ROM
                            remotesaves.Remove(remotesave);
                            if (matchinglocal.LocalSave.HistoricSaves == null)
                                matchinglocal.LocalSave.HistoricSaves = new();

                            GravitonSave historicSave = new()
                            {
                                ROMID = remotesave.ROMID,
                                SaveID = remotesave.ID,
                                Slot = remotesave.Slot,
                                Status = SaveStatus.ServerOnly,
                                ServerHash = remotesave.ContentHash,
                                ServerLastUpdatedAt = DateTime.TryParse(remotesave.UpdatedAt, out DateTime ServerUpdatedAt) ? ServerUpdatedAt : null,
                                Filename = remotesave.FileName != null ? ServerTimestampTagPattern.Replace(remotesave.FileName, "") : "",
                                SourceFilePaths = new() { $"{EmulatorMapping.SavePathToken}/{ServerTimestampTagPattern.Replace(remotesave.FileName ?? "", "")}" },
                                IsHistoric = true,
                            };

                            // Don't re-add historic save if the save is already in the historic list
                            var existingHistoricSave = matchinglocal.LocalSave.HistoricSaves.FirstOrDefault(x => x.SaveID == remotesave.ID);
                            if (existingHistoricSave == null)
                                matchinglocal.LocalSave.HistoricSaves.Add(historicSave);
                            else
                                matchinglocal.LocalSave.HistoricSaves[matchinglocal.LocalSave.HistoricSaves.IndexOf(existingHistoricSave)] = historicSave;

                            continue;
                        }
                    }

                    var matchingremote = remotesaves.FirstOrDefault(x => x.ROMID == remotesave.ROMID && x.Slot == remotesave.Slot && x != remotesave);
                    if(matchingremote != null)
                    {
                        DateTime.TryParse(matchingremote.UpdatedAt, out DateTime matchingUpdatedAt);
                        DateTime.TryParse(remotesave.UpdatedAt, out DateTime remotesaveUpdatedAt);

                        if(matchingUpdatedAt < remotesaveUpdatedAt) // Matching save is older place all saves in historic saves list
                        {
                            remotesaves.Remove(remotesave);

                            remotesave.HistoricSaves.Add(matchingremote);
                            remotesave.HistoricSaves.AddRange(matchingremote.HistoricSaves);

                            remotesaves.Remove(matchingremote);
                            remotesaves.Add(remotesave);
                        }
                        else // Current save is older place all its saves and its self in historic saves list for the Matching save
                        {
                            matchingremote.HistoricSaves.AddRange(remotesave.HistoricSaves);
                            remotesave.HistoricSaves.Clear();
                            matchingremote.HistoricSaves.Add(remotesave);
                        }
                    }

                }

                // Second pass: Build save list
                foreach (var remotesave in remotesaves)
                {
                    // Build save history
                    ObservableCollection<GravitonSave>? historicSaves = null;
                    if(remotesave.HistoricSaves.Count > 0)
                    {
                        historicSaves = new();
                        foreach (var historicSave in remotesave.HistoricSaves)
                        {
                            historicSaves.Add(new()
                            {
                                ROMID = historicSave.ROMID,
                                SaveID = historicSave.ID,
                                Slot = historicSave.Slot,
                                Status = SaveStatus.ServerOnly,
                                ServerHash = historicSave.ContentHash,
                                ServerLastUpdatedAt = DateTime.TryParse(historicSave.UpdatedAt, out DateTime HistoricServerUpdatedAt) ? HistoricServerUpdatedAt : null,
                                Filename = historicSave.FileName != null ? ServerTimestampTagPattern.Replace(historicSave.FileName, "") : "",
                                SourceFilePaths = new() { $"{EmulatorMapping.SavePathToken}/{ServerTimestampTagPattern.Replace(historicSave.FileName ?? "", "")}" },
                                IsHistoric = true
                            });
                        }
                    }

                    // Add remote save
                    GravitonSave newsave = new()
                    {
                        ROMID = remotesave.ROMID,
                        SaveID = remotesave.ID,
                        Slot = remotesave.Slot,
                        Status = SaveStatus.ServerOnly,
                        FileSize = remotesave.FileSize ?? 0,
                        ServerHash = remotesave.ContentHash,
                        ServerLastUpdatedAt = DateTime.TryParse(remotesave.UpdatedAt, out DateTime ServerUpdatedAt) ? ServerUpdatedAt : null,
                        Filename = remotesave.FileName != null ? ServerTimestampTagPattern.Replace(remotesave.FileName, "") : "",
                        SourceFilePaths = new() { $"{EmulatorMapping.SavePathToken}/{ServerTimestampTagPattern.Replace(remotesave.FileName ?? "", "")}" },
                        HistoricSaves = historicSaves?.OrderByDescending(x => x.LastSyncedAt).ToObservableCollection() ?? null,
                        IsCurrent = true
                    };

                    // Add newest save to historic saves list for switching
                    if (newsave.HistoricSaves != null)
                    {
                        newsave.HistoricSaves.Add(newsave);
                        newsave.HistoricSaves = newsave.HistoricSaves.OrderByDescending(x => x.ServerLastUpdatedAt).ToObservableCollection();
                    }
                       
                    saves.Add(newsave);

                }
            }
            else
            {
                _logger.Debug($"Remote saves scan failed, skipping!");
            }

            // Process local saves
            foreach (var localrom in localroms)
            {
                if (localrom.LocalSave == null)
                    continue;

                var mapping = _plugin.Settings.Mappings.FirstOrDefault( x => x.MappingId == localrom.MappingID);
                if (mapping == null)
                {
                    _logger.Error($"Failed to find mapping for {localrom.Id}");
                    continue;
                }

                localrom.LocalSave.GameName = localrom.Name ?? "";
                localrom.LocalSave.SaveDirectoryTrees = SaveDirectoryTree.Build(mapping.SavePath, localrom.LocalSave.SourceFilePaths.ToList());
                localrom.LocalSave.IsCurrent = true;

                // Add newest save to historic saves list for historic selection
                if (localrom.LocalSave.HistoricSaves != null)
                {
                    var exisitingHistoricSave = localrom.LocalSave.HistoricSaves.FirstOrDefault(x => x.SaveID == localrom.LocalSave.SaveID);
                    if (exisitingHistoricSave == null)
                        localrom.LocalSave.HistoricSaves.Add(localrom.LocalSave);

                    localrom.LocalSave.HistoricSaves = localrom.LocalSave.HistoricSaves.OrderByDescending(x => x.LastSyncedAt).ToObservableCollection();
                }

                saves.Add(localrom.LocalSave);
            }

            // Process auto detected saves
            var autoDetectedSaves = await GetAutoDetectedSaves(saves);
            if(autoDetectedSaves != null)
            {
                _logger.Debug($"Found {autoDetectedSaves.Count} potential saves!");

                foreach (var autosave in autoDetectedSaves)
                {
                    var localsourcepaths = localroms.Select(x => x.LocalSave).SelectMany(y => y?.SourceFilePaths ?? []);
                    var exists = autosave.SourceFilePaths.Intersect(localsourcepaths);
                    if (exists == null || exists.Count() < 1)
                        saves.Add(autosave);
                }
            }
            else
            {
                _logger.Debug($"Auto detect saves scan failed, skipping!");
            }
                
            return saves;
        }

        public async Task<List<MemoryCardSave>?> DiscoverMemoryCards(EmulatorMapping mapping)
        {
            Mapping = mapping;
            return await DiscoverMemoryCards(true);
        }

        public async Task<List<MemoryCardSave>?> DiscoverMemoryCards(List<RomMRomLocal> roms)
        {
            ROMs = roms;
            return await DiscoverMemoryCards(true);
        }

        public async Task<List<MemoryCardSave>?> DiscoverMemoryCards(bool setup = false)
        {
            if (!setup)
            {
                Mapping = null;
                ROMs = null;
            }
            List<MemoryCardSave> memoryCards = new();

            var result = await GetLocalMemoryCards();
            if (result != null)
                memoryCards.AddRange(result);

            result = await GetRemoteMemoryCards();
            if (result != null)
            {
                
            }
                
            return memoryCards;
        }

        private async Task<List<RomMRomLocal>?> GetLocalSaves()
        {
            List<RomMRomLocal>? roms;

            if (ROMs != null)
            {
                roms = ROMs.Where(x => x.LocalSave != null && x.LocalSave.Enabled).ToList();
            }
            else if(Mapping != null)
            {
                roms = _plugin.ImportedGames.Where(x => x.Value.MappingID == Mapping.MappingId).Where(x => x.Value.LocalSave != null && x.Value.LocalSave.Enabled).Select(x => x.Value).ToList();
            }
            else
            {
                roms = _plugin.ImportedGames.Where(x => x.Value.LocalSave != null && x.Value.LocalSave.Enabled).Select(x => x.Value).ToList();
            }

            if (roms == null || roms.Count < 1)
                return new();

            roms = await SaveController.Negotiator.SoftNegotiateSaves(roms.Where(x => x.LocalSave?.Enabled ?? false).ToList());
            if (roms == null)
                return null;

            _logger.Debug($"Found {roms.Count} roms with saves");

            return roms;
        }
        private async Task<List<MemoryCardSave>?> GetLocalMemoryCards()
        {
            List<EmulatorMapping> mappings = new();

            if(Mapping != null)
            {
                mappings.Add(Mapping);
            }
            else if(ROMs != null)
            {
                foreach (var rom in ROMs)
                {
                    var mapping = _plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == rom.MappingID);

                    if (mapping == null)
                        continue;

                    if (mappings.Contains(mapping))
                        continue;

                    mappings.Add(mapping); 
                }
            }
            else
            {
                mappings = _plugin.Settings.Mappings.ToList();
            }

            if (mappings.Count() <= 0 || !mappings.Any(x => x.MemoryCardSave == null))
                return null;

            List<MemoryCardSave> memoryCards = new();
            foreach (var mapping in mappings)
            {
                if(mapping.MemoryCardSave != null)
                    memoryCards.Add(mapping.MemoryCardSave);
            }

            return memoryCards;
        }

        private async Task<List<RomMSave>?> GetRemoteSaves()
        {
            JsonDocument? response = null;

            if (ROMs != null)
            {
                List<RomMSave>? saves = null;

                foreach (var rom in ROMs)
                {
                    response = await _romMServer.GETAsync($"/api/saves?rom_id={rom.Id}");
                    if (response == null)
                        continue;

                    try
                    {
                        var rommsaves = JsonSerializer.Deserialize<List<RomMSave>>(response);
                        if (rommsaves == null)
                            continue;

                        if (saves == null)
                            saves = new();

                        saves.AddRange(rommsaves);
                    }
                    catch (Exception ex)
                    {
                        GravitonNotify.Notify("graviton.deserialize.failed", Loc.GetString("FailedDeserialize", ("Error", ex.Message)), GravitonSeverity.Error, ex);
                        continue;
                    }
                }

                return saves;
            }

            if (Mapping != null)
            {
                response = await _romMServer.GETAsync($"/api/saves?platform_id={Mapping.RomMPlatformId}");
            }
            else
            {
                response = await _romMServer.GETAsync($"/api/saves");
            }

            if (response == null)
                return null;

            try
            {
                return JsonSerializer.Deserialize<List<RomMSave>>(response);
            }
            catch (Exception ex)
            {
                GravitonNotify.Notify("graviton.deserialize.failed", Loc.GetString("FailedDeserialize", ("Error", ex.Message)), GravitonSeverity.Error, ex);
                return null;
            }
        }
        private async Task<List<MemoryCardSave>?> GetRemoteMemoryCards()
        {
            List<EmulatorMapping> mappings = new();

            if (Mapping != null)
            {
                mappings.Add(Mapping);
            }
            else if (ROMs != null)
            {
                foreach (var rom in ROMs)
                {
                    var mapping = _plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == rom.MappingID);

                    if (mapping == null)
                        continue;

                    if (mappings.Contains(mapping))
                        continue;

                    mappings.Add(mapping);
                }
            }
            else
            {
                mappings = _plugin.Settings.Mappings.ToList();
            }

            if (mappings.Count() <= 0 || !mappings.Any(x => x.MemoryCardSave == null))
                return null;

            var response = await _romMServer.GETAsync("/api/memory-cards");
            if (response == null)
                return null;

            var result = JsonSerializer.Deserialize<List<RomMMemoryCard>>(response);
            if (result == null)
                return null;

            List<MemoryCardSave> memoryCards = new();
            foreach (var card in result)
            {
                // Skip memory cards for platforms that have not been imported
                var mapping = mappings.FirstOrDefault(x => x.RomMPlatform?.Id == card.PlatformID);
                if (mapping == null)
                    continue;

                // Fetch all the memory card versions
                response = await _romMServer.GETAsync($"/api/memory-cards/{card.ID}/versions");
                if (response == null)
                    continue;

                var cardVersions = JsonSerializer.Deserialize<List<RomMMemoryCardVersion>>(response);
                if (cardVersions == null)
                    return null;

                cardVersions = cardVersions.OrderByDescending(x => DateTime.Parse(x.CreatedAt ?? DateTime.UnixEpoch.ToString())).ToList();

                // Create a memory card history
                ObservableCollection<MemoryCardSave> cardSaveVersions = new();
                foreach (var cardVersion in cardVersions.GetRange(1, cardVersions.Count() - 1))
                {
                    cardSaveVersions.Add(new()
                    {
                        EmulatorMappingID = mapping.MappingId,
                        MemoryCardID = card.ID,
                        Status = SaveStatus.ServerOnly,
                        ContentHash = cardVersion.ContentHash,
                        IsCurrent = false,
                        IsHistoric = true,
                        CreatedAt = DateTime.Parse(cardVersion.CreatedAt ?? DateTime.UnixEpoch.ToString())
                    });
                }

                // Update history on already tracked memory card
                if (mapping.MemoryCardSave?.MemoryCardID == card.ID)
                {
                    if(mapping.MemoryCardSave.ContentHash != cardVersions[0].ContentHash)
                    {
                        if (mapping.MemoryCardSave.CreatedAt < DateTime.Parse(cardVersions[0].CreatedAt ?? DateTime.UnixEpoch.ToString()))
                        {
                            mapping.MemoryCardSave.Status = SaveStatus.RemoteNewer;
                        }
                        else
                        {
                            mapping.MemoryCardSave.Status = SaveStatus.LocalNewer;
                        }
                    }

                    mapping.MemoryCardSave.HistoricSaves = cardSaveVersions;
                    continue;
                }

                // Add untrack memory card to the memory card list
                memoryCards.Add(new()
                {
                    EmulatorMappingID = mapping.MappingId,
                    MemoryCardID = card.ID,
                    Status = SaveStatus.ServerOnly,
                    ContentHash = cardVersions[0].ContentHash,
                    IsCurrent = true,
                    CreatedAt = DateTime.Parse(cardVersions[0].CreatedAt ?? DateTime.UnixEpoch.ToString()),
                    HistoricSaves = cardSaveVersions
                });
            }


            return memoryCards;
        }

        public async Task<List<RomMSave>?> GetArchivedSaves()
        {
            var saves = await GetRemoteSaves();
            if (saves == null)
                return null;

            foreach (var save in saves)
            {
                var rom = _plugin.ImportedGames.FirstOrDefault(x => x.Value.Id == save.ROMID);
                if(rom.Value != null)
                {
                    save.ROMName = rom.Value.Name;
                }
            }

            return saves.Where(x => string.IsNullOrEmpty(x.Slot) && !string.IsNullOrEmpty(x.ROMName)).ToList();
        }
        public async Task<List<RomMSave>?> GetArchivedSaves(EmulatorMapping mapping)
        {
            var saves = await GetRemoteSaves();
            var roms = _plugin.ImportedGames.Where(x => x.Value.MappingID == mapping.MappingId);
            if (saves == null)
                return null;

            foreach (var save in saves)
            {
                var rom = roms.FirstOrDefault(x => x.Value.Id == save.ROMID);
                if (rom.Value != null)
                {
                    save.ROMName = rom.Value.Name;
                }
            }

            return saves.Where(x => string.IsNullOrEmpty(x.Slot) && !string.IsNullOrEmpty(x.ROMName)).ToList();
        }
        public async Task<List<RomMSave>?> GetArchivedSaves(List<RomMRomLocal> roms)
        {
            var saves = await GetRemoteSaves();
            if (saves == null)
                return null;

            foreach (var save in saves)
            {
                var rom = roms.FirstOrDefault(x => x.Id == save.ROMID);
                if (rom != null)
                {
                    save.ROMName = rom.Name;
                }
                else
                {
                    save.ROMName = Loc.GetString("UnknownGame");
                }
            }

            return saves.Where(x => string.IsNullOrEmpty(x.Slot) && !string.IsNullOrEmpty(x.ROMName)).ToList();
        }

        private async Task<List<GravitonSave>?> GetAutoDetectedSaves(List<GravitonSave> currectSaveList)
        {
            List<RomMRomLocal> roms;

            if(ROMs != null)
            {
                roms = ROMs;
            }
            else if(Mapping != null)
            {
                if (Mapping.FindSaveLayout == SaveLayoutStyle.Disabled)
                    return new();

                roms = _plugin.ImportedGames.Where(x => x.Value.MappingID == Mapping.MappingId).Select(y => y.Value).ToList();
            }
            else
            {
                roms = _plugin.ImportedGames.Select(y => y.Value).ToList();
            }


            if (Mapping != null)
            {
                var saves = await GetAutoDetectedSavesForMapping(Mapping, roms, currectSaveList);
                return saves;
            }
            else
            {
                var mappings = _plugin.Settings.Mappings.Where(x => roms.Any(y => y.MappingID == x.MappingId));
                bool noExtentions = false;

                List<GravitonSave>? saves = null;

                foreach (var mapping in mappings)
                {
                    var mappingsaves = await GetAutoDetectedSavesForMapping(mapping, roms, currectSaveList);
                    if (mappingsaves == null)
                    {
                        noExtentions = true;
                        continue;
                    }

                    if (saves == null)
                        saves = new();

                    saves.AddRange(mappingsaves);
                }

                if(noExtentions)
                {
                    GravitonNotify.Notify("graviton.autodetect.noextentions", Loc.GetString("NoAutoDetectExtensions"), GravitonSeverity.Warn);
                }

                return saves;
            }


        }

        private async Task<List<GravitonSave>?> GetAutoDetectedSavesForMapping(EmulatorMapping mapping, List<RomMRomLocal> roms, List<GravitonSave> currectSaveList)
        {
            if (mapping.FindSaveLayout == SaveLayoutStyle.Disabled)
                return new();

            List<GravitonSave>? saves = null;

            if (mapping.FindSaveLayout == SaveLayoutStyle.WholeFolder)
            {
                foreach (var dir in Directory.EnumerateDirectories(mapping.SavePath, "*", SearchOption.AllDirectories))
                {
                    var matchingROM = roms.FirstOrDefault(x => (Path.GetFileNameWithoutExtension(x.FileName) == Path.GetFileName(dir)) || (x.SaveTarget != null && dir.EndsWith(x.SaveTarget.Replace("/", "\\"))));
                    if (matchingROM == null)
                        continue;
                    
                    if (saves == null)
                        saves = new();

                    // Saves need to start with {MappingSavePath} so they can be moved anywhere
                    var saveDir = dir.Replace(mapping.SavePath, "{MappingSavePath}");
                    saveDir = saveDir.Replace("\\", "/");

                    // Directories need to add trailing slash so that we know they are directories
                    //      e.g. {MappingSavePath}\Mario.Backup is a directory but Path.HasExtension would think its a file so we need to add \ to the end to avoid that
                    if (!saveDir.EndsWith('/') && !saveDir.EndsWith('\\'))
                        saveDir += "/";

                    long totalSize = 0;
                    var info = new DirectoryInfo(dir);
                    if(info != null)
                        totalSize = info.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);


                    var save = new GravitonSave
                    {
                        ROMID = matchingROM.Id,
                        GameName = matchingROM.Name ?? "",
                        Status = SaveStatus.UntrackedLocal,
                        Filename = $"{Path.GetFileNameWithoutExtension(matchingROM.FileName)}.zip",
                        SourceFilePaths = new() { saveDir },
                        FileSize = totalSize
                    };
                    save.SaveDirectoryTrees = SaveDirectoryTree.Build(mapping.SavePath, save.SourceFilePaths.ToList());

                    saves.Add(save);
                    continue;
                }

                return saves;
            }

            // Skip if no extensions are set
            if (string.IsNullOrWhiteSpace(mapping.FindSaveFileExtensions))
                return null;
            
            var extensions = mapping.FindSaveFileExtensions.Split(';');

            foreach (var rom in roms.Where(x => x.MappingID == mapping.MappingId))
            {
                var files = Directory.EnumerateFiles(mapping.SavePath, $"{Path.GetFileNameWithoutExtension(rom.FileName)}.*", SearchOption.AllDirectories).ToList();

                // Add files that match the save target
                if (rom.SaveTarget != null)
                {
                    files.AddRange(Directory.EnumerateFiles(mapping.SavePath, $"{rom.SaveTarget}.*", SearchOption.AllDirectories));

                    // Add gamecube gci files
                    if(rom.SaveTarget.Length == 8)
                    {
                        var GCID = Encoding.ASCII.GetString(Convert.FromHexString(rom.SaveTarget));
                        files.AddRange(Directory.EnumerateFiles(mapping.SavePath, $"??-{GCID}-*.gci", SearchOption.AllDirectories));
                    }
                    
                }
                    
                if (files.Count <= 0)
                    continue;

                // Ignore files that are already being tracked by other saves
                foreach (var file in files.ToList())
                {
                    if (currectSaveList.SelectMany(x => x.SourceFilePaths).Any(y => IsAlreadyTracked(mapping.SavePath, file, y)))
                        files.Remove(file);
                    else if (!extensions.Any(x => file.EndsWith("." + x, StringComparison.OrdinalIgnoreCase)))
                        files.Remove(file);
                }

                if (mapping.FindSaveLayout == SaveLayoutStyle.SingleFile)
                {
                    if (saves == null)
                        saves = new();

                    foreach (var file in files)
                    {
                        var filePath = file.Replace(mapping.SavePath, "{MappingSavePath}");
                        filePath = filePath.Replace("\\", "/");

                        var save = new GravitonSave
                        {
                            ROMID = rom.Id,
                            GameName = rom.Name ?? "",
                            Status = SaveStatus.UntrackedLocal,
                            SourceFilePaths = new() { filePath },
                            Filename = Path.GetFileName(filePath),
                            FileSize = new FileInfo(file).Length,
                        };
                        save.SaveDirectoryTrees = SaveDirectoryTree.Build(mapping.SavePath, save.SourceFilePaths.ToList());

                        saves.Add(save);

                    }
                }
                else // SaveLayoutStyle.FixedSet
                {
                    if (saves == null)
                        saves = new();

                    long totalSize = 0;

                    List<string> savePaths = new();
                    foreach (var file in files)
                    {
                        totalSize += new FileInfo(file).Length;
                        savePaths.Add((file.Replace(mapping.SavePath, "{MappingSavePath}")).Replace("\\", "/"));
                    }

                    var save = new GravitonSave
                    {
                        ROMID = rom.Id,
                        GameName = rom.Name ?? "",
                        Status = SaveStatus.UntrackedLocal,
                        SourceFilePaths = savePaths.ToObservableCollection(),
                        Filename = savePaths.Count > 1 ? $"{Path.GetFileNameWithoutExtension(rom.FileName)}.zip" : Path.GetFileName(savePaths[0]),
                        FileSize = totalSize,
                    };
                    save.SaveDirectoryTrees = SaveDirectoryTree.Build(mapping.SavePath, save.SourceFilePaths.ToList());

                    saves.Add(save);

                }
            }

            return saves;
        }

        private bool IsAlreadyTracked(string rootPath, string file, string sourcePath)
        {
            // Check if file is already tracked
            if (string.Equals(file, sourcePath, StringComparison.OrdinalIgnoreCase))
                return true;

            if (sourcePath.StartsWith("{MappingSavePath}"))
                sourcePath = sourcePath.Replace("{MappingSavePath}", rootPath);

            // Check if the folder that the file is in is already tracked
            if (!Path.HasExtension(sourcePath))
            {
                var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourcePath));
                var fullFile = Path.GetFullPath(file);
                return fullFile.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

    }
}
