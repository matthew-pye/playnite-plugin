using Playnite;

using RomM.Properties;

using Graviton;
using Graviton.Models.RomM.Rom;
using Graviton.Settings;

using System.Net.Http;
using System.Text.Json;

using static Playnite.MetadataProvider;

namespace RomM.Import
{
    public class GravitonMetadataProviderGameSession : MetadataProviderGameSession
    {
        private GravitonPlugin Plugin { get => GravitonPlugin.Instance; }
        private readonly ILogger Logger = LogManager.GetLogger();
        private RomMRom? ROM = null;

        public GravitonMetadataProviderGameSession(Game game) : base(game)
        {
            if (game.LibraryId == "Graviton")
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
            string romUrl = $"{Plugin.Settings.Host}/api/roms/{romId}";
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
                    return ROM.Metadatum?.Age_Ratings?.Count > 0 ? ROM.Metadatum.Age_Ratings : null;
                case BuiltInGameDataId.Region:
                    return ROM.Regions?.Count > 0 ? ROM.Regions : null;
                //case BuiltInGameDataId.CompletionStatus:
                //    return null;
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
                //case BuiltInGameDataId.Favorite:
                //    return null;
                //case BuiltInGameDataId.Links:
                //    return null;
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
        private GravitonPlugin Plugin { get => GravitonPlugin.Instance; }

        public override async Task<MetadataProviderGameSession?> CreateGameSessionAsync(CreateGameMetadataSessionArgs args)
        {
            // This gets called for each game and returned MetadataProviderGameSession is disposed when Playnite is done with it.
            return new GravitonMetadataProviderGameSession(args.Game);
        }
    }
}