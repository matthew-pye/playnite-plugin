using Playnite;

using RomM.Properties;

using RomMLibrary;
using RomMLibrary.Models.RomM.Rom;
using RomMLibrary.Settings;

using System.Net.Http;
using System.Text.Json;

using static Playnite.MetadataProvider;

namespace RomM.Import
{
    public class RomMLibraryMetadataProviderGameSession : MetadataProviderGameSession
    {
        private readonly RomMLibraryPlugin Plugin;
        private readonly ILogger Logger = LogManager.GetLogger();
        private RomMRom? ROM = null;

        public RomMLibraryMetadataProviderGameSession(RomMLibraryPlugin plugin, Game game) : base(game)
        {
            Plugin = plugin;
            if (game.LibraryId == "RomMLibrary")
            {
                try
                {
                    int romMId;
                    if (!int.TryParse(game.LibraryGameId?.Split(':')[0], out romMId))
                        throw new Exception($"[Metadata] {game.Name} GameID is malformed!");

                    RomMRom romMGame = FetchRom(romMId.ToString());

                    if (romMGame == null)
                        throw new Exception($"[Metadata] {game.Name} failed to get game!");

                    ROM = romMGame;

                }
                catch (Exception Ex)
                {
                    Logger.Error($"[Metadata] {game.Name} failed to get metadata\n{Ex}!");
                }
            }

            
        }

        public RomMRom FetchRom(string romId)
        {
            string romUrl = $"{Plugin.Settings.Host.Trim('/')}/api/roms/{romId}";
            try
            {
                HttpResponseMessage response = HttpClientSingleton.Instance.GetAsync(romUrl).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();

                string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return JsonSerializer.Deserialize<RomMRom>(body) ?? throw new Exception("Unable to deserialize ROM!");
            }
            catch (HttpRequestException e)
            {
                Logger.Error($"Request exception: {e.Message}");
                return null!;
            }
        }

        public override async Task<object?> GetDataAsync(GetDataArgs dataArgs)
        {
            if(ROM == null)
                return null;

            switch (dataArgs.DataId)
            {
                case BuiltInGameDataId.Name:
                    return ROM.Name;
                case BuiltInGameDataId.Description:
                    return ROM.Summary;
                case BuiltInGameDataId.Note:
                    return null;
                case BuiltInGameDataId.DesktopCover:
                    return ROM.HasCover ? ROM.PathCoverL : null;
                case BuiltInGameDataId.Genres:
                    return ROM.Metadatum?.Genres.Count > 0 ? ROM.Metadatum.Genres : null;
                case BuiltInGameDataId.Tags:
                    return ROM.Tags?.Count > 0 ? ROM.Tags : null;
                case BuiltInGameDataId.Features:
                    return ROM.Metadatum?.Gamemodes.Count > 0 ? ROM.Metadatum.Gamemodes : null;
                case BuiltInGameDataId.Platforms:
                    return ROM.PlatformName;
                case BuiltInGameDataId.Categories:
                    return ROM.Metadatum?.Collections.Count > 0 ? ROM.Metadatum.Collections : null;
                case BuiltInGameDataId.Series:
                    return ROM.Metadatum?.Franchises.Count > 0 ? ROM.Metadatum.Franchises : null;
                case BuiltInGameDataId.AgeRating:
                    return ROM.Metadatum?.Age_Ratings.Count > 0 ? ROM.Metadatum.Age_Ratings : null;
                case BuiltInGameDataId.Region:
                    return ROM.Regions?.Count > 0 ? ROM.Regions : null;
                //case BuiltInGameDataId.CompletionStatus:
                //    return null;
                case BuiltInGameDataId.UserScore:
                    return ROM.RomUser?.Rating * 10;
                case BuiltInGameDataId.CommunityScore:
                    return ROM.Metadatum?.Average_Rating;
                case BuiltInGameDataId.ReleaseDate:
                    return ROM.FirstReleaseDate;
                case BuiltInGameDataId.ObtainedDate:
                    return ROM.CreatedAt;
                case BuiltInGameDataId.LastPlayedDate:
                    return ROM.RomUser?.LastPlayed;
                //case BuiltInGameDataId.Favorite:
                //    return null;
                //case BuiltInGameDataId.Links:
                //    return null;
                //case BuiltInGameDataId.TimeToBeatEstimated:
                //    return null;
                //case BuiltInGameDataId.TTBMainEstimated:
                //    return null;
                //case BuiltInGameDataId.TTBMainSidesEstimated:
                //    return null;
                //case BuiltInGameDataId.TTBCompletionEstimated:
                //    return null;
                default:
                    return null;
            }
        }
    }

