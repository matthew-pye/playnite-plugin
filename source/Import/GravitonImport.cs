using Graviton.Models;
using Graviton.Models.Notifications;
using Graviton.Models.RomM.Collection;
using Graviton.Models.RomM.Rom;

using Playnite;

using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using static Playnite.Plugin;

namespace Graviton.Import
{
    internal class GravitonImport
    {
        private GravitonPlugin _plugin;
        private IPlayniteApi _playniteAPI;
        private ILogger _logger;

        private ImportGamesArgs _args;
        private EmulatorMapping _mapping;
        private List<RomMRom> _roms;
        private List<RomMCollection> _collections;

        private static Regex _SHA1Regex = new Regex("^[a-fA-F0-9]{40}$");

        public GravitonImport(GravitonPlugin plugin, IPlayniteApi playniteAPI, ILogger logger, ImportGamesArgs args, EmulatorMapping mapping, List<RomMRom> roms, List<RomMCollection> collections)
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;

            _args = args;
            _mapping = mapping;
            _roms = roms;
            _collections = collections;
        }

        // Main library import functions
        public async Task<(List<Game> NewGames, List<string> ProcessedGames)> ProcessData()
        {
            // Add all series, genres, collections, etc to playnite database
            await PreProcessData();

            var games = new List<Game>();
            List<string> ImportedGamesIDs = new List<string>();

            // Process ROMs
            foreach (var ROM in _roms)
            {
                if (_args.CancelToken.IsCancellationRequested)
                    break;

                string gameID = $"{ROM.Id}";
                try
                {
                    var result = await ProcessROM(ROM, gameID);
                    if (result.HasValue)
                    {
                        ImportedGamesIDs.Add(result.Value.gameID);
                        if (result.Value.newGame != null) games.Add(result.Value.newGame);
                    }
                }
                catch (Exception ex)
                {
                    if (_plugin.ImportedGames.ContainsKey(gameID))
                    {
                        ImportedGamesIDs.Add(gameID);
                        _logger?.Warn($"[Importer] Failed to re-process already-imported ROM {ROM.Id} ({ROM.Name}), keeping existing entry: {ex.Message}");
                    }
                    else
                    {
                        _logger?.Warn($"[Importer] Failed to import new ROM {ROM.Id} ({ROM.Name}), skipping: {ex.Message}");
                    }
                    GravitonNotify.Add(new GravitonNotification($"graviton.ROM.import.failed", "One or more ROMs failed to be imported!", GravitonSeverity.Warn, ex));
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

            foreach (var collection in _collections)
            {
                if (_args.CancelToken.IsCancellationRequested)
                    break;

                if (!string.IsNullOrEmpty(collection.Name) && collection.RomIDs.Any(x => _roms.Any(y => y.Id == x)))
                {
                    categories.Add(new Category(collection.Name.ToLower(), collection.Name));
                }
            }


            foreach (var ROM in _roms)
            {
                if (_args.CancelToken.IsCancellationRequested)
                    break;

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

            await _playniteAPI.Library.Platforms.AddAsync(new Platform(_mapping.RomMPlatform!.Name.ToLower(), _mapping.RomMPlatform.Name));

        }

        private async Task<(string gameID, Game? newGame)?> ProcessROM(RomMRom ROM, string gameID)
        {
            // Skip if ROM has no filename
            if (string.IsNullOrEmpty(ROM.FileName))
            {
                GravitonNotify.Add(new GravitonNotification($"graviton.proccess.{ROM.Id}.nofilename", Loc.GetString("NoFileNameWithID", ("ROMID", ROM.Id)), GravitonSeverity.Error));
                return null;
            }

            // If keep deleted games is enabled and a deleted game gets re-added back to the server under a new romMId, Update playnite entry
            if (_plugin.Settings.KeepDeletedGames)
            {
                if (await UpdatedDeletedGame(ROM))
                {
                    return new(gameID, null);
                }
            }

            if (_plugin.ImportedGames.ContainsKey(gameID) && !string.IsNullOrEmpty(_plugin.ImportedGames[gameID].PlayniteID)) // Skip full import if ROM has already been imported 
            {
                return await UpdateGame(ROM, gameID);
            }
            else // Import game
            {
                return await ImportNewGame(ROM, gameID);
            }
        }

        private async Task<(string gameID, Game? newGame)?> UpdateGame(RomMRom ROM, string gameID)
        {
            var game = _playniteAPI.Library.Games.Get(_plugin.ImportedGames[gameID].PlayniteID!);

            if (game == null)
            {
                GravitonNotify.Add(new GravitonNotification($"graviton.import.game.{ROM.Id}.failed", Loc.GetString("ROMUpdateFailed", ("GameName", ROM.Name!), ("ROMID", ROM.Id)), GravitonSeverity.Error));
                return new(gameID, null);
            }

            // Import new game sessions
            if (_args.SessionImport == SessionImportMode.Always && _plugin.Settings.ImportPlaysessions != Models.RomM.PlaySessions.ImportPlaySessions.None)
            {
                var sessions = await _plugin.StatusController!.FetchPlaySessions(ROM.Id);

                if (sessions != null && sessions.Count > 0)
                {
                    var playnitesessions = _playniteAPI.Library.GameSessions.Where(x => x.GameId == game.Id).ToList();
                    List<GameSession> newsessions = _playniteAPI.Library.GameSessions.Where(x => x.GameId == game.Id).ToList();

                    foreach (var session in sessions)
                    {
                        if (string.IsNullOrEmpty(session.StartTime))
                            continue;

                        var sessiondate = DateTime.Parse(session.StartTime ?? "0");
                        sessiondate.AddMilliseconds(-sessiondate.Millisecond);

                        // Check to see if session has already been imported if not add it
                        if (!playnitesessions.Any(x => x.Date.HasValue && DateTime.Compare(x.Date.Value.AddMilliseconds(-x.Date.Value.Millisecond).UtcDateTime, sessiondate) == 0))
                        {
                            GameSession newssession = new(game.LibraryGameId!, GravitonPlugin.Id, session.ID.ToString())
                            {
                                Date = sessiondate.ToLocalTime(),
                                Length = (uint)(session.Duration / 1000)
                            };

                            if (game.SessionIds == null)
                                game.SessionIds = new();

                            game.SessionIds.Add(newssession.Id);
                            newsessions.Add(newssession);

                        }
                    }

                    if(newsessions.Count > 0)
                        await _playniteAPI.Library.GameSessions.AddAsync(newsessions);
                }
            }

            if (ROM.Collections != null)
            {
                game.Favorite = ROM.Collections.Any(x => x.Name == "Favorites");
            }

            // Update categories the ROM is in
            if (game.CategoryIds == null)
                game.CategoryIds = ROM.Metadatum?.Collections?.Select(x => x.ToLower()).ToHashSet();
            else
                game.CategoryIds.AddRange(ROM.Metadatum?.Collections?.Select(x => x.ToLower()).ToHashSet());

            await _playniteAPI.Library.Games.UpdateAsync(game);
            _plugin.ImportedGames[gameID].Resync(ROM);

            ROM.Processed = true; // Skips the ROM being remerged if user has split the ROMs apart
            return new(gameID, null);
        }

        private async Task<(string gameID, Game? newGame)?> ImportNewGame(RomMRom ROM, string gameID)
        {
            var importedGame = await GenerateGame(ROM);
            if (importedGame != null)
            {
                // Import game sessions
                if (_args.SessionImport != SessionImportMode.Never && _plugin.Settings.ImportPlaysessions != Models.RomM.PlaySessions.ImportPlaySessions.None)
                {
                    var sessions = await _plugin.StatusController!.FetchPlaySessions(ROM.Id);

                    if (sessions != null && sessions.Count > 0)
                    {
                        List<GameSession> newsessions = new();

                        foreach (var session in sessions)
                        {
                            if (string.IsNullOrEmpty(session.StartTime))
                                continue;

                            var sessiondate = DateTime.Parse(session.StartTime ?? "0");
                            sessiondate.AddMilliseconds(-sessiondate.Millisecond);

                            newsessions.Add(new(importedGame.LibraryGameId!, GravitonPlugin.Id, session.ID.ToString())
                            {
                                Date = sessiondate.ToLocalTime(),
                                Length = (uint)(session.Duration / 1000)
                            });

                            if (importedGame.SessionIds == null)
                                importedGame.SessionIds = new();

                            importedGame.SessionIds.Add(session.ID.ToString());

                        }

                        await _playniteAPI.Library.GameSessions.AddAsync(newsessions);
                    }
                }

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

        private async Task<Game?> GenerateGame(RomMRom ROM)
        {
            Game game = new Game();

            game.SourceId = GravitonPlugin.Id;
            game.LibraryId = GravitonPlugin.Id;
            game.LibraryGameId = $"{ROM.Id}";

            if(string.IsNullOrEmpty(ROM.Name))
                return null;

            game.Name = ROM.Name;
            game.SortingName = ROM.SortName ?? "";
            await _playniteAPI.Library.GameDescriptions.AddAsync(new GameDescription(game.Id, ROM.Summary, GameDescriptionFormat.Markdown));

            game.EstimatedInstallSize = ROM.FileSizeBytes;
            if (ROM.Metadatum?.ReleaseDate != null && ROM.Metadatum?.ReleaseDate > 0)
                game.ReleaseDate = new PartialDate(new DateTime(((ROM.Metadatum.ReleaseDate ?? 0) + 62135596800000) * 10000));

            game.CommunityScore = (ROM.Metadatum?.AverageRating != null && ROM.Metadatum?.AverageRating > 0) ? (int)ROM.Metadatum.AverageRating : -1;

            game.ObtainedDate = ROM.CreatedAt;
            game.AddedDate = DateTime.UtcNow;

            if (ROM.HLTBMetadata != null)
                game.TimeToBeatEstimated = new(ROM.HLTBMetadata.MainStory, ROM.HLTBMetadata.MainStoryExtra, ROM.HLTBMetadata.Completionist);

            game.GenreIds = ROM.Metadatum?.Genres != null ? ROM.Metadatum.Genres.Select(x => x.ToLower()).ToHashSet() : null;
            game.PlatformIds = new HashSet<string>([_mapping.RomMPlatform!.Name.ToLower() ?? ""]);
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
            game.ExternalIdentifiers?.Add(new("romm", ROM.Id.ToString()));
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

            game.InstallDirectory = _mapping.DestinationPathResolved;
            if(!ROM.HasMultipleFiles)
            {
                var relativeROMPath = ROM.FullPath?.Replace(ROM.FileSystemPath ?? "", "");
                game.InstallState = File.Exists($"{_mapping.DestinationPathResolved}\\{relativeROMPath}") ? InstallState.Installed : InstallState.Uninstalled;
            }

            return game;
        }

        private async Task<bool> UpdatedDeletedGame(RomMRom ROM)
        {
            // Check to see if a game already exists with an old romMId
            var oldgame = _plugin.ImportedGames.FirstOrDefault(g => g.Value.SHA1 == ROM.SHA1);

            if (oldgame.Value != null)
            {
                var game = _playniteAPI.Library.Games.Get(oldgame.Value.PlayniteID!)!;

                game.LibraryGameId = $"{ROM.Id}";
                oldgame.Value.Id = ROM.Id;
                await _playniteAPI.Library.Games.UpdateAsync(game);

                _plugin.ImportedGames.TryRemove(oldgame.Key, out _);
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
