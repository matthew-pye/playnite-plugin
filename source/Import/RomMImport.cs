using Graviton.Models;
using Graviton.Models.Notifications;
using Graviton.Models.RomM.Rom;

using Playnite;

using System.IO;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace Graviton.Import
{
    internal class RomMImport
    {
        private GravitonPlugin _plugin {get => GravitonPlugin.Instance; }
        private IPlayniteApi _playniteAPI { get => GravitonPlugin.PlayniteApi; }
        private ILogger _logger { get => GravitonPlugin.Logger; }

        CancellationToken CancelToken;
        EmulatorMapping Mapping;
        List<RomMRom> ROMs;

        public RomMImport(CancellationToken cancelToken, EmulatorMapping mapping, List<RomMRom> roms)
        {
            CancelToken = cancelToken;
            Mapping = mapping;
            ROMs = roms;
        }      

        // Main library import functions
        public async Task<(List<Game> NewGames, List<string> ProcessedGames)> ProcessData()
        {
            // Add all series, genres, collections, etc to playnite database
            PreProcessData();

            var games = new List<Game>();
            List<string> ImportedGamesIDs = new List<string>();

            if (Mapping.RomMPlatform?.Name != null && !_playniteAPI.Library.Platforms.Any(x => x.Name == Mapping.RomMPlatform.Name))
            {
                await _playniteAPI.Library.Platforms.AddAsync(new Platform(Mapping.RomMPlatform.Name));
            }
   
            // Process ROMs
            foreach (var ROM in ROMs)
            {
                if (CancelToken.IsCancellationRequested)
                    break;

                var result = await ProcessROM(ROM);
                if(result.HasValue)
                {
                    ImportedGamesIDs.Add(result.Value.gameID);

                    if(result.Value.newGame != null)
                        games.Add(result.Value.newGame);
                }
            }



            _logger?.Info($"[Importer] Finished adding new games for {Mapping.RomMPlatform?.Name}");

            if(_plugin.Settings.MergeRevisions)
            {
                MergeSiblings();
            }


            _logger?.Info($"[Importer] Finished import of ROMs for {Mapping.RomMPlatform?.Name}.");
            return (games, ImportedGamesIDs);
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

                var ROMAgeRatings = ROM.IgdbMetadata?.AgeRatings?.Select(x => new AgeRating($"{x.RatingBoard} {x.Rating}", $"{x.RatingBoard} {x.Rating}")).ToList();
                if (ROMAgeRatings != null)
                    ageRatings.AddRange(ROMAgeRatings);
            }


            if (genres.Count > 0)
            {
                _playniteAPI.Library.Genres.AddAsync(genres);
            }
            if (categories.Count > 0)
            {
                _playniteAPI.Library.Categories.AddAsync(categories);
            }
            if (series.Count > 0)
            {
                _playniteAPI.Library.Series.AddAsync(series);
            }
            if (features.Count > 0)
            {
                _playniteAPI.Library.Features.AddAsync(features);
            }
            if (ageRatings.Count > 0)
            {
                _playniteAPI.Library.AgeRatings.AddAsync(ageRatings);
            }
            if (regions.Count > 0)
            {
                _playniteAPI.Library.Regions.AddAsync(regions);
            }

            _playniteAPI.Library.Platforms.AddAsync(new Platform(Mapping.RomMPlatform!.Name, Mapping.RomMPlatform.Name));

        }

        private async Task<(string gameID, Game? newGame)?> ProcessROM(RomMRom ROM)
        {
            // Skip if ROM has no filename
            if (string.IsNullOrEmpty(ROM.FileName))
            {
                GravitonNotify.Add(new GravitonNotification($"graviton.proccess.{ROM.Id}.nofilename", Loc.GetString("NoFileNameWithID", ("ROMID", ROM.Id)), GravitonSeverity.Error));
                return null;
            }

            // Skip game import if the ROM is apart of the exclusion list
            //if (_plugin.Playnite.Database.ImportExclusions[Playnite.ImportExclusionItem.GetId($"{ROM.Id}:{ROM.SHA1}", _plugin.Id)] != null)
            //{
            //    Logger?.Warn($"[Importer] Excluding {ROM.Name} from import.");
            //    continue;
            //}

            // Some newer platforms don't get a hash value so we will compromise with this
            if (string.IsNullOrEmpty(ROM.SHA1))
            {
                var tohash = Encoding.UTF8.GetBytes($"{ROM.Id}{ROM.FileNameNoExt}");
                ROM.SHA1 = Encoding.UTF8.GetString(SHA1.HashData(tohash));
            }

            string gameID = $"{ROM.Id}:{ROM.SHA1}";

            // Save Game ROM data to file
            SaveGameData(ROM);

            // If keep deleted games is enabled and a deleted game gets re-added back to the server under a new romMId, Update playnite entry
            if (_plugin.Settings.KeepDeletedGames)
            {
                if (UpdatedDeletedGame(ROM))
                {
                    return new(gameID, null);
                }
            }

            var game = _playniteAPI.Library.Games.FirstOrDefault(g => g.LibraryGameId == gameID);
            if (game != null) // Skip full import if ROM has already been imported 
            {
                if(ROM.Collections != null)
                    game.Favorite = ROM.Collections.Any(x => x.Name == "Favorites") ? true : false;

                if (ROM.Notes != null)
                {
                    foreach (var note in ROM.Notes)
                    {
                        if(_playniteAPI.Library.GameNotes.Any(x => x.Id == game.Id))
                        {
                            var playnitenotes = _playniteAPI.Library.GameNotes.Where(x => x.Id == game.Id);
                            playnitenotes.First().Text = note.Note;
                            await _playniteAPI.Library.GameNotes.UpdateAsync(playnitenotes);
                        }
                        else
                        {
                            GameNote newNote = new(game.Id, note.Note, GameNoteFormat.Markdown);
                            newNote.Name = note.Title;
                            await _playniteAPI.Library.GameNotes.AddAsync(newNote);
                        }
                    }
                }

                return new(gameID, null);
            }
            else // Import game
            {
                var importedGame = ImportGame(ROM);
                if (importedGame != null)
                {
                    await _playniteAPI.Library.Games.AddAsync(importedGame);
                    await PostProccessROM(_playniteAPI.Library.Games.First(x => x.LibraryGameId == gameID).Id, ROM);
                    return new(gameID, importedGame);
                }
                else
                {
                    GravitonNotify.Add(new GravitonNotification($"graviton.import.game.{ROM.Id}.failed", $"Failed to import {ROM.Name} [ID:{ROM.Id}]", GravitonSeverity.Error));
                    return null;
                }
            }
        }

        private async Task PostProccessROM(string PlayniteID, RomMRom ROM)
        {
            await _playniteAPI.Library.GameDescriptions.AddAsync(new GameDescription(PlayniteID, ROM.Summary, GameDescriptionFormat.Markdown));

            if (ROM.Notes != null)
            {
                foreach (var note in ROM.Notes!)
                {
                    await _playniteAPI.Library.GameNotes.AddAsync(new GameNote(PlayniteID, note.Note, GameNoteFormat.Markdown));
                }
            }
        }

        private Game ImportGame(RomMRom ROM)
        {
            Game game = new Game();

            game.SourceId = GravitonPlugin.Id;
            game.LibraryId = GravitonPlugin.Id;
            game.LibraryGameId = $"{ROM.Id}:{ROM.SHA1}";

            game.Name = ROM.Name ?? throw new Exception("ROM doesn't have a name cannot continue!");
            game.EstimatedInstallSize = ROM.FileSizeBytes;
            if (ROM.Metadatum?.ReleaseDate != null && ROM.Metadatum?.ReleaseDate > 0)
                game.ReleaseDate = new PartialDate(new DateTime(((ROM.Metadatum?.ReleaseDate ?? 0) + 62135607600000) * 10000));

            game.CommunityScore = (ROM.Metadatum?.Average_Rating != null && ROM.Metadatum?.Average_Rating > 0) ? (int)ROM.Metadatum!.Average_Rating : -1;

            game.ObtainedDate = ROM.CreatedAt;
            game.AddedDate = DateTime.UtcNow;
            
            if (ROM.HLTBMetadata != null)
                game.TimeToBeatEstimated = new(ROM.HLTBMetadata.MainStory, ROM.HLTBMetadata.MainStoryExtra, ROM.HLTBMetadata.Completionist);

            game.GenreIds = ROM.Metadatum?.Genres?.ToHashSet();
            game.PlatformIds = new HashSet<string>([Mapping.RomMPlatform!.Name]);
            game.CategoryIds = ROM.Metadatum?.Collections?.ToHashSet();
            game.FeatureIds = ROM.Metadatum?.Gamemodes?.ToHashSet();
            game.SeriesIds = ROM.Metadatum?.Franchises?.ToHashSet();
            game.RegionIds = ROM.Regions?.ToHashSet();
            game.AgeRatingIds = ROM.IgdbMetadata?.AgeRatings?.Select(x => $"{x.RatingBoard}-{x.Rating}").ToHashSet();

            game.UserScore = (ROM.RomUser?.Rating != null && ROM.RomUser?.Rating > 0) ? ROM.RomUser!.Rating * 10 : -1;
            game.Favorite = ROM.Collections?.Any(x => x.Name == "Favorites") ?? false;
            game.Hidden = ROM.RomUser?.Hidden ?? false;
            game.LastPlayedDate = ROM.RomUser?.LastPlayed;

            game.CompletionStatusId = ROM.RomUser?.Status != null ? _playniteAPI.Library.CompletionStatuses.First(x => x.Name == RomMRomUser.CompletionStatusMap[ROM.RomUser.Status]).Id : null;

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

            //var rootInstallDir = _playniteApi.ApplicationInfo.IsPortable
            //            ? Mapping.DestinationPathResolved.Replace(Playnite.Paths.ApplicationPath, ExpandableVariables.PlayniteDirectory)
            //            : Mapping.DestinationPathResolved;
            //var gameInstallDir = Path.Combine(rootInstallDir, Path.GetFileNameWithoutExtension(ROM.Name));
            //var pathToGame = Path.Combine(gameInstallDir, ROM.Name);

            //if (ROM.HasManual)
            //{
            //    game.Manual = $"{_plugin.Settings.RomMHost}/assets/romm/resources/{ROM.ManualPath}";
            //}

            return game;
        }
    
        private bool UpdatedDeletedGame(RomMRom ROM)
        {
            // Check to see if a game already exists with an old romMId
            var oldgame = _playniteAPI.Library.Games.FirstOrDefault(g => g.LibraryId == GravitonPlugin.Id && g.LibraryGameId?.Split(':')[1] == ROM.SHA1);
            if (oldgame != null)
            {
                oldgame.LibraryGameId = $"{ROM.Id}:{ROM.SHA1}";
                _playniteAPI.Library.Games.UpdateAsync(oldgame);
                return true;
            }
            else
            {
                return false;
            }
        }

        private void MergeSiblings()
        {
            _logger.Info($"[Importer] Started merging new games for {Mapping.RomMPlatform?.Name}");

            foreach (var ROM in ROMs)
            {
                if (ROM.Siblings?.Count > 0)
                {
                    if(ROM.Processed)
                        continue;       

                    // Check to see if ROM already has a game relation setup
                    var game = _playniteAPI.Library.Games.First(x => x.LibraryGameId == $"{ROM.Id}:{ROM.SHA1}");
                    if (_playniteAPI.Library.GameRelations.Any(x => x.PrimaryGame == game.Id))
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

                            game = _playniteAPI.Library.Games.First(x => x.LibraryGameId == $"{siblingROM.Id}:{siblingROM.SHA1}");
                            if (_playniteAPI.Library.GameRelations.Any(x => x.PrimaryGame == game.Id))
                            {
                                var gamerelation = _playniteAPI.Library.GameRelations.First(x => x.PrimaryGame == game.Id);
                                gamerelation.LinkedGames.Add(_playniteAPI.Library.Games.First(x => x.LibraryGameId == $"{siblingROM.Id}:{siblingROM.SHA1}").Id);
                                _playniteAPI.Library.GameRelations.UpdateAsync(gamerelation);
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

                            game = _playniteAPI.Library.Games.First(x => x.LibraryGameId == $"{siblingROM.Id}:{siblingROM.SHA1}");
                            newgamerelation.LinkedGames.Add(game.Id);
                            ROMs.Find(x => x.Id == sibling.Id)?.Processed = true;
                        }

                        _playniteAPI.Library.GameRelations.AddAsync(newgamerelation);
                    }
                }
            }

            _logger.Info($"[Importer] Finished merging new games for {Mapping.RomMPlatform?.Name}");
        }

        private void SaveGameData(RomMRom ROM)
        {
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
                    _logger.Error("[Importer] Unable to save ROM data as there is no rom file!");
                    return;
                }

                toSave.FileName = romfile.FileName;
                toSave.DownloadURL = $"{_plugin.Settings.Host}/api/roms/{romfile.Id}/files/content/{romfile.FileName}";
            }
            else
            {
                toSave.FileName = ROM.FileName;
                toSave.DownloadURL = $"{_plugin.Settings.Host}/api/roms/{ROM.Id}/content/{ROM.FileName}";
            } 
            toSave.MappingID = Mapping.MappingId;

            // Write data to file
            string json = JsonSerializer.Serialize(toSave);
            File.WriteAllText($"{_plugin.PluginDataPath.Trim('/')}/Games/{ROM.SHA1}.json", json);

        }  
    }
}
