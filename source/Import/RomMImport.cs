using Playnite;

using RomMLibrary.Models;
using RomMLibrary.Models.RomM.Rom;

using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RomMLibrary.Import
{
    internal class RomMImport
    {
        private readonly RomMLibraryPlugin Plugin;
        private readonly IPlayniteApi PlayniteApi;
        private readonly ILogger? Logger;
        CancellationToken CancelToken;
        EmulatorMapping Mapping;
        List<RomMRom> ROMs;

        public RomMImport(RomMLibraryPlugin plugin, CancellationToken cancelToken, EmulatorMapping mapping, List<RomMRom> roms)
        {
            Plugin = plugin;
            PlayniteApi = RomMLibraryPlugin.PlayniteApi ?? throw new Exception("Playnite API is null cannot continue!"); ;
            Logger = RomMLibraryPlugin.Logger;
            CancelToken = cancelToken;
            Mapping = mapping;
            ROMs = roms;
        }

        private RomMFile? DetermineFile(RomMRom ROM)
        {
            if(ROM.Files == null)
                return null;

            if(ROM.Files.Count > 1)
            {
                List<string> fullpaths = new List<string>();
                foreach (RomMFile file in ROM.Files)
                {
                    fullpaths.Add(file.FullPath);
                }

                fullpaths = fullpaths.OrderBy(x => x.Count(c => c == '/')).ToList();
                return ROM.Files.Where(x => x.FullPath == fullpaths[0]).FirstOrDefault();
            }

            return ROM.Files.FirstOrDefault();
        }

        // Main library import functions
        public List<Game> ProcessData()
        {
            var games = new List<Game>();
            List<string> ImportedGamesIDs = new List<string>();

            if (Mapping.RomMPlatform?.Name != null && !PlayniteApi.Library.Platforms.Any(x => x.Name == Mapping.RomMPlatform.Name))
            {
                PlayniteApi.Library.Platforms.AddAsync(new Platform(Mapping.RomMPlatform.Name));
            }

            foreach (var ROM in ROMs)
            {
                if (CancelToken.IsCancellationRequested)
                    break;

                // Some newer platforms don't get a hash value so we will compromise with this
                if (string.IsNullOrEmpty(ROM.SHA1))
                {
                    var tohash = Encoding.UTF8.GetBytes($"{ROM.Name}{ROM.FileSizeBytes}");
                    ROM.SHA1 = Encoding.UTF8.GetString(SHA1.HashData(tohash));
                }

                // Skip game import if the ROM is apart of the exclusion list
                //if (_plugin.Playnite.Database.ImportExclusions[Playnite.ImportExclusionItem.GetId($"{ROM.Id}:{ROM.SHA1}", _plugin.Id)] != null)
                //{
                //    Logger?.Warn($"[Importer] Excluding {ROM.Name} from import.");
                //    continue;
                //}

                // Skip if ROM has no filename
                if (string.IsNullOrEmpty(ROM.FileName))
                {
                    PlayniteApi.Notifications.Add(new NotificationMessage(RomMLibraryPlugin.Id, $"Filename for ROM ID: {ROM.Id} doesn't exist!\nDoes ROM exist on the servers filesystem?", NotificationSeverity.Error));
                    continue;
                }

                // Fail-safe incase none of these are set to true
                if (!ROM.HasSimpleSingleFile & !ROM.HasNestedSingleFile & !ROM.HasMultipleFiles)
                    ROM.HasMultipleFiles = true;

                // Migrate old RomMGameInfo id to new romMId:SHA1 id
                string gameID = $"{ROM.Id}:{ROM.SHA1}";

                // Merging revisions
                if (Plugin.Settings.MergeRevisions && ROM.Siblings?.Count > 0)
                {
                    if (CheckForMainSibling(ROM) == MainSibling.Other)
                    {
                        var siblinggame = PlayniteApi.Library.Games.FirstOrDefault(x => x.LibraryGameId == gameID);
                        if(siblinggame != null)
                        {
                            PlayniteApi.Library.Games.RemoveAsync(siblinggame.Id);
                        }  
                        continue;
                    }
                        
                    if (ROM.Processed)
                    {
                        var siblinggame = PlayniteApi.Library.Games.FirstOrDefault(x => x.LibraryGameId == gameID);
                        if (siblinggame != null)
                        {
                            PlayniteApi.Library.Games.RemoveAsync(siblinggame.Id);
                        }
                        continue;
                    }
                        
                }

                // Save Game ROM data to file
                SaveGameData(ROM);
                
                // Skip full import if ROM has already been imported 
                Guid statusID = new Guid();
                var game = PlayniteApi.Library.Games.FirstOrDefault(g => g.LibraryGameId == gameID);
                if (game != null)
                {
                    // Sync user data
                    if(Plugin.Settings.KeepRomMSynced)
                    {
                        //statusID = DetermineCompletionStatus(ROM);
                        //
                        //game.Favorite = _favourites.Exists(f => f == ROM.Id);
                        //
                        //if (statusID != Guid.Empty)
                        //{
                        //    game.CompletionStatusId = statusID;
                        //}
                        //_plugin.Playnite.Database.Games.Update(game);
                    }

                    ImportedGamesIDs.Add(gameID);
                    continue;
                }

                // If keep deleted games is enabled and a deleted game gets re-added back to the server under a new romMId, Update playnite entry
                if(Plugin.Settings.KeepDeletedGames)
                {
                    if(UpdatedDeletedGame(ROM))
                    {
                        ImportedGamesIDs.Add(gameID);
                        continue;
                    }
                }

                var importedGame = ImportGame(ROM, statusID);
                if (importedGame != null)
                {
                    games.Add(importedGame); 
                    ImportedGamesIDs.Add(gameID);
                }
                else
                {
                    Logger?.Error($"[Importer] Failed to import RomM GameID: {ROM.Id}");
                    continue;
                }
            }
            Logger?.Info($"[Importer] Finished adding new games for {Mapping.RomMPlatform?.Name}");

            if (!Plugin.Settings.KeepDeletedGames)
            {
                RemoveMissingGames(ImportedGamesIDs);
            }

            return games;
        }
        private Game ImportGame(RomMRom ROM, Guid StatusID)
        {
            //var rootInstallDir = PlayniteApi.ApplicationInfo.IsPortable
            //            ? Mapping.DestinationPathResolved.Replace(Playnite.Paths.ApplicationPath, ExpandableVariables.PlayniteDirectory)
            //            : Mapping.DestinationPathResolved;
            //var gameInstallDir = Path.Combine(rootInstallDir, Path.GetFileNameWithoutExtension(ROM.Name));
            //var pathToGame = Path.Combine(gameInstallDir, ROM.Name);

            var gameNameWithTags =
                        $"{ROM.Name}" +
                        $"{(ROM.Regions?.Count > 0 ? $" ({string.Join(", ", ROM.Regions)})" : "")}" +
                        $"{(!string.IsNullOrEmpty(ROM.Revision) ? $" (Rev {ROM.Revision})" : "")}" +
                        $"{(ROM.Tags?.Count > 0 ? $" ({string.Join(", ", ROM.Tags)})" : "")}";

            //var preferedRatingsBoard = PlayniteApi.ApplicationSettings.AgeRatingOrgPriority;
            //var agerating = ROM.Metadatum.Age_Ratings.Count > 0 ? new HashSet<MetadataProperty>(ROM.Metadatum.Age_Ratings.Where(r => r.Split(':')[0] == preferedRatingsBoard.ToString()).Select(r => new MetadataNameProperty(r.ToString()))) : null;

            var status = PlayniteApi.Library.CompletionStatuses.Get(StatusID.ToString());
            //var completionStatusProperty = status != null ? new MetadataNameProperty(status.Name) : null;

            ObservableCollection<WebLink> gameLinks = new ObservableCollection<WebLink>();
            if(ROM.SSId != null)
                gameLinks.Add(new WebLink("Screenscraper", $"https://www.screenscraper.fr/gameinfos.php?gameid={ROM.SSId}"));
            if (ROM.HasheousId != null)
                gameLinks.Add(new WebLink("Hasheous", $"https://hasheous.org/index.html?page=dataobjectdetail&type=game&id={ROM.HasheousId}"));
            if (ROM.RAId != null)
                gameLinks.Add(new WebLink("RetroAchievements", $"https://retroachievements.org/game/{ROM.RAId}"));
            if (ROM.HLTBId != null)
                gameLinks.Add(new WebLink("HowLongToBeat", $"https://howlongtobeat.com/game/{ROM.HLTBId}"));


            var game = new Game
            {
                SourceId = RomMLibraryPlugin.ExternalIdName, // ?
                LibraryId = RomMLibraryPlugin.Id, // ?
                LibraryGameId = $"{ROM.Id}:{ROM.SHA1}",

                Name = ROM.Name ?? throw new Exception("ROM doesn't have a name cannot continue!"),
                //Description = ROM.Summary,

                //Platforms = new HashSet<MetadataProperty> { new MetadataNameProperty(_mapping.RomMPlatform.Name ?? "") },
                //Regions = new HashSet<MetadataProperty>(ROM.Regions.Where(r => !string.IsNullOrEmpty(r)).Select(r => new MetadataNameProperty(r.ToString()))),
                //Genres = new HashSet<MetadataProperty>(ROM.Metadatum.Genres.Where(r => !string.IsNullOrEmpty(r)).Select(r => new MetadataNameProperty(r.ToString()))),
                //AgeRatings = agerating,
                //Series = new HashSet<MetadataProperty>(ROM.Metadatum.Franchises.Where(r => !string.IsNullOrEmpty(r)).Select(r => new MetadataNameProperty(r.ToString()))),
                //Features = new HashSet<MetadataProperty>(ROM.Metadatum.Gamemodes.Where(r => !string.IsNullOrEmpty(r)).Select(r => new MetadataNameProperty(r.ToString()))),
                //Categories = new HashSet<MetadataProperty>(ROM.Metadatum.Collections.Where(r => !string.IsNullOrEmpty(r)).Select(r => new MetadataNameProperty(r.ToString()))),

                ReleaseDate = ROM.Metadatum?.Release_Date != null ? new PartialDate(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(ROM.Metadatum.Release_Date.Value).ToLocalTime()) : null,
                CommunityScore = ROM.Metadatum?.Average_Rating != null ? (int)ROM.Metadatum.Average_Rating : -1,

                //CoverImage = !string.IsNullOrEmpty(ROM.PathCoverL) ? new MetadataFile($"{_plugin.Settings.RomMHost}{ROM.PathCoverL}") : null,

                //Favorite = _favourites.Exists(f => f == ROM.Id),
                //LastActivity = ROM.RomUser.LastPlayed,
                UserScore = ROM.RomUser?.Rating != null ? ROM.RomUser.Rating * 10 : -1, //RomM-Rating is 1-10, Playnite 1-100, so it can unfortunately only by synced one direction without loosing decimals
                //CompletionStatus = completionStatusProperty,
                Links = gameLinks,
                //Roms = new List<GameRom> { new GameRom(gameNameWithTags, pathToGame) },
                //InstallDirectory = gameInstallDir,
                //InstallState = File.Exists(pathToGame) ? InstallState.Installed : InstallState.Uninstalled,
                InstallSize = ROM.FileSizeBytes,
                //GameActions = new List<GameAction>
                //            {
                //                new GameAction
                //                {
                //                    Name = $"Play in {_mapping.Emulator.Name}",
                //                    Type = GameActionType.Emulator,
                //                    EmulatorId = _mapping.EmulatorId,
                //                    EmulatorProfileId = _mapping.EmulatorProfileId,
                //                    IsPlayAction = true,
                //                },
                //                new GameAction
                //                {
                //                    Type = GameActionType.URL,
                //                    Name = "View in RomM",
                //                    Path = _plugin.CombineUrl(_plugin.Settings.RomMHost, $"rom/{ROM.Id}"),
                //                    IsPlayAction = false
                //                }
                //            }
            };

            // Import new game
            //Game game = _plugin.Playnite.Database.ImportGame(metadata, _plugin);

            //if (ROM.HasManual)
            //{
            //    game.Manual = $"{_plugin.Settings.RomMHost}/assets/romm/resources/{ROM.ManualPath}";
            //}

            return game;
        }
        private void RemoveMissingGames(List<string> ImportedGames)
        {
            //var gamesInDatabase = PlayniteApi.Library.Games.Where(g =>
            //            g.Source != null && g.Source.Name == _plugin.Source.ToString() &&
            //            g.Platforms != null && g.Platforms.Any(p => p.Name == _mapping.RomMPlatform.Name)
            //        );

            Logger?.Info($"[Importer] Starting to remove not found games for {Mapping.RomMPlatform?.Name}.");

            //foreach (var game in gamesInDatabase)
            //{
            //    if (Args.CancelToken.IsCancellationRequested)
            //        break;
            //
            //    if (ImportedGames.Contains(game.GameId))
            //    {
            //        continue;
            //    }
            //
            //    PlayniteApi.Library.Games.Remove(game.Id);
            //    Logger?.Info($"[Importer] Removing {game.Name} - {game.Id} for {Mapping.RomMPlatform.Name}");
            //}

            Logger?.Info($"[Importer] Finished removing not found games for {Mapping.RomMPlatform?.Name}");
        }
        
        private bool UpdatedDeletedGame(RomMRom ROM)
        {
            // Check to see if a game already exists with an old romMId
            var oldgame = PlayniteApi.Library.Games.FirstOrDefault(g => g.LibraryId == RomMLibraryPlugin.Id && g.LibraryGameId?.Split(':')[1] == ROM.SHA1);
            if (oldgame != null)
            {
                oldgame.LibraryGameId = $"{ROM.Id}:{ROM.SHA1}";
                PlayniteApi.Library.Games.UpdateAsync(oldgame);
                return true;
            }
            else
            {
                return false;
            }
        }

        private MainSibling CheckForMainSibling(RomMRom ROM)
        {
            if(ROM.RomUser == null)
                return MainSibling.None;

            //Check to see if ROM is the main sibling
            if (ROM.RomUser.IsMainSibling)
                return MainSibling.Current;

            if(ROM.Siblings != null && ROM.Siblings.Count > 0)
            {
                //Find if there is a main sibling
                foreach (var sibling in ROM.Siblings)
                {
                    var siblingROM = ROMs.Find(x => x.Id == sibling.Id);

                    if (siblingROM?.RomUser != null && siblingROM.RomUser.IsMainSibling)
                    {
                        return MainSibling.Other;
                    }
                }
            }

            return MainSibling.None;
        }
        private void SaveGameData(RomMRom ROM)
        {
            string[] versionBreakdown = Plugin.Settings.ServerVersion.Split('.');
            float versionParsed = float.Parse(versionBreakdown[0]) + (float.Parse(versionBreakdown[1]) / 100);

            RomMRomLocal toSave = new RomMRomLocal();

            // Save base ROM data
            toSave.Id = ROM.Id;
            toSave.Name = ROM.Name;
            toSave.SHA1 = ROM.SHA1;
            toSave.HasMultipleFiles = ROM.HasMultipleFiles;
            if(!ROM.HasMultipleFiles)
            {
                var romfile = DetermineFile(ROM);
                if (romfile == null)
                {
                    Logger?.Error("[Importer] Unable to save ROM data as there is no rom file!");
                    return;
                }

                toSave.FileName = romfile.FileName;
                toSave.DownloadURL = versionParsed <= 4.7 ?
                                           $"{Plugin.Settings.Host.Trim('/')}/api/romsfiles/{romfile.Id}/content/{romfile.FileName}" : 
                                           $"{Plugin.Settings.Host.Trim('/')}/api/roms/{romfile.Id}/files/content/{romfile.FileName}"; // TODO: Sanitize the input so trim doesn't have the be called everywhere
            }
            else
            {
                toSave.FileName = ROM.FileName;
                toSave.DownloadURL = $"{Plugin.Settings.Host.Trim('/')}/api/roms/{ROM.Id}/content/{ROM.FileName}";
            } 
            toSave.IsSelected = false;
            toSave.MappingID = Mapping.MappingId;

            // Save sibling data
            if (Plugin.Settings.MergeRevisions && ROM.Siblings?.Count > 0)
            {
                toSave.Siblings = new List<RomMSavedSibing>();

                foreach (var sibling in ROM.Siblings)
                {
                    var siblingROM = ROMs.Find(x => x.Id == sibling.Id);
                    if(siblingROM != null)
                    {
                        RomMSavedSibing saveSibling = new RomMSavedSibing();

                        saveSibling.Id = siblingROM.Id;
                        saveSibling.HasMultipleFiles = siblingROM.HasMultipleFiles;
                        if (!siblingROM.HasMultipleFiles)
                        {
                            var romfile = DetermineFile(siblingROM);
                            if (romfile == null)
                            {
                                Logger?.Error("[Importer] Unable to save sibling ROM data as there is no rom file!");
                                continue;
                            }

                            saveSibling.FileName = romfile.FileName;
                            toSave.DownloadURL = versionParsed <= 4.7 ?
                                           $"{Plugin.Settings.Host.Trim('/')}/api/romsfiles/{romfile.Id}/content/{romfile.FileName}" :
                                           $"{Plugin.Settings.Host.Trim('/')}/api/roms/{romfile.Id}/files/content/{romfile.FileName}";
                        }
                        else
                        {
                            saveSibling.FileName = siblingROM.FileName;
                            saveSibling.DownloadURL = $"{Plugin.Settings.Host.Trim('/')}/api/roms/{siblingROM.Id}/content/{siblingROM.FileName}";
                        }          
                        saveSibling.IsSelected = false;
                        ROMs.First(x => x.Id == sibling.Id).Processed = true;

                        toSave.Siblings.Add(saveSibling);
                    }
                }
            }

            // Write data to file
            string json = JsonSerializer.Serialize(toSave);
            File.WriteAllText($"{Plugin.PluginDataPath.Trim('/')}/Games/{ROM.SHA1}.json", json);

        }

        //private Guid DetermineCompletionStatus(RomMRom ROM)
        //{
        //    string completionStatus;
        //    // Determine status in Playnite. Backlogged and "now playing" take precedent over the status options
        //    if (ROM.RomUser.Backlogged || ROM.RomUser.NowPlaying)
        //    {
        //        completionStatus = ROM.RomUser.NowPlaying ? RomMRomUser.CompletionStatusMap["now_playing"] : RomMRomUser.CompletionStatusMap["backlogged"];
        //    }
        //    else
        //    {
        //        completionStatus = RomMRomUser.CompletionStatusMap[ROM.RomUser.Status ?? "not_played"];
        //    }
        //
        //    _completionStatusMap.TryGetValue(completionStatus, out var statusId);
        //    
        //    var status = _plugin.Playnite.Database.CompletionStatuses.Get(statusId);
        //    var completionStatusProperty = status != null ? new MetadataNameProperty(status.Name) : null;
        //
        //    return statusId;
        //}    
    }
}
