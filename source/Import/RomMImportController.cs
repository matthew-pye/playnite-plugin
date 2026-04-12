using Playnite;

using RomMLibrary.Models;
using RomMLibrary.Models.RomM.Platform;
using RomMLibrary.Models.RomM.Rom;

using System.Collections.Specialized;
using System.IO;
using System.Net.Http;
using System.Runtime.Intrinsics.Arm;
using System.Text.Json;
using System.Web;

using static Playnite.Plugin;

namespace RomMLibrary.Import
{
    class RomMImportController
    {
        private readonly RomMLibraryPlugin Plugin;
        public IPlayniteApi PlayniteApi;
        public ILogger Logger => LogManager.GetLogger();

        public RomMImportController(RomMLibraryPlugin plugin)
        {
            Plugin = plugin;
            PlayniteApi = RomMLibraryPlugin.PlayniteApi ?? throw new Exception("Playnite API is null cannot continue!");
        }

        public async Task<List<Game>> Import(ImportGamesArgs args)
        {
            IList<RomMPlatform> apiPlatforms = FetchPlatforms();
            List<Task<List<Game>>> tasks = new List<Task<List<Game>>>();
            List<Game> games = new List<Game>();
            IEnumerable<EmulatorMapping> enabledMappings = Plugin.Settings.Mappings.Where(m => m.Enabled);
            string url = BuildROMUrl();

            if (enabledMappings == null || !enabledMappings.Any())
            {
                PlayniteApi.Notifications.Add(new NotificationMessage(RomMLibraryPlugin.Id, Loc.GetString("NoEmulatorsConfigured"), NotificationSeverity.Error));
                return games;
            }

            //IList<RomMCollection> favoritCollections = Plugin.FetchFavorites();
            //var favorites = favoritCollections.FirstOrDefault(c => c.IsFavorite)?.RomIds ?? new List<int>();

            // Pull ROM data for each enabled mapping and add the games to playnite
            foreach (var mapping in enabledMappings)
            {
                if (args.CancelToken.IsCancellationRequested)
                    break;

                // Check mapping has an Emulator, Profile & Platform assigned to it
                //if (mapping.Emulator == null || mapping.EmulatorProfile == null || mapping.RomMPlatform == null || mapping.RomMPlatformId == -1)
                //{
                //    Logger.Warn($"[Import Controller] Emulator {mapping.MappingId} is misconfigured, skipping.");
                //    continue;
                //}

                RomMPlatform? apiPlatform = apiPlatforms.FirstOrDefault(p => p.Id == mapping.RomMPlatformId);
                if (apiPlatform == null)
                {
                    PlayniteApi.Notifications.Add(new NotificationMessage(RomMLibraryPlugin.Id, Loc.GetString("PlatformNotFound", ("PlatformName", mapping.RomMPlatform.Name), ("PlatformID", mapping.RomMPlatformId)), NotificationSeverity.Error));
                    continue;
                }

                // Pull data from server
                // Could be made async, but when testing (4.7.0) found a performance degradation
                var romMROMs = DownloadROMData(args, url, apiPlatform);
                if(romMROMs == null)
                {
                    Logger.Warn($"[Import Controller] Failed to get ROMs for {apiPlatform.Name}.");
                    continue;
                }
                Logger.Info($"[Import Controller] Finished parsing response for {apiPlatform.Name}.");

                // Import games for current mapping 
                tasks.Add(Task<List<Game>>.Factory.StartNew(() =>
                {
                    RomMImport newImport = new RomMImport(Plugin, args.CancelToken, mapping, romMROMs);
                    return newImport.ProcessData();
                }));

            }

            await Task.WhenAll(tasks);
            
            foreach (var task in tasks)
            {
                games.AddRange(task.Result);
            }

            return games;
        }

