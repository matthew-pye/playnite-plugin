using Graviton.Models.Notifications;
using Graviton.Models.RomM.Collection;
using Graviton.Models.RomM.PlaySessions;
using Graviton.Models.RomM.Rom;

using Playnite;

using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Graviton.Status
{
    public class StatusController
    {
        private GravitonPlugin _plugin { get => GravitonPlugin.Instance; }
        private IPlayniteApi _playniteAPI { get => GravitonPlugin.PlayniteApi; }
        private ILogger _logger { get => GravitonPlugin.Logger; }

        // Syncing

        public async Task PushPlaySession(List<RomMPlaySession> playSessions)
        {
            object sessions = new { device_id = "", sessions = playSessions };

            var response = await HttpClientSingleton.RomMPutJsonAsync("/api/play-sessions", sessions);


        }

        // Favourites
        private async Task<RomMCollection?> CreateFavorites()
        {
            var formData = new MultipartFormDataContent();
            formData.Add(new StringContent("Favorites"), "name");

            var result = await HttpClientSingleton.RomMPostContentAsync("/api/collections?is_favorite=true&is_public=false", formData);
            if (result == null)
                return null;

            try
            {
                return JsonSerializer.Deserialize<RomMCollection>(result);
            }
            catch (HttpRequestException ex)
            {
                _logger.Error($"{Loc.GetString("CreateFavoritesFailed")} - {ex}");
                return null;
            }
        }

        public async Task<RomMCollection?> PullFavourites()
        {
            var result = await HttpClientSingleton.RomMGetAsync("/api/collections");
            if (result == null)
                return null;

            try
            {
                RomMCollection? favourites = result.Deserialize<List<RomMCollection>>()?.First(x => x.Name == "Favorites");

                if (favourites == null)
                    return await CreateFavorites();

                return favourites;
            }
            catch (HttpRequestException e)
            {
                _logger.Error($"Request exception: {e.Message}");
                return null;
            }
        }

        public async Task UpdateFavorites(Game game)
        {
            var favouriteCollection = await PullFavourites();

            if (favouriteCollection == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.favourites.update.failed", Loc.GetString("FavouritesUpdateFailed"), GravitonSeverity.Error));
                return;
            }

            int romMID;
            if (!int.TryParse(game.LibraryGameId?.Split(':')[0], out romMID))
            {
                GravitonNotify.Add(new GravitonNotification("graviton.update.status.failed", Loc.GetString("LibraryIdConvertFailed"), GravitonSeverity.Error));
                return;
            }

            if(game.Favorite)
            {
                favouriteCollection.RomIDs?.Add(romMID);
            }
            else
            {
                favouriteCollection.RomIDs?.Remove(romMID);
            }

            var formData = new MultipartFormDataContent();
            formData.Add(new StringContent(JsonSerializer.Serialize(favouriteCollection.RomIDs)), "rom_ids");
            var result = await HttpClientSingleton.RomMPutContentAsync($"/api/collections/{favouriteCollection.Id}", formData);

        }

        // Play Status
        public async Task UpdateStatus(Game game)
        {

            int romMID;
            if (!int.TryParse(game.LibraryGameId?.Split(':')[0], out romMID))
            {
                GravitonNotify.Add(new GravitonNotification("graviton.update.status.failed", Loc.GetString("LibraryIdConvertFailed"), GravitonSeverity.Error));
                return;
            }

            var playniteStatus = _playniteAPI.Library.CompletionStatuses.FirstOrDefault(x => x.Id == game.CompletionStatusId)?.Name;
            if(playniteStatus == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.update.status.failed", Loc.GetString("CompletionStatusNameFailed"), GravitonSeverity.Error));
                return;
            }

            var status = RomMRomUser.CompletionStatusMap.FirstOrDefault(x => x.Value == playniteStatus).Key;
            if (status == null)
            { 
                GravitonNotify.Add(new GravitonNotification("graviton.update.status.failed", Loc.GetString("ConvertStatusFailed", [("$PlayniteStatus", $"{playniteStatus}")]), GravitonSeverity.Error));
                return;
            }
                
            var props = new
            {
                data = new
                {
                    backlogged = status == "Plan to Play",
                    now_playing = status == "Playing",
                    status = RomMRomUser.CompletionStatusMap.FirstOrDefault((kv) => kv.Value == status && kv.Value != "Playing" && kv.Value != "Plan to Play" && kv.Value != "Not Played").Key
                }
            };

            await HttpClientSingleton.RomMPutContentAsync($"/api/roms/{romMID}/props", new StringContent(JsonSerializer.Serialize(props), Encoding.UTF8, "application/json"));
        }
    }
}
