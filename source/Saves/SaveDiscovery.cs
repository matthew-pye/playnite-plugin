using Graviton.Models;
using Graviton.Models.Notifications;
using Graviton.Models.RomM.Rom;
using Graviton.Models.RomM.Saves;
using Graviton.Models.Saves;

using Playnite;

using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Graviton.Saves
{
    internal static class SaveDiscovery
    {
        private static GravitonPlugin _plugin => GravitonPlugin.Instance;
        private static ILogger Logger => GravitonPlugin.Logger;

        private static readonly Regex ServerTimestampTagPattern = new(@"[ _]\[\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}\]", RegexOptions.Compiled);

        private static EmulatorMapping? Mapping;
        private static List<RomMRomLocal>? ROMs;

        public static async Task<List<GravitonSave>?> Discover(EmulatorMapping mapping)
        {
            Mapping = mapping;
            return await Discover(true);
        }

        public static async Task<List<GravitonSave>?> Discover(List<RomMRomLocal> roms)
        {
            ROMs = roms;
            return await Discover(true);
        }

        public static async Task<List<GravitonSave>?> Discover(bool setup = false)
        {
            if(!setup)
            {
                Mapping = null;
                ROMs = null;
            }

            var localroms = await GetLocalSaves();
            if (localroms == null)
            {
                Logger.Debug($"Local roms scan failed, exiting save scan!");
                return null;
            }

            Logger.Debug($"Found {localroms.Count} local saves!");

            List<GravitonSave> saves = new();

            // Process remote saves
            var remotesaves = await GetRemoteSaves();
            if (remotesaves != null)
            {
                Logger.Debug($"Found {remotesaves.Count} remote saves!");

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
                            // Add to historic saves list for that ROM
                            remotesaves.Remove(remotesave);
                            if (matchinglocal.LocalSave!.HistoricSaves == null)
                                matchinglocal.LocalSave.HistoricSaves = new();

                            matchinglocal.LocalSave.HistoricSaves.Add(new()
                            {
                                ROMID = remotesave.ROMID,
                                SaveID = remotesave.ID,
                                Slot = remotesave.Slot,
                                Status = SaveStatus.ServerOnly,
                                ServerHash = remotesave.ContentHash,
                                ServerLastUpdatedAt = DateTime.TryParse(remotesave.UpdatedAt, out DateTime ServerUpdatedAt) ? ServerUpdatedAt : null,
                                Filename = remotesave.FileName != null ? ServerTimestampTagPattern.Replace(remotesave.FileName, "") : "",
                                SourceFilePaths = new() { $"{EmulatorMapping.MappingPathToken}/{remotesave.FileName}" },
                            });
                            continue;
                        }
                    }

                    var matchingremote = remotesaves.FirstOrDefault(x => x.ROMID == remotesave.ROMID && x.Slot == remotesave.Slot);
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
                                SourceFilePaths = new() { $"{EmulatorMapping.MappingPathToken}/{historicSave.FileName}" },
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
                        ServerHash = remotesave.ContentHash,
                        ServerLastUpdatedAt = DateTime.TryParse(remotesave.UpdatedAt, out DateTime ServerUpdatedAt) ? ServerUpdatedAt : null,
                        Filename = remotesave.FileName != null ? ServerTimestampTagPattern.Replace(remotesave.FileName, "") : "",
                        SourceFilePaths = new() { $"{EmulatorMapping.MappingPathToken}/{remotesave.FileName}" },
                        HistoricSaves = historicSaves ?? null,
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
                Logger.Debug($"Remote saves scan failed, skipping!");
            }

            // Process local saves
            foreach (var localrom in localroms)
            {
                if (localrom.LocalSave == null)
                    continue;

                var mapping = _plugin.Settings.Mappings.FirstOrDefault( x => x.MappingId == localrom.MappingID);
                if (mapping == null)
                {
                    Logger.Error($"[SaveDiscovery] Failed to find mapping for {localrom.Id}:{localrom.SHA1}");
                    continue;
                }

                localrom.LocalSave.GameName = localrom.Name!;
                localrom.LocalSave.SaveDirectoryTrees = SaveDirectoryTree.Build(mapping.SavePath, localrom.LocalSave.SourceFilePaths);

                // Add newest save to historic saves list for historic selection
                if (localrom.LocalSave.HistoricSaves != null)
                {
                    localrom.LocalSave.HistoricSaves.Add(localrom.LocalSave);
                    localrom.LocalSave.HistoricSaves = localrom.LocalSave.HistoricSaves.OrderByDescending(x => x.ServerLastUpdatedAt).ToObservableCollection();
                }
                    

                saves.Add(localrom.LocalSave);
            }

            // Process auto detected saves
            var autoDetectedSaves = await GetAutoDetectedSaves(saves);
            if(autoDetectedSaves != null)
            {
                Logger.Debug($"Found {autoDetectedSaves.Count} potential saves!");
                saves.AddRange(autoDetectedSaves);
            }
            else
            {
                Logger.Debug($"Auto detect saves scan failed, skipping!");
            }
                
            return saves;
        }


        private static async Task<List<RomMRomLocal>?> GetLocalSaves()
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

            roms = await SaveNegotiator.SoftNegotiateSaves(roms.Where(x => x.LocalSave!.Enabled).ToList());
            if (roms == null)
                return null;

            Logger.Debug($"Found {roms.Count} roms with saves");

            return roms;
        }


        private static async Task<List<RomMSave>?> GetRemoteSaves()
        {
            JsonDocument? response = null;

            if (ROMs != null)
            {
                List<RomMSave>? saves = null;

                foreach (var rom in ROMs)
                {
                    response = await HttpClientSingleton.RomMGetAsync($"/api/saves?rom_id={rom.Id}");
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
                        GravitonNotify.Add(new GravitonNotification("graviton.deserialize.failed", Loc.GetString("FailedDeserialize", ("Error", ex.Message)), GravitonSeverity.Error, ex));
                        continue;
                    }
                }

                return saves;
            }

            if (Mapping != null)
            {
                response = await HttpClientSingleton.RomMGetAsync($"/api/saves?platform_id={Mapping.RomMPlatformId}");
            }
            else
            {
                response = await HttpClientSingleton.RomMGetAsync($"/api/saves");
            }

            if (response == null)
                return null;

            try
            {
                return JsonSerializer.Deserialize<List<RomMSave>>(response);
            }
            catch (Exception ex)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.deserialize.failed", Loc.GetString("FailedDeserialize", ("Error", ex.Message)), GravitonSeverity.Error, ex));
                return null;
            }
        }

        public static async Task<List<RomMSave>?> GetArchivedSaves()
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
        public static async Task<List<RomMSave>?> GetArchivedSaves(EmulatorMapping mapping)
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
        public static async Task<List<RomMSave>?> GetArchivedSaves(List<RomMRomLocal> roms)
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
                    save.ROMName = " -- UNKNOWN GAME --";
                }
            }

            return saves.Where(x => string.IsNullOrEmpty(x.Slot) && !string.IsNullOrEmpty(x.ROMName)).ToList();
        }

        private static async Task<List<GravitonSave>?> GetAutoDetectedSaves(List<GravitonSave> currectSaveList)
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
                    GravitonNotify.Add(new GravitonNotification("graviton.autodetect.noextentions", $"One or more mappings have no auto detect extensions, they have been skipped!", GravitonSeverity.Warn));
                }

                return saves;
            }


        }

        private static async Task<List<GravitonSave>?> GetAutoDetectedSavesForMapping(EmulatorMapping mapping, List<RomMRomLocal> roms, List<GravitonSave> currectSaveList)
        {
            if (mapping.FindSaveLayout == SaveLayoutStyle.Disabled)
                return new();

            List<GravitonSave>? saves = null;

            if (mapping.FindSaveLayout == SaveLayoutStyle.WholeFolder)
            {
                foreach (var dir in Directory.EnumerateDirectories(mapping.SavePath, "*", SearchOption.AllDirectories))
                {
                    var matchingROM = roms.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x.FileName) == Path.GetFileName(dir));
                    if (matchingROM == null)
                        continue;

                    if (saves == null)
                        saves = new();

                    // Saves need to start with {MappingSavePath} so they can be moved anywhere
                    var saveDir = dir.Replace(mapping.SavePath, "{MappingSavePath}");

                    // Directories need to add trailing slash so that we know they are directories
                    //      e.g. {MappingSavePath}\Mario.Backup is a directory but Path.HasExtension would think its a file so we need to add \ to the end to avoid that
                    if (!saveDir.EndsWith('/') && !saveDir.EndsWith('\\'))
                        saveDir += "\\";

                    var save = new GravitonSave
                    {
                        Status = SaveStatus.UntrackedLocal,
                        SourceFilePaths = new() { saveDir },
                        Filename = $"{Path.GetFileNameWithoutExtension(matchingROM.FileName)}.rommsave.zip",
                        GameName = matchingROM.Name!,
                        ROMID = matchingROM.Id
                    };
                    save.SaveDirectoryTrees = SaveDirectoryTree.Build(mapping.SavePath, save.SourceFilePaths);

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

                        var save = new GravitonSave
                        {
                            Status = SaveStatus.UntrackedLocal,
                            SourceFilePaths = new() { filePath },
                            Filename = Path.GetFileName(filePath),
                            GameName = rom.Name!,
                            ROMID = rom.Id
                        };
                        save.SaveDirectoryTrees = SaveDirectoryTree.Build(mapping.SavePath, save.SourceFilePaths);

                        saves.Add(save);

                    }
                }
                else // SaveLayoutStyle.FixedSet
                {
                    if (saves == null)
                        saves = new();

                    List<string> savePaths = new();
                    foreach (var file in files)
                    {
                        savePaths.Add(file.Replace(mapping.SavePath, "{MappingSavePath}"));
                    }

                    var save = new GravitonSave
                    {
                        Status = SaveStatus.UntrackedLocal,
                        SourceFilePaths = savePaths,
                        Filename = savePaths.Count > 1 ? $"{Path.GetFileNameWithoutExtension(rom.FileName)}.rommsave.zip" : Path.GetFileName(savePaths[0]),
                        GameName = rom.Name!,
                        ROMID = rom.Id
                    };
                    save.SaveDirectoryTrees = SaveDirectoryTree.Build(mapping.SavePath, save.SourceFilePaths);

                    saves.Add(save);

                }
            }

            return saves;
        }


        private static bool IsAlreadyTracked(string rootPath, string file, string sourcePath)
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
