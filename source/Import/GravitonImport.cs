using Graviton.Models;
using Graviton.Models.Notifications;
using Graviton.Models.RomM.Rom;

using Playnite;

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Graviton.Import
{
    internal class GravitonImport
    {
        private GravitonPlugin _plugin;
        private IPlayniteApi _playniteAPI;
        private ILogger _logger;

        private CancellationToken _cancelToken;
        private EmulatorMapping _mapping;
        private List<RomMRom> _roms;

        private static Regex _SHA1Regex = new Regex("^[a-fA-F0-9]{40}$");

        public GravitonImport(GravitonPlugin plugin, IPlayniteApi playniteAPI, ILogger logger, CancellationToken cancelToken, EmulatorMapping mapping, List<RomMRom> roms)
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;

            _cancelToken = cancelToken;
            _mapping = mapping;
            _roms = roms;
        }

        // Main library import functions
        public async Task<(List<Game> NewGames, List<string> ProcessedGames)> ProcessData()
        {
            // Add all series, genres, collections, etc to playnite database
            await PreProcessData();

            var games = new List<Game>();
            List<string> ImportedGamesIDs = new List<string>();

            if (_mapping.RomMPlatform?.Name != null && !_playniteAPI.Library.Platforms.Any(x => x.Name == _mapping.RomMPlatform.Name))
            {
                await _playniteAPI.Library.Platforms.AddAsync(new Platform(_mapping.RomMPlatform.Name));
            }

            // Process ROMs
            foreach (var ROM in _roms)
            {
                if (_cancelToken.IsCancellationRequested)
                    break;

                var result = await ProcessROM(ROM);
                if (result.HasValue)
                {
                    ImportedGamesIDs.Add(result.Value.gameID);

                    if (result.Value.newGame != null)
                        games.Add(result.Value.newGame);
                }
            }

            _logger?.Info($"[Importer] Finished adding new games for {_mapping.RomMPlatform?.Name}");

            if (_plugin.Settings.MergeRevisions)
            {
                _logger?.Info($"[Importer] Started merging new games for {_mapping.RomMPlatform?.Name}");
                await GravitonSiblingMerger.MergeSiblings(_plugin, _roms);

                _logger?.Info($"[Importer] Finished merging new games for {_mapping.RomMPlatform?.Name}");
            }


            _logger?.Info($"[Importer] Finished import of ROMs for {_mapping.RomMPlatform?.Name}.");
            return (games, ImportedGamesIDs);
        }

        private async Task PreProcessData()
        {
            List<Genre> genres = new();
            List<Category> categories = new();
            List<Series> series = new();
            List<Feature> features = new();
            List<AgeRating> ageRatings = new();
            List<Region> regions = new();

            foreach (var ROM in _roms)
            {
                // Some newer platforms don't get a hash value so we will compromise with this
                if (string.IsNullOrEmpty(ROM.SHA1) || !_SHA1Regex.IsMatch(ROM.SHA1!))
                {
                    var tohash = Encoding.UTF8.GetBytes($"{ROM.Id}{ROM.FileNameNoExt}");
                    ROM.SHA1 = Convert.ToHexString(SHA1.HashData(tohash));
                }

                // Fail-safe incase none of these are set to true
                if (!ROM.HasSimpleSingleFile && !ROM.HasNestedSingleFile && !ROM.HasMultipleFiles)
                    ROM.HasMultipleFiles = true;

                var ROMGenres = ROM.Metadatum?.Genres?.Select(x => new Genre(x.ToLower(), x)).ToList();
                if (ROMGenres != null)
                    genres.AddRange(ROMGenres);

                var ROMCollections = ROM.Metadatum?.Collections?.Select(x => new Category(x.ToLower(), x)).ToList();
                ROMCollections?.RemoveAll(x => x.Name == "Favorites");
                if (ROMCollections != null)
                    categories.AddRange(ROMCollections);

                var ROMSeries = ROM.Metadatum?.Franchises?.Select(x => new Series(x.ToLower(), x)).ToList();
                if (ROMSeries != null)
                    series.AddRange(ROMSeries);

                var ROMfeatures = ROM.Metadatum?.Gamemodes?.Select(x => new Feature(x.ToLower(), x)).ToList();
                if (ROMfeatures != null)
                    features.AddRange(ROMfeatures);

                var ROMRegions = ROM.Regions?.Select(x => new Region(x.ToLower(), x)).ToList();
                if (ROMRegions != null)
                    regions.AddRange(ROMRegions);

                var ROMAgeRatings = ROM.IgdbMetadata?.AgeRatings?.Select(x => new AgeRating($"{x.RatingBoard.ToLower()} {x.Rating}", $"{x.RatingBoard} {x.Rating}")).ToList();
                if (ROMAgeRatings != null)
                    ageRatings.AddRange(ROMAgeRatings);
            }


            if (genres.Count > 0)
            {
                await _playniteAPI.Library.Genres.AddAsync(genres);
            }
            if (categories.Count > 0)
            {
                await _playniteAPI.Library.Categories.AddAsync(categories);
            }
            if (series.Count > 0)
            {
                await _playniteAPI.Library.Series.AddAsync(series);
            }
            if (features.Count > 0)
            {
                await _playniteAPI.Library.Features.AddAsync(features);
            }
            if (ageRatings.Count > 0)
            {
                await _playniteAPI.Library.AgeRatings.AddAsync(ageRatings);
            }
            if (regions.Count > 0)
            {
                await _playniteAPI.Library.Regions.AddAsync(regions);
            }

            await _playniteAPI.Library.Platforms.AddAsync(new Platform(_mapping.RomMPlatform!.Name, _mapping.RomMPlatform.Name));

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

            string gameID = $"{ROM.Id}:{ROM.SHA1}";

            // If keep deleted games is enabled and a deleted game gets re-added back to the server under a new romMId, Update playnite entry
            if (_plugin.Settings.KeepDeletedGames)
            {
                if (await UpdatedDeletedGame(ROM))
                {
                    return new(gameID, null);
                }
            }

            if (_plugin.ImportedGames!.ContainsKey(gameID) && !string.IsNullOrEmpty(_plugin.ImportedGames[gameID].PlayniteID)) // Skip full import if ROM has already been imported 
            {
                var game = _playniteAPI.Library.Games.Get(_plugin.ImportedGames[gameID].PlayniteID!)!;

                if (ROM.Collections != null)
                {
                    game.Favorite = ROM.Collections.Any(x => x.Name == "Favorites");
                }

                //if (ROM.Notes != null)
                //{
                //    foreach (var note in ROM.Notes)
                //    {
                //        var rootGameNote = _playniteAPI.Library.GameNotes.FirstOrDefault(x => x.Id == game.Id);
                //        if(rootGameNote != null)
                //        {
                //            rootGameNote.Text = note.Note;
                //            await _playniteAPI.Library.GameNotes.UpdateAsync(rootGameNote);
                //        }
                //        else
                //        {
                //            GameNote newNote = new(game.Id, note.Note, GameNoteFormat.Markdown);
                //            newNote.Name = note.Title;
                //            await _playniteAPI.Library.GameNotes.AddAsync(newNote);
                //        }
                //    }
                //}

                await _playniteAPI.Library.Games.UpdateAsync(game);

                ROM.Processed = true; // Skips the ROM being remerged if user has split the ROMs apart
                return new(gameID, null);
            }
            else // Import game
            {
                var importedGame = await ImportGame(ROM);
                if (importedGame != null)
                {
                    await _playniteAPI.Library.Games.AddAsync(importedGame);
                    RomMRomLocal.Build(_mapping.MappingId, ROM, importedGame.Id);
                    return new(gameID, importedGame);
                }
                else
                {
                    GravitonNotify.Add(new GravitonNotification($"graviton.import.game.{ROM.Id}.failed", Loc.GetString("ROMImportFailed", ("GameName", ROM.Name!), ("ROMID", ROM.Id)), GravitonSeverity.Error));
                    return null;
                }
            }
        }

        private async Task<Game> ImportGame(RomMRom ROM)
        {
            Game game = new Game();

            game.SourceId = GravitonPlugin.Id;
            game.LibraryId = GravitonPlugin.Id;
            game.LibraryGameId = $"{ROM.Id}:{ROM.SHA1}";

            game.Name = ROM.Name ?? throw new Exception("ROM doesn't have a name cannot continue!");
            game.EstimatedInstallSize = ROM.FileSizeBytes;
            if (ROM.Metadatum?.ReleaseDate != null && ROM.Metadatum?.ReleaseDate > 0)
                game.ReleaseDate = new PartialDate(new DateTime(((ROM.Metadatum?.ReleaseDate ?? 0) + 62135607600000) * 10000));

            game.CommunityScore = (ROM.Metadatum?.AverageRating != null && ROM.Metadatum?.AverageRating > 0) ? (int)ROM.Metadatum.AverageRating : -1;

            game.ObtainedDate = ROM.CreatedAt;
            game.AddedDate = DateTime.UtcNow;

            if (ROM.HLTBMetadata != null)
                game.TimeToBeatEstimated = new(ROM.HLTBMetadata.MainStory, ROM.HLTBMetadata.MainStoryExtra, ROM.HLTBMetadata.Completionist);

            game.GenreIds = ROM.Metadatum?.Genres != null ? ROM.Metadatum.Genres.Select(x => x.ToLower()).ToHashSet() : null;
            game.PlatformIds = new HashSet<string>([_mapping.RomMPlatform!.Name]);
            game.CategoryIds = ROM.Metadatum?.Collections != null ? ROM.Metadatum.Collections.Select(x => x.ToLower()).ToHashSet() : null;
            game.FeatureIds = ROM.Metadatum?.Gamemodes != null ? ROM.Metadatum.Gamemodes.Select(x => x.ToLower()).ToHashSet() : null;
            game.SeriesIds = ROM.Metadatum?.Franchises != null ? ROM.Metadatum.Franchises.Select(x => x.ToLower()).ToHashSet() : null;
            game.RegionIds = ROM.Regions != null ? ROM.Regions.Select(x => x.ToLower()).ToHashSet() : null;
            game.AgeRatingIds = ROM.IgdbMetadata?.AgeRatings != null ? ROM.IgdbMetadata.AgeRatings.Select(x => $"{x.RatingBoard.ToLower()} {x.Rating}").ToHashSet() : null;

            game.UserScore = (ROM.RomUser?.Rating != null && ROM.RomUser?.Rating > 0) ? ROM.RomUser!.Rating * 10 : -1;
            game.Favorite = ROM.Collections?.Any(x => x.Name == "Favorites") ?? false;
            game.Hidden = ROM.RomUser?.Hidden ?? false;
            game.LastPlayedDate = ROM.RomUser?.LastPlayed;

            if (ROM.RomUser?.Status != null && RomMRomUser.CompletionStatusMap.ContainsKey(ROM.RomUser.Status))
            {
                var playniteStatus = _playniteAPI.Library.CompletionStatuses.FirstOrDefault(x => x.Name == RomMRomUser.CompletionStatusMap[ROM.RomUser.Status]);
                if (playniteStatus != null)
                    game.CompletionStatusId = playniteStatus.Id;
            }

            game.Links = new();
            game.ExternalIdentifiers = new();
            game.ExternalIdentifiers?.Add(new("romm", ROM.Id.ToString()!));
            game.ExternalIdentifiers?.Add(new("gravitonmappingid", _mapping.MappingId.ToString()));
            if (ROM.IgdbId != null)
            {
                game.ExternalIdentifiers?.Add(new("igdb", ROM.IgdbId.ToString()!));

                if (ROM.Slug != null)
                {
                    game.Links.Add(new WebLink("igdb", $"https://www.igdb.com/games/{ROM.Slug}"));
                }
            }

            if (ROM.SSId != null)
            {
                game.Links.Add(new WebLink("screenscraper", $"https://www.screenscraper.fr/gameinfos.php?gameid={ROM.SSId}"));
                game.ExternalIdentifiers?.Add(new("screenscraper", ROM.SSId.ToString()!));
            }
            if (ROM.HasheousId != null)
            {
                game.Links.Add(new WebLink("hasheous", $"https://hasheous.org/index.html?page=dataobjectdetail&type=game&id={ROM.HasheousId}"));
                game.ExternalIdentifiers?.Add(new("hasheous", ROM.HasheousId.ToString()!));
            }
            if (ROM.RAId != null)
            {
                game.Links.Add(new WebLink("retroachievements", $"https://retroachievements.org/game/{ROM.RAId}"));
                game.ExternalIdentifiers?.Add(new("retroachievements", ROM.RAId.ToString()!));
            }
            if (ROM.HLTBId != null)
            {

                game.Links.Add(new WebLink("howlongtobeat", $"https://howlongtobeat.com/game/{ROM.HLTBId}"));
                game.ExternalIdentifiers?.Add(new("howlongtobeat", ROM.HLTBId.ToString()!));
            }

            await _playniteAPI.Library.GameDescriptions.AddAsync(new GameDescription(game.Id, ROM.Summary, GameDescriptionFormat.Markdown));

            if (ROM.Notes != null)
            {
                foreach (var note in ROM.Notes!)
                {
                    GameNote newNote = new GameNote()
                    {
                        Id = game.Id,
                        Name = note.Title,
                        Text = note.Note,
                        Format = GameNoteFormat.Markdown
                    };

                    await _playniteAPI.Library.GameNotes.AddAsync(newNote);
                }
            }

            return game;
        }

        private async Task<bool> UpdatedDeletedGame(RomMRom ROM)
        {
            // Check to see if a game already exists with an old romMId
            var oldgame = _plugin.ImportedGames!.FirstOrDefault(g =>
            {
                var splitID = g.Key?.Split(':');
                return splitID?.Length == 2 && splitID[1] == ROM.SHA1;
            });

            if (oldgame.Value != null)
            {
                var game = _playniteAPI.Library.Games.Get(oldgame.Value.PlayniteID!)!;

                game.LibraryGameId = $"{ROM.Id}:{ROM.SHA1}";
                oldgame.Value.Id = ROM.Id;
                await _playniteAPI.Library.Games.UpdateAsync(game);

                _plugin.ImportedGames!.TryRemove(oldgame.Key, out _);
                oldgame.Value.Save();

                return true;
            }
            else
            {
                return false;
            }
        }

    }
}
