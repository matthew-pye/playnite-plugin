using Graviton.Models.RomM.Rom;

using Playnite;

using System.Text.Json;


namespace Graviton.Import
{
    public class GravitonMetadataProviderGameSession : MetadataProviderGameSession
    {
        private GravitonPlugin _plugin { get => GravitonPlugin.Instance; }
        private IPlayniteApi _playniteAPI { get => GravitonPlugin.PlayniteApi; }
        private ILogger _logger { get => GravitonPlugin.Logger; }

        private RomMRom? ROM = null;

        public GravitonMetadataProviderGameSession(Game game) : base(game) { }

        public async Task<bool> PullRomData()
        {
            if (Game.LibraryId == GravitonPlugin.Id)
            {
                try
                {
                    int romMId;
                    if (!int.TryParse(Game.LibraryGameId?.Split(':')[0], out romMId))
                        throw new Exception($"[Metadata] {Game.Name} GameID is malformed!");

                    var result = await HttpClientSingleton.RomMGetAsync($"/api/roms/{romMId}");
                    if (result == null)
                        return false;

                    ROM = JsonSerializer.Deserialize<RomMRom>(result) ?? throw new Exception("Unable to deserialize ROM!");
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
                //    return null;

                case BuiltInGameDataId.DesktopCover:
                    return ROM.HasCover ? ROM.PathCoverL : null;

                case BuiltInGameDataId.Genres:
                    return ROM.Metadatum?.Genres?.Count > 0 ? ROM.Metadatum.Genres : null;

                case BuiltInGameDataId.Tags:
                    return ROM.Tags?.Count > 0 ? ROM.Tags : null;

                case BuiltInGameDataId.Features:
                    return ROM.Metadatum?.Gamemodes?.Count > 0 ? ROM.Metadatum.Gamemodes : null;

                case BuiltInGameDataId.Platforms:
                    return ROM.PlatformName;

                case BuiltInGameDataId.Categories:
                    return ROM.Metadatum?.Collections?.Count > 0 ? ROM.Metadatum.Collections : null;

                case BuiltInGameDataId.Series:
                    return ROM.Metadatum?.Franchises?.Count > 0 ? ROM.Metadatum.Franchises : null;

                case BuiltInGameDataId.AgeRating:
                    if(ROM.IgdbMetadata?.AgeRatings != null)
                    {
                        List<string> ageratings = new();
                        foreach (var rating in ROM.IgdbMetadata.AgeRatings)
                        {
                            ageratings.Add($"{rating.RatingBoard} {rating.Rating}");
                        }
                        return ageratings;
                    }
                    return null;

                case BuiltInGameDataId.Region:
                    return ROM.Regions?.Count > 0 ? ROM.Regions : null;

                case BuiltInGameDataId.CompletionStatus:
                    if (ROM.RomUser?.Status != null)
                    {
                        return RomMRomUser.CompletionStatusMap[ROM.RomUser.Status];
                    }
                    return null;

                case BuiltInGameDataId.UserScore:
                    return ROM.RomUser?.Rating * 10;

                case BuiltInGameDataId.CommunityScore:
                    return ROM.Metadatum?.Average_Rating;

                case BuiltInGameDataId.ReleaseDate:
                    return ROM.Metadatum?.ReleaseDate;

                case BuiltInGameDataId.ObtainedDate:
                    return ROM.CreatedAt;

                case BuiltInGameDataId.LastPlayedDate:
                    return ROM.RomUser?.LastPlayed;

                case BuiltInGameDataId.Favorite:
                    return ROM.Collections?.Any(x => x.Name == "Favorites");

                case BuiltInGameDataId.Links:
                    List<WebLink> links = new();
                    if (ROM.SSId != null)
                    {
                        links.Add(new WebLink("Screenscraper", $"https://www.screenscraper.fr/gameinfos.php?gameid={ROM.SSId}"));
                    }
                    if (ROM.HasheousId != null)
                    {
                        links.Add(new WebLink("Hasheous", $"https://hasheous.org/index.html?page=dataobjectdetail&type=game&id={ROM.HasheousId}"));
                    }
                    if (ROM.RAId != null)
                    {
                        links.Add(new WebLink("RetroAchievements", $"https://retroachievements.org/game/{ROM.RAId}"));
                    }
                    if (ROM.HLTBId != null)
                    {

                        links.Add(new WebLink("HowLongToBeat", $"https://howlongtobeat.com/game/{ROM.HLTBId}"));
                    }

                    if (links.Count > 0)
                        return links;

                    return null;

                case BuiltInGameDataId.TTBMainEstimated:
                    return ROM.HLTBMetadata?.MainStory;
                case BuiltInGameDataId.TTBMainSidesEstimated:
                    return ROM.HLTBMetadata?.MainStoryExtra;
                case BuiltInGameDataId.TTBCompletionEstimated:
                    return ROM.HLTBMetadata?.Completionist;
                default:
                    return null;
            }
        }
    }

    public class GravitonMetadataProvider : MetadataProvider
    {
        public override async Task<MetadataProviderGameSession?> CreateGameSessionAsync(CreateGameMetadataSessionArgs args)
        {
            GravitonMetadataProviderGameSession metadata = new(args.Game);
            var success = await metadata.PullRomData();
            if (!success)
                return null;

            return metadata;
        }
    }
}