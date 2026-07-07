using Graviton.Models.Notifications;
using Graviton.Models.RomM.Collection;
using Graviton.Models.RomM.PlaySessions;
using Graviton.Models.RomM.Rom;

using Playnite;

using System.Net.Http;
using System.Text.Json;

namespace Graviton.Status
{
    public class StatusController
    {
        private GravitonPlugin _plugin;
        private IPlayniteApi _playniteAPI;
        private ILogger _logger;

        private CancellationTokenSource? _heartbeatCts;

        public StatusController(GravitonPlugin plugin, IPlayniteApi playniteAPI, ILogger logger)
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;
        }

        // Syncing

        public async Task PushPlaySession(string GameID, DateTime StopTime, uint SessionLength)
        {
            int romMID;
            if (!int.TryParse(GameID.Split(':')[0], out romMID))
            {
                GravitonNotify.Add(new GravitonNotification("graviton.update.status.failed", Loc.GetString("LibraryIdConvertFailed", ("GameID", GameID)), GravitonSeverity.Error));
                return;
            }

            var session = new List<RomMPlaySession>
            {
                new RomMPlaySession
                {
                   ROMId = romMID,
                   StopTime = StopTime.ToString("O"),
                   StartTime = StopTime.AddMilliseconds(-SessionLength).ToString("O"),
                   Duration = (int)SessionLength
                }
            };
            var playsessions = new { device_id = _plugin.Settings.AccountState.DeviceID, sessions = session };    
            var response = await HttpClientSingleton.RomMPostJsonAsync("/api/play-sessions", playsessions);

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

        public async Task UpdateFavorites(RomMCollection favouriteCollection)
        {
            if (favouriteCollection == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.favourites.update.failed", Loc.GetString("FavouritesUpdateFailed"), GravitonSeverity.Error));
                return;
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
                GravitonNotify.Add(new GravitonNotification("graviton.update.status.failed", Loc.GetString("LibraryIdConvertFailed", ("GameID", game.LibraryGameId!.ToString())), GravitonSeverity.Error));
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
              backlogged = status == "backlogged",
              now_playing = status == "now_playing",
              status = (status != "backlogged" && status != "now_playing" && status != "not_played") ? status : null            
            };

            await HttpClientSingleton.RomMPutJsonAsync($"/api/roms/{romMID}/props", props);
        }

        //public async Task RefreshRA()
        //{
        //    var refresh = new { incremental = true };
        //
        //    var result = await HttpClientSingleton.RomMPostJsonAsync($"/api/users/{_plugin.Settings.AccountState.UserID}/ra/refresh", refresh);
        //    if(result != null)
        //    {
        //
        //    }
        //
        //}

        public async Task StartActivityHeartbeat(string GameID)
        {
            _heartbeatCts?.Cancel();
            _heartbeatCts = new CancellationTokenSource();
            var token = _heartbeatCts.Token;

            int romMID;
            if (!int.TryParse(GameID.Split(':')[0], out romMID))
            {
                GravitonNotify.Add(new GravitonNotification("graviton.start.game.failed", Loc.GetString("LibraryIdConvertFailed", ("GameID", GameID)), GravitonSeverity.Error));
                return;
            }

            try
            {
                while (!token.IsCancellationRequested)
                {
                    var heartbeat = new { rom_id = romMID, device_id = _plugin.Settings.AccountState.DeviceID };
                    await HttpClientSingleton.RomMPostJsonAsync("/api/activity/heartbeat", heartbeat);
                    await Task.Delay(5000, token);
                }
            }
            catch (Exception ex) 
            { 
                if(!token.IsCancellationRequested)
                    GravitonNotify.Add(new GravitonNotification("graviton.game.heartbeat.failed", $"{Loc.GetString("GameHeartbeatFailed")} - {ex.Message}", GravitonSeverity.Error, ex)); 
            }
            finally
            {
                await HttpClientSingleton.RomMDeleteAsync($"/api/activity/heartbeat?device_id={_plugin.Settings.AccountState.DeviceID}");
            }

        }

        public void StopActivityHeartbeat() => _heartbeatCts?.Cancel();

    }
}