        private string BuildROMUrl()
        {
            string url = $"{Plugin.Settings.Host.Trim('/')}/api/roms";

            if (Plugin.Settings.SkipMissingFiles)
            {
                url += "?missing=false&";
            }

            // Exclude genres from import
            string excludeGenresString = Plugin.Settings.ExcludeGenres.Trim(' ');
            excludeGenresString = excludeGenresString.Trim(';');
            List<string> excludeGenres = excludeGenresString.Split(';').ToList();
            if (!string.IsNullOrEmpty(excludeGenresString))
            {
                // Add ? if it hasn't been added already
                if (!Plugin.Settings.SkipMissingFiles)
                {
                    url += "?";
                }

                if (excludeGenres.Count > 1)
                {
                    foreach (var genre in excludeGenres)
                    {
                        url += $"genres={HttpUtility.UrlEncode(genre)}&";
                    }
                }
                else
                {
                    url += $"genres={HttpUtility.UrlEncode(excludeGenresString)}";
                }
            }
            url.Trim('&');

            return url;
        }
        private IList<RomMPlatform> FetchPlatforms()
        {
            string apiPlatformsUrl = $"{Plugin.Settings.Host.Trim('/')}/api/platforms";
            try
            {
                HttpResponseMessage response = HttpClientSingleton.Instance.GetAsync(apiPlatformsUrl).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();

                string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return JsonSerializer.Deserialize<List<RomMPlatform>>(body) ?? throw new Exception("Failed to deseralize plaforms from server!");
            }
            catch (HttpRequestException e)
            {
                Logger.Error($"[Import Controller] Request exception: {e.Message}");
                return new List<RomMPlatform>();
            }
        }
        
        private List<RomMRom> DownloadROMData(ImportGamesArgs args, string url, RomMPlatform platform)
        {
            Logger.Info($"[Import Controller] Starting to fetch games for {platform.Name}.");

            const int pageSize = 50;
            int offset = 0;
            bool hasMoreData = true;
            var romData = new List<RomMRom>();

            // Download data from RomM server
            while (hasMoreData)
            {
                if (args.CancelToken.IsCancellationRequested)
                    break;

                NameValueCollection queryParams = new NameValueCollection
                    {
                        { "platform_ids", platform.Id.ToString() },
                        { "genres_logic", "none" },
                        { "order_by", "name" },
                        { "order_dir", "asc" },
                        { "limit", pageSize.ToString() },
                        { "offset", offset.ToString() },
                    };

                var uriBuilder = new UriBuilder(url);
                var query = HttpUtility.ParseQueryString(uriBuilder.Query);

                foreach (string key in queryParams)
                {
                    query[key] = queryParams[key];
                }

                uriBuilder.Query = query.ToString();

                try
                {
                    HttpResponseMessage response = HttpClientSingleton.Instance.GetAsync(uriBuilder.Uri).GetAwaiter().GetResult();
                    response.EnsureSuccessStatusCode();

                    Logger.Info($"[Import Controller] Parsing response for {platform.Name} batch {offset / pageSize + 1}.");

                    Stream body = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                    List<RomMRom> roms;
                    using (StreamReader reader = new StreamReader(body))
                    {
                        var jsonResponse = JsonDocument.Parse(reader.ReadToEnd());
                        roms = jsonResponse.RootElement.GetProperty("items").Deserialize<List<RomMRom>>() ?? throw new Exception("Unable to deseralize ROMs!");
                    }

                    Logger.Info($"[Import Controller] Parsed {roms.Count} roms for batch {offset / pageSize + 1}.");
                    romData.AddRange(roms);

                    if (roms.Count < pageSize)
                    {
                        Logger.Info($"[Import Controller] Received less than {pageSize} roms for {platform.Name}, assuming no more games.");
                        hasMoreData = false;
                        break;
                    }

                    offset += pageSize;
                }
                catch (HttpRequestException e)
                {
                    Logger.Error($"[Import Controller] Request exception: {e.Message}");
                    hasMoreData = false;
                }
            }

            return romData;
        }
    }

}
