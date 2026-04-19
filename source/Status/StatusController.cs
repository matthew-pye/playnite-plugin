
using Playnite;

using RomMLibrary.Models.RomM.Collection;
using RomMLibrary.Models.RomM.Rom;

using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace RomMLibrary.Status
{
    public class StatusController
    {
        RomMLibraryPlugin Plugin;
        private static readonly ILogger Logger = LogManager.GetLogger();
        private readonly IPlayniteApi PlayniteApi;

        public StatusController(RomMLibraryPlugin plugin) 
        {
            Plugin = plugin;
            PlayniteApi = RomMLibraryPlugin.PlayniteApi ?? throw new Exception("Playnite API is null, cannot continue!");
        }

        // Favourites
        private RomMCollection? CreateFavorites()
        {
            string apiCollectionUrl = $"{Plugin.Settings.Host}/api/collections?is_favorite=true&is_public=false";
            try
            {
                var formData = new MultipartFormDataContent();
                formData.Add(new StringContent("Favorites"), "name");

                HttpResponseMessage postResponse = HttpClientSingleton.Instance.PostAsync(apiCollectionUrl, formData).GetAwaiter().GetResult();
                postResponse.EnsureSuccessStatusCode();

                string body = postResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return JsonSerializer.Deserialize<RomMCollection>(body);
            }
            catch (HttpRequestException e)
            {
                Logger.Error($"Request exception: {e.Message}");
                return null;
            }
        }

        public RomMCollection? PullFavourites()
        {
            string apiFavoriteUrl = $"{Plugin.Settings.Host}/api/collections";
            try
            {
                // Make the request and get the response
                HttpResponseMessage response = HttpClientSingleton.Instance.GetAsync(apiFavoriteUrl).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();

                // Assuming the response is in JSON format
                string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                RomMCollection? favourites = JsonSerializer.Deserialize<List<RomMCollection>>(body)?.First(x => x.Name == "Favorites");

                if (favourites == null)
                    return CreateFavorites();

                return favourites;
            }
            catch (HttpRequestException e)
            {
                Logger.Error($"Request exception: {e.Message}");
                return null;
            }
        }

        public void UpdateFavorites(RomMCollection favoriteCollection, List<int> romMRomIDs)
        {
            if (favoriteCollection == null)
            {
                Logger.Error($"Can't update favorites, collection is null");
                return;
            }

            string apiCollectionUrl = $"{Plugin.Settings.Host}/api/collections";
            try
            {
                var formData = new MultipartFormDataContent();
                formData.Add(new StringContent(JsonSerializer.Serialize(romMRomIDs)), "rom_ids");
                HttpResponseMessage putResponse = HttpClientSingleton.Instance.PutAsync($"{apiCollectionUrl}/{favoriteCollection.Id}", formData).GetAwaiter().GetResult();
                putResponse.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException e)
            {
                Logger.Error($"Request exception: {e.Message}");
            }
        }

        // Play Status
        public CompletionStatus? DetermineCompletionStatus(RomMRom ROM)
        {
            if(ROM.RomUser?.Status != null)
            {
                var status = RomMRomUser.CompletionStatusMap[ROM.RomUser.Status];
                return PlayniteApi.Library.CompletionStatuses.First(x => x.Name == status);
            }

            return null;
        }
        public CompletionStatus? DetermineCompletionStatus(Game game)
        {
            if (game.CompletionStatusId != null)
            {
                return PlayniteApi.Library.CompletionStatuses.First(x => x.Id == game.CompletionStatusId);
            }

            return null;
        }

        public void UpdateStatus(Game game)
        {
            try
            {
                if (game.CompletionStatusId == null) return;

                int romMID;
                if (int.TryParse(game.LibraryGameId?.Split(':')[0], out romMID))
                {
                    Logger.Error("Failed to parse GameID, Skipping status update!");
                    return;
                }

                var status = PlayniteApi.Library.CompletionStatuses.Get(game.CompletionStatusId)?.Name;
                var updatePayload = new
                {
                    data = new
                    {
                        backlogged = status == "Plan to Play",
                        now_playing = status == "Playing",
                        status = RomMRomUser.CompletionStatusMap.FirstOrDefault((kv) => kv.Value == status && kv.Value != "Playing" && kv.Value != "Plan to Play" && kv.Value != "Not Played").Key
                    }
                };
                string apiRomMRomUserProps = $"{Plugin.Settings.Host}api/roms/{romMID}/props";
                HttpResponseMessage response = HttpClientSingleton.Instance.PutAsync(apiRomMRomUserProps, new StringContent(JsonSerializer.Serialize(updatePayload), Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"RomM Status Sync Failed for {game.Name}");
            }
        }


    }
}
