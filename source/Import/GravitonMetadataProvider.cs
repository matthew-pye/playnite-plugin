using Graviton.Models.RomM;
using Graviton.Models.RomM.Rom;

using Playnite;

using System.Text.Json;


namespace Graviton.Import
{
    public class GravitonMetadataProviderGameSession : MetadataProviderGameSession
    {
        private GravitonPlugin _plugin;
        private IPlayniteApi _playniteAPI;
        private ILogger _logger;
        private IRomMServer _romMServer;

        private RomMRom? ROM = null;
        private static (DateTime, RomMUser?) UserData;

        public GravitonMetadataProviderGameSession(GravitonPlugin plugin, IPlayniteApi playniteAPI, ILogger logger, IRomMServer romMServer, Game game) : base(game) 
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;
            _romMServer = romMServer;
        }

        public async Task<bool> PullRomData()
        {
            if (Game.LibraryId == GravitonPlugin.Id)
            {
                try
                {
                    int romMId;
                    if (!int.TryParse(Game.LibraryGameId?.Split(':')[0], out romMId))
                        throw new Exception($"[Metadata] {Game.Name} GameID is malformed!");

                    var result = await _romMServer.GETAsync($"/api/roms/{romMId}");
                    if (result == null)
                        return false;

                    ROM = JsonSerializer.Deserialize<RomMRom>(result) ?? throw new Exception("Unable to deserialize ROM!");

                    if(DateTime.UtcNow - UserData.Item1 > TimeSpan.FromSeconds(30))
                    {
                        result = await _romMServer.GETAsync($"/api/users/me");
                        if (result == null)
                            return false;

                        UserData.Item2 = JsonSerializer.Deserialize<RomMUser>(result);
                        UserData.Item1 = DateTime.UtcNow;
                    }
                    

                    return true;
                }
                catch (Exception Ex)
                {
                    _logger.Error($"[Metadata] {Game.Name} failed to get metadata\n{Ex}!");
                    return false;
                }
            }
            return false;
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
                //case BuiltInGameDataId.Note:
                  //return null;
                case BuiltInGameDataId.DesktopCover:
                    return ROM?.PathCoverL != null ? new ImportableFile(BuiltInGameDataId.DesktopCover, $"{_plugin.Settings.Host}{ROM.PathCoverL}") : null;

                case BuiltInGameDataId.Genres:
                    return ROM.Metadatum?.Genres;
                case BuiltInGameDataId.Tags:
                    return ROM.Tags;
                case BuiltInGameDataId.Features:
                    return ROM.Metadatum?.Gamemodes;
                case BuiltInGameDataId.Platforms:
                    return ROM.PlatformName;
                case BuiltInGameDataId.Categories:
                    return ROM.Metadatum?.Collections;
                case BuiltInGameDataId.Series:
                    return ROM.Metadatum?.Franchises;
                case BuiltInGameDataId.AgeRating:
                    return ROM.IgdbMetadata?.AgeRatings?.Select(x => $"{x.RatingBoard} {x.Rating}");
                case BuiltInGameDataId.Region:
                    return ROM.Regions;
                case BuiltInGameDataId.CommunityScore:
                    return ROM.Metadatum?.AverageRating;
                case BuiltInGameDataId.ReleaseDate:
                    if(ROM.Metadatum?.ReleaseDate != null)
                        return new DateTime(ROM.Metadatum.ReleaseDate ?? 0);
                    else 
                        return null;
                case BuiltInGameDataId.EstimatedInstallSize:
					return ROM.FileSizeBytes;
                     
                case BuiltInGameDataId.CompletionStatus:
                    if (ROM.RomUser?.Status != null)
                        return RomMRomUser.CompletionStatusMap[ROM.RomUser.Status];
                    else
                        return null;
                case BuiltInGameDataId.UserScore:
                    return ROM.RomUser?.Rating * 10;
                case BuiltInGameDataId.ObtainedDate:
                    return ROM.CreatedAt;
                case BuiltInGameDataId.LastPlayedDate:
                    return ROM.RomUser?.LastPlayed;
                case BuiltInGameDataId.Favorite:
                    return ROM.Collections?.Any(x => x.Name == "Favorites");
                case BuiltInGameDataId.Hidden:
                    return ROM.RomUser?.Hidden;

                case BuiltInGameDataId.Links:
                    List<WebLink> links = new();
                    if (ROM.SSId != null)
                    {
                        links.Add(new WebLink("screenscraper", $"https://www.screenscraper.fr/gameinfos.php?gameid={ROM.SSId}"));
                    }
                    if (ROM.HasheousId != null)
                    {
                        links.Add(new WebLink("hasheous", $"https://hasheous.org/index.html?page=dataobjectdetail&type=game&id={ROM.HasheousId}"));
                    }
                    if (ROM.RAId != null)
                    {
                        links.Add(new WebLink("retroachievements", $"https://retroachievements.org/game/{ROM.RAId}"));
                    }
                    if (ROM.HLTBId != null)
                    {

                        links.Add(new WebLink("howlongtobeat", $"https://howlongtobeat.com/game/{ROM.HLTBId}"));
                    }

                    if (links.Count > 0)
                        return links;

                    return null;
                case BuiltInGameDataId.ExternalIds:
                    List<ExternalIdentifier> Ids = new();
                    if (ROM.SSId != null)
                    {
                        Ids.Add(new("screenscraper", ROM.SSId.ToString()!));
                    }
                    if (ROM.HasheousId != null)
                    {
                        Ids.Add(new("hasheous", ROM.HasheousId.ToString()!));
                    }
                    if (ROM.RAId != null)
                    {
                        Ids.Add(new("retroachievements", ROM.RAId.ToString()!));
                    }
                    if (ROM.HLTBId != null)
                    {

                        Ids.Add(new("howlongtobeat", ROM.HLTBId.ToString()!));
                    }

                    if (Ids.Count > 0)
                        return Ids;

                    return null;

                case BuiltInGameDataId.TimeToBeatEstimated:
                    return ROM.HLTBMetadata?.MainStory != null ? new TimeToBeat(ROM.HLTBMetadata.MainStory, ROM.HLTBMetadata.MainStoryExtra, ROM.HLTBMetadata.Completionist) : null;

                default:
                    return null;
            }
        }
    }

    public class GravitonMetadataProvider : MetadataProvider
    {
        private GravitonPlugin _plugin;
        private IPlayniteApi _playniteAPI;
        private ILogger _logger;
        private IRomMServer _romMServer;

        public GravitonMetadataProvider(GravitonPlugin plugin, IPlayniteApi playniteAPI, ILogger logger, IRomMServer romMServer)
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;
            _romMServer = romMServer;
        }

        public override async Task<MetadataProviderGameSession?> CreateGameSessionAsync(CreateGameMetadataSessionArgs args)
        {
            GravitonMetadataProviderGameSession metadata = new(_plugin, _playniteAPI, _logger, _romMServer, args.Game);
            var success = await metadata.PullRomData();
            if (!success)
                return null;

            return metadata;
        }
    }
}