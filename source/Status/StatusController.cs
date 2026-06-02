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

        public async Task UpdateFavorites(RomMCollection favoriteCollection, List<int> romMRomIDs)
        {
            if (favoriteCollection == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.favourites.update.failed", Loc.GetString("FavouritesUpdateFailed"), GravitonSeverity.Error));
                _logger.Error($"Can't update favorites, collection is null");
                return;
            }

            var formData = new MultipartFormDataContent();
            formData.Add(new StringContent(JsonSerializer.Serialize(romMRomIDs)), "rom_ids");
            var result = await HttpClientSingleton.RomMPutContentAsync("/api/collections", formData);

        }

        // Play Status
        public CompletionStatus? DetermineCompletionStatus(RomMRom ROM)
        {
            if(ROM.RomUser?.Status != null)
            {
                var status = RomMRomUser.CompletionStatusMap[ROM.RomUser.Status];
                return _playniteAPI.Library.CompletionStatuses.First(x => x.Name == status);
            }

            return null;
        }
        public CompletionStatus? DetermineCompletionStatus(Game game)
        {
            if (game.CompletionStatusId != null)
            {
                return _playniteAPI.Library.CompletionStatuses.First(x => x.Id == game.CompletionStatusId);
            }

            return null;
        }

        public async Task UpdateStatus(Game game)
        {
            try
            {
                if (game.CompletionStatusId == null) return;

                int romMID;
                if (int.TryParse(game.LibraryGameId?.Split(':')[0], out romMID))
                {
                    _logger.Error("Failed to parse GameID, Skipping status update!");
                    return;
                }

                var status = _playniteAPI.Library.CompletionStatuses.Get(game.CompletionStatusId)?.Name;
                var updatePayload = new
                {
                    data = new
                    {
                        backlogged = status == "Plan to Play",
                        now_playing = status == "Playing",
                        status = RomMRomUser.CompletionStatusMap.FirstOrDefault((kv) => kv.Value == status && kv.Value != "Playing" && kv.Value != "Plan to Play" && kv.Value != "Not Played").Key
                    }
                };
                string apiRomMRomUserProps = $"{_plugin.Settings.Host}";

                var result = await HttpClientSingleton.RomMPutContentAsync($"/api/roms/{romMID}/props", new StringContent(JsonSerializer.Serialize(updatePayload), Encoding.UTF8, "application/json"));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"RomM Status Sync Failed for {game.Name}");
            }
        }
    }
}
