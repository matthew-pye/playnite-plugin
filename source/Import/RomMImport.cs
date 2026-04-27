using Playnite;

using RomMLibrary.Models;
using RomMLibrary.Models.RomM.Rom;

using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace RomMLibrary.Import
{
    struct ProcessedGame
    {
        public ProcessedGame(string gameID)
        {
            GameID = gameID;
        }
        public ProcessedGame(string gameID, Game newGame)
        {
            GameID = gameID;
            NewGame = newGame;
        }

        public string GameID;
        public Game? NewGame;
    }

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

        // Main library import functions
        public List<Game> ProcessData()
        {
            // Add all series, genres, collections, etc to playnite database
            PreProcessData();

            var games = new List<Game>();
            List<string> ImportedGamesIDs = new List<string>();

            if (Mapping.RomMPlatform?.Name != null && !PlayniteApi.Library.Platforms.Any(x => x.Name == Mapping.RomMPlatform.Name))
            {
                PlayniteApi.Library.Platforms.AddAsync(new Platform(Mapping.RomMPlatform.Name));
            }
   
            // Process ROMs
            foreach (var ROM in ROMs)
            {
                if (CancelToken.IsCancellationRequested)
                    break;

                ProcessedGame? result = ProcessROM(ROM);
                if(result.HasValue)
                {
                    ImportedGamesIDs.Add(result.Value.GameID);

                    if(result.Value.NewGame != null)
                        games.Add(result.Value.NewGame);
                }
            }
            Logger?.Info($"[Importer] Finished adding new games for {Mapping.RomMPlatform?.Name}");

            if(Plugin.Settings.MergeRevisions)
            {
                MergeSiblings();
            }

            if (!Plugin.Settings.KeepDeletedGames)
            {
                RemoveMissingGames(ImportedGamesIDs);
            }

            return games;
        }

        private RomMFile? DetermineFile(RomMRom ROM)
        {
            if (ROM.Files == null)
                return null;

            if (ROM.Files.Count > 1)
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

        private void PreProcessData()
        {
            List<Genre> genres = new();
            List<Category> categories = new();
            List<Series> series = new();
            List<Feature> features = new();
            List<AgeRating> ageRatings = new();
            List<Region> regions = new();

            foreach (var ROM in ROMs)
            {
                // Some newer platforms don't get a hash value so we will compromise with this
                if (string.IsNullOrEmpty(ROM.SHA1))
                {
                    var tohash = Encoding.UTF8.GetBytes($"{ROM.Name}{ROM.FileSizeBytes}");
                    ROM.SHA1 = Encoding.UTF8.GetString(SHA1.HashData(tohash));
                }

                // Fail-safe incase none of these are set to true
                if (!ROM.HasSimpleSingleFile & !ROM.HasNestedSingleFile & !ROM.HasMultipleFiles)
                    ROM.HasMultipleFiles = true;

                var ROMGenres = ROM.Metadatum?.Genres?.Select(x => new Genre(x, x)).ToList();
                if(ROMGenres != null)
                    genres.AddRange(ROMGenres);

                var ROMCollections= ROM.Metadatum?.Collections?.Select(x => new Category(x, x)).ToList();
                ROMCollections?.Remove(ROMCollections.Where(x => x.Name == "Favorites"));
                if (ROMCollections != null)
                    categories.AddRange(ROMCollections);

                var ROMSeries = ROM.Metadatum?.Franchises?.Select(x => new Series(x, x)).ToList();
                if (ROMSeries != null)
                    series.AddRange(ROMSeries);

                var ROMfeatures = ROM.Metadatum?.Gamemodes?.Select(x => new Feature(x, x)).ToList();
                if (ROMfeatures != null)
                    features.AddRange(ROMfeatures);

                var ROMRegions = ROM.Regions?.Select(x => new Region(x, x)).ToList();
                if (ROMRegions != null)
                    regions.AddRange(ROMRegions);

                var ROMAgeRatings = ROM.IgdbMetadata?.AgeRatings?.Select(x => new AgeRating($"{x.RatingBoard}-{x.Rating}", $"{x.RatingBoard} {x.Rating}")).ToList();
                if (ROMAgeRatings != null)
                    ageRatings.AddRange(ROMAgeRatings);
            }


            if (genres.Count > 0)
            {
                PlayniteApi.Library.Genres.AddAsync(genres);
            }
            if (categories.Count > 0)
            {
                PlayniteApi.Library.Categories.AddAsync(categories);
            }
            if (series.Count > 0)
            {
                PlayniteApi.Library.Series.AddAsync(series);
            }
            if (features.Count > 0)
            {
                PlayniteApi.Library.Features.AddAsync(features);
            }
            if (ageRatings.Count > 0)
            {
                PlayniteApi.Library.AgeRatings.AddAsync(ageRatings);
            }
            if (regions.Count > 0)
            {
                PlayniteApi.Library.Regions.AddAsync(regions);
            }

            PlayniteApi.Library.Platforms.AddAsync(new Platform(Mapping.RomMPlatform.Name, Mapping.RomMPlatform.Name));

        }

        private ProcessedGame? ProcessROM(RomMRom ROM)
        {
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
                PlayniteApi.Notifications.Add(new NotificationMessage(RomMLibraryPlugin.Id, Loc.GetString("NoFileNameWithID", ("ROMID", ROM.Id)), NotificationSeverity.Error));
                return null;
            }

            string gameID = $"{ROM.Id}:{ROM.SHA1}";

            // Save Game ROM data to file
            SaveGameData(ROM);

            // If keep deleted games is enabled and a deleted game gets re-added back to the server under a new romMId, Update playnite entry
            if (Plugin.Settings.KeepDeletedGames)
            {
                if (UpdatedDeletedGame(ROM))
                {
                    return new(gameID);
                }
            }

            var game = PlayniteApi.Library.Games.FirstOrDefault(g => g.LibraryGameId == gameID);
            if (game != null) // Skip full import if ROM has already been imported 
            {
                // Sync user data
                if (Plugin.Settings.KeepRomMSynced)
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


                return new(gameID);
            }
            else // Import game
            {
                var importedGame = ImportGame(ROM);
                if (importedGame != null)
                {
                    PlayniteApi.Library.Games.AddAsync(importedGame);
                    return new(gameID, importedGame);
                }
                else
                {
                    Logger?.Error($"[Importer] Failed to import RomM GameID: {ROM.Id}");
                    return null;
                }
            }
        }

        private Game ImportGame(RomMRom ROM)
        {
            Game game = new Game();

            game.SourceId = RomMLibraryPlugin.Id;
            game.LibraryId = RomMLibraryPlugin.Id;
            game.LibraryGameId = $"{ROM.Id}:{ROM.SHA1}";

            game.Name = ROM.Name ?? throw new Exception("ROM doesn't have a name cannot continue!");
            game.EstimatedInstallSize = ROM.FileSizeBytes;
            if (ROM.Metadatum?.ReleaseDate != null && ROM.Metadatum?.ReleaseDate > 0)
                game.ReleaseDate = new PartialDate(new DateTime(((ROM.Metadatum?.ReleaseDate ?? 0) + 62135607600000) * 10000));
            game.CommunityScore = (ROM.Metadatum?.Average_Rating != null && ROM.Metadatum?.Average_Rating > 0) ? (int)ROM.Metadatum!.Average_Rating : -1;
            game.ObtainedDate = ROM.CreatedAt;
            if (ROM.HLTBMetadata != null)
                game.TimeToBeatEstimated = new(ROM.HLTBMetadata.MainStory, ROM.HLTBMetadata.MainStoryExtra, ROM.HLTBMetadata.Completionist);

            game.GenreIds = ROM.Metadatum?.Genres?.ToHashSet();
            game.PlatformIds = new HashSet<string>([Mapping.RomMPlatform.Name]);
            game.CategoryIds = ROM.Metadatum?.Collections?.ToHashSet();
            game.FeatureIds = ROM.Metadatum?.Gamemodes?.ToHashSet();
            game.SeriesIds = ROM.Metadatum?.Franchises?.ToHashSet();
            game.RegionIds = ROM.Regions?.ToHashSet();
            game.AgeRatingIds = ROM.IgdbMetadata?.AgeRatings?.Select(x => $"{x.RatingBoard}-{x.Rating}").ToHashSet();

            game.UserScore = (ROM.RomUser?.Rating != null && ROM.RomUser?.Rating > 0) ? ROM.RomUser!.Rating * 10 : -1;
            game.Favorite = Plugin.StatusController?.PullFavourites()?.RomIDs?.Any(x => x == ROM.Id) ?? false;
            game.Hidden = ROM.RomUser?.Hidden ?? false;
            game.LastPlayedDate = ROM.RomUser?.LastPlayed;
            game.CompletionStatusId = ROM.RomUser?.Status != null ? RomMRomUser.CompletionStatusMap[ROM.RomUser.Status] : null;

            game.Links = new();
            game.ExternalIdentifiers = new();
            game.ExternalIdentifiers?.Add(new("RomM", ROM.Id.ToString()!));
            if (ROM.SSId != null)
            {
                game.Links.Add(new WebLink("Screenscraper", $"https://www.screenscraper.fr/gameinfos.php?gameid={ROM.SSId}"));
                game.ExternalIdentifiers?.Add(new("Screenscraper", ROM.SSId.ToString()!));
            }                  
            if (ROM.HasheousId != null)
            {
                game.Links.Add(new WebLink("Hasheous", $"https://hasheous.org/index.html?page=dataobjectdetail&type=game&id={ROM.HasheousId}"));
                game.ExternalIdentifiers?.Add(new("Hasheous", ROM.HasheousId.ToString()!));
            }                
            if (ROM.RAId != null)
            {
                game.Links.Add(new WebLink("RetroAchievements", $"https://retroachievements.org/game/{ROM.RAId}"));
                game.ExternalIdentifiers?.Add(new("RetroAchievements", ROM.RAId.ToString()!));
            }    
            if (ROM.HLTBId != null)
            {
                game.Links.Add(new WebLink("HowLongToBeat", $"https://howlongtobeat.com/game/{ROM.HLTBId}"));
                game.ExternalIdentifiers?.Add(new("HowLongToBeat", ROM.HLTBId.ToString()!));
            }

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

            //if (ROM.HasManual)
            //{
            //    game.Manual = $"{_plugin.Settings.RomMHost}/assets/romm/resources/{ROM.ManualPath}";
            //}

            return game;
        }

        private void RemoveMissingGames(List<string> ImportedGames)
        {
            var gamesInDatabase = PlayniteApi.Library.Games.Where(g =>
                        g.SourceId != null && g.SourceId == RomMLibraryPlugin.Id &&
                        g.PlatformIds != null && g.PlatformIds.Any(p => p == Mapping.RomMPlatform.Name)
                    );

            Logger?.Info($"[Importer] Starting to remove not found games for {Mapping.RomMPlatform?.Name}.");

            foreach (var game in gamesInDatabase)
            {
            
                if (ImportedGames.Contains(game.LibraryGameId!))
                {
                    continue;
                }
            
                PlayniteApi.Library.Games.RemoveAsync(game.Id);
                Logger?.Info($"[Importer] Removing {game.Name} - {game.Id} for {Mapping.RomMPlatform?.Name}");
            }

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

        private void MergeSiblings()
        {
            Logger?.Info($"[Importer] Started merging new games for {Mapping.RomMPlatform?.Name}");

            foreach (var ROM in ROMs)
            {
                if (ROM.Siblings?.Count > 0)
                {
                    if(ROM.Processed)
                        continue;       

                    // Check to see if ROM already has a game relation setup
                    var game = PlayniteApi.Library.Games.First(x => x.LibraryGameId == $"{ROM.Id}:{ROM.SHA1}");
                    if (PlayniteApi.Library.GameRelations.Any(x => x.PrimaryGame == game.Id))
                    {
                        continue;
                    }
                    else
                    {
                        bool GameRelationAlreadySetup = false;

                        foreach (var sibling in ROM.Siblings!)
                        {
                            var siblingROM = ROMs.Find(x => x.Id == sibling.Id);
                            if (siblingROM == null)
                                continue;

                            game = PlayniteApi.Library.Games.First(x => x.LibraryGameId == $"{siblingROM.Id}:{siblingROM.SHA1}");
                            if (PlayniteApi.Library.GameRelations.Any(x => x.PrimaryGame == game.Id))
                            {
                                var gamerelation = PlayniteApi.Library.GameRelations.First(x => x.PrimaryGame == game.Id);
                                gamerelation.LinkedGames.Add(PlayniteApi.Library.Games.First(x => x.LibraryGameId == $"{siblingROM.Id}:{siblingROM.SHA1}").Id);
                                PlayniteApi.Library.GameRelations.UpdateAsync(gamerelation);
                                GameRelationAlreadySetup = true;
                                break;
                            }
                        }

                        if (GameRelationAlreadySetup)
                            continue;
                    }

                    //Check to see if ROM is the main sibling
                    MainSibling isMainSibling = MainSibling.None;
                    if (ROM.RomUser != null && ROM.RomUser.IsMainSibling)
                    {
                        isMainSibling = MainSibling.Current;
                    }
                    else if (ROM.Siblings != null && ROM.Siblings.Count > 0)
                    {
                        //Find if there is a main sibling
                        foreach (var sibling in ROM.Siblings)
                        {
                            var siblingROM = ROMs.Find(x => x.Id == sibling.Id);

                            if (siblingROM?.RomUser != null && siblingROM.RomUser.IsMainSibling)
                            {
                                isMainSibling = MainSibling.Other;
                            }
                        }
                    }

                    // Create new game relation
                    if(isMainSibling != MainSibling.Other) 
                    {
                        GameRelation newgamerelation = new GameRelation();
                        newgamerelation.PrimaryGame = game.Id;
                        ROM.Processed = true;
                        foreach (var sibling in ROM.Siblings!)
                        {
                            var siblingROM = ROMs.Find(x => x.Id == sibling.Id);
                            if (siblingROM == null)
                                continue;

                            game = PlayniteApi.Library.Games.First(x => x.LibraryGameId == $"{siblingROM.Id}:{siblingROM.SHA1}");
                            newgamerelation.LinkedGames.Add(game.Id);
                            ROMs.Find(x => x.Id == sibling.Id)?.Processed = true;
                        }

                        PlayniteApi.Library.GameRelations.AddAsync(newgamerelation);
                    }
                }
            }

            Logger?.Info($"[Importer] Finished merging new games for {Mapping.RomMPlatform?.Name}");
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
                                           $"{Plugin.Settings.Host}/api/romsfiles/{romfile.Id}/content/{romfile.FileName}" : 
                                           $"{Plugin.Settings.Host}/api/roms/{romfile.Id}/files/content/{romfile.FileName}"; // TODO: Sanitize the input so trim doesn't have the be called everywhere
            }
            else
            {
                toSave.FileName = ROM.FileName;
                toSave.DownloadURL = $"{Plugin.Settings.Host}/api/roms/{ROM.Id}/content/{ROM.FileName}";
            } 
            toSave.MappingID = Mapping.MappingId;

            // Write data to file
            string json = JsonSerializer.Serialize(toSave);
            File.WriteAllText($"{Plugin.PluginDataPath.Trim('/')}/Games/{ROM.SHA1}.json", json);

        }  
    }
}