    public class RomMLibraryMetadataProvider : MetadataProvider
    {
        private readonly RomMLibraryPlugin Plugin;
        public RomMLibraryMetadataProvider(RomMLibraryPlugin plugin)
        {
            Plugin = plugin;
        }

        public override async Task<MetadataProviderGameSession?> CreateGameSessionAsync(CreateGameMetadataSessionArgs args)
        {
            // This gets called for each game and returned MetadataProviderGameSession is disposed when Playnite is done with it.
            return new RomMLibraryMetadataProviderGameSession(Plugin, args.Game);
        }
    }

    // TODO: Change this to use the new playnite functionallity as there is only a single unified plugin type now

    //public class RomMMetadataProvider : LibraryMetadataProvider
    //{
    //    private readonly IRomM _romM;
    //    public RomMMetadataProvider(RomM romM)
    //    {
    //        _romM = romM;
    //    }
    //
    //    public override GameMetadata GetMetadata(Game game)
    //    {
    //        int romMId;
    //        if (!int.TryParse(game.GameId.Split(':')[0], out romMId))
    //        {
    //            _romM.Logger.Error($"[Metadata] {game.Name} GameID is malformed!");
    //            return null;
    //        }
    //
    //        RomMRom romMGame = _romM.FetchRom(romMId.ToString());
    //        if(romMGame == null)
    //        {
    //            _romM.Logger.Error($"[Metadata] {game.Name} failed to get game!");
    //            return null;
    //        }
    //
    //        var preferedRatingsBoard = _romM.Playnite.ApplicationSettings.AgeRatingOrgPriority;
    //        var agerating = romMGame.Metadatum.Age_Ratings.Count > 0 ? new HashSet<MetadataProperty>(romMGame.Metadatum.Age_Ratings.Where(r => r.Split(':')[0] == preferedRatingsBoard.ToString()).Select(r => new MetadataNameProperty(r.ToString()))) : null;
    //
    //        List<Link> gameLinks = new List<Link>();
    //        if (romMGame.SSId != null)
    //            gameLinks.Add(new Link("Screenscraper", $"https://www.screenscraper.fr/gameinfos.php?gameid={romMGame.SSId}"));
    //        if (romMGame.HasheousId != null)
    //            gameLinks.Add(new Link("Hasheous", $"https://hasheous.org/index.html?page=dataobjectdetail&type=game&id={romMGame.HasheousId}"));
    //        if (romMGame.RAId != null)
    //            gameLinks.Add(new Link("RetroAchievements", $"https://retroachievements.org/game/{romMGame.RAId}"));
    //        if (romMGame.HLTBId != null)
    //            gameLinks.Add(new Link("HowLongToBeat", $"https://howlongtobeat.com/game/{romMGame.HLTBId}"));
    //
    //        var metadata = new GameMetadata
    //        {
    //            Name = romMGame.Name,
    //            Description = romMGame.Summary,
    //
    //            Regions = new HashSet<MetadataProperty>(romMGame.Regions.Where(r => !string.IsNullOrEmpty(r)).Select(r => new MetadataNameProperty(r.ToString()))),
    //            Genres = new HashSet<MetadataProperty>(romMGame.Metadatum.Genres.Where(r => !string.IsNullOrEmpty(r)).Select(r => new MetadataNameProperty(r.ToString()))),
    //            AgeRatings = agerating,
    //            Series = new HashSet<MetadataProperty>(romMGame.Metadatum.Franchises.Where(r => !string.IsNullOrEmpty(r)).Select(r => new MetadataNameProperty(r.ToString()))),
    //            Features = new HashSet<MetadataProperty>(romMGame.Metadatum.Gamemodes.Where(r => !string.IsNullOrEmpty(r)).Select(r => new MetadataNameProperty(r.ToString()))),
    //            Categories = new HashSet<MetadataProperty>(romMGame.Metadatum.Collections.Where(r => !string.IsNullOrEmpty(r)).Select(r => new MetadataNameProperty(r.ToString()))),
    //
    //            ReleaseDate = romMGame.Metadatum.Release_Date.HasValue ? new ReleaseDate(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(romMGame.Metadatum.Release_Date.Value).ToLocalTime()) : new ReleaseDate(),
    //            CommunityScore = (int?)romMGame.Metadatum.Average_Rating,
    //
    //            CoverImage = !string.IsNullOrEmpty(romMGame.PathCoverL) ? new MetadataFile($"{_romM.Settings.RomMHost}{romMGame.PathCoverL}") : null,
    //
    //            LastActivity = romMGame.RomUser.LastPlayed,
    //            UserScore = romMGame.RomUser.Rating * 10, //RomM-Rating is 1-10, Playnite 1-100, so it can unfortunately only by synced one direction without loosing decimals
    //            Links = gameLinks,
    //            
    //        };
    //
    //        return metadata;
    //    }
    //}
}
