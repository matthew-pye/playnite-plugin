using Graviton.Models.Notifications;
using Graviton.Models.RomM.Collection;
using Graviton.Models.RomM.PlaySessions;
using Graviton.Models.RomM.Rom;

using Playnite;

using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Graviton.Status
{
    public class StatusController
    {
        private GravitonPlugin _plugin { get => GravitonPlugin.Instance; }
        private IPlayniteApi _playniteAPI { get => GravitonPlugin.PlayniteApi; }
        private ILogger _logger { get => GravitonPlugin.Logger; }

        private bool _gameRunning = false;
        private CancellationTokenSource? _heartbeatCts;

        // Syncing

        public async Task PushPlaySession(List<RomMPlaySession> playSessions)
        {
            object sessions = new { device_id = _plugin.Settings.DeviceID, sessions = playSessions };

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
            catch (Exception ex)
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
                RomMCollection? favourites = result.Deserialize<List<RomMCollection>>()?.FirstOrDefault(x => x.Name == "Favorites");

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

            if (favouriteCollection.RomIDs == null)
                favouriteCollection.RomIDs = new();

            if (game.Favorite && !favouriteCollection.RomIDs.Contains(romMID))
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
                    backlogged = status == "backlogged",
                    now_playing = status == "now_playing",
                    status = (status != "backlogged" && status != "now_playing" && status != "not_played") ? status : null
                }
            };

            await HttpClientSingleton.RomMPutContentAsync($"/api/roms/{romMID}/props", new StringContent(JsonSerializer.Serialize(props), Encoding.UTF8, "application/json"));
        }

        public async void StartActivityHeartbeat(string GameID)
        {
            _heartbeatCts?.Cancel();
            _heartbeatCts = new CancellationTokenSource();
            var token = _heartbeatCts.Token;

            int romMID;
            if (!int.TryParse(GameID?.Split(':')[0], out romMID))
            {
                GravitonNotify.Add(new GravitonNotification("graviton.update.status.failed", Loc.GetString("LibraryIdConvertFailed"), GravitonSeverity.Error));
                return;
            }

            _gameRunning = true;

            while (_gameRunning && !_heartbeatCts.IsCancellationRequested)
            {
                var heartbeat = new { rom_id=romMID, device_id=_plugin.Settings.DeviceID };

                await HttpClientSingleton.RomMPostJsonAsync("/api/activity/heartbeat", heartbeat);
                await Task.Delay(5000, token); // 5 secs
            }

            await HttpClientSingleton.Instance.DeleteAsync($"{_plugin.Settings.Host}/api/activity/heartbeat?device_id={_plugin.Settings.DeviceID}");

        }

        public void StopActivityHeartbeat()
        {
            _heartbeatCts?.Cancel();
            _gameRunning = false;
        }

    }
}
