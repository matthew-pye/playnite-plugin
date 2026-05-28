using Playnite;
using static Playnite.Plugin;

using Graviton.Models;
using Graviton.Models.RomM.Platform;
using Graviton.Models.RomM.Rom;

using System.Net.Http;
using System.Text.Json;
using System.Web;

namespace Graviton.Import
{
    public class RomMImportController
    {
        //public List<NotificationMessage> Notifications = new List<NotificationMessage>> ();

        private IPlayniteApi _playniteApi { get => GravitonPlugin.PlayniteApi ?? throw new Exception("Playnite API is null, cannot continue!"); }
        private ILogger _logger { get => GravitonPlugin.Logger ?? throw new Exception("Logger is null, cannot continue!"); }
        private GravitonPlugin _plugin { get => GravitonPlugin.Instance; }

        public async Task<List<Game>> Import(ImportGamesArgs args)
        {
            IEnumerable<EmulatorMapping> enabledMappings = _plugin.Settings.Mappings.Where(m => m.Enabled);
            if (enabledMappings == null || !enabledMappings.Any())
            {
                _playniteApi.Notifications.Add(new NotificationMessage($"graviton.emulators.notconfigured", Loc.GetString("NoEmulatorsConfigured"), NotificationSeverity.Error));
                return new List<Game>();
            }

            IList<RomMPlatform>? apiPlatforms = FetchPlatforms();
            if (apiPlatforms == null)
                return new List<Game>();
     
            string url = BuildGeneralROMUrl();

            // Pull ROM data for each enabled mapping and add the games to playnite
            List<Task<List<Game>>> tasks = new List<Task<List<Game>>>();
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
                    _playniteApi.Notifications.Add(new NotificationMessage($"graviton.platform.{mapping.RomMPlatform.Id}.notfound", Loc.GetString("PlatformNotFound", ("PlatformName", mapping.RomMPlatform.Name), ("PlatformID", mapping.RomMPlatformId)), NotificationSeverity.Error));
                    continue;
                }

                // Pull data from server
                _logger.Info($"[Import Controller] Started parsing response for {apiPlatform.Name}.");
                var rommROMs = DownloadROMData(args, url, apiPlatform);
                if (rommROMs == null)
                {
                    _logger.Warn($"[Import Controller] Failed to get ROMs for {apiPlatform.Name}.");
                    continue;
                }
                else
                    _logger.Info($"[Import Controller] Finished parsing response for {apiPlatform.Name}.");


                _logger.Info($"[Import Controller] Creating new import task for {apiPlatform.Name}.");
                // Import games for current mapping 
                tasks.Add(Task<List<Game>>.Factory.StartNew(() =>
                {
                    RomMImport newImport = new RomMImport(args.CancelToken, mapping, rommROMs);
                    return newImport.ProcessData();
                }));

            }

            await Task.WhenAll(tasks);

            List<Game> games = new List<Game>();
            foreach (var task in tasks)
            {
                games.AddRange(task.Result);
            }
            
            return games;
        }

        private IList<RomMPlatform>? FetchPlatforms()
        {
            var result = HttpClientSingleton.RomMGetAsync("/api/platforms").GetAwaiter().GetResult();
            if (result == null)
                return null;

            return JsonSerializer.Deserialize<List<RomMPlatform>>(result) ?? throw new Exception("Failed to deseralize plaforms from server!");
        }

        private string BuildGeneralROMUrl()
        {
            string url = $"{_plugin.Settings.Host}/api/roms";
            string options = "?";

            options += $"genres_logic=none&";
            options += $"order_by=name&";
            options += $"order_dir=asc&";

            if (_plugin.Settings.SkipMissingFiles)
            {
                options += "missing=false&";
            }

            // Exclude genres from import
            List<string> excludeGenres = _plugin.Settings.ExcludeGenres.TrimEnd(' ').TrimEnd(';').Split(';').ToList() ?? new List<string>();
            if (excludeGenres.Count > 0)
            {
                foreach (var genre in excludeGenres)
                {
                    options += $"genres={HttpUtility.UrlEncode(genre)}&";
                }
            }
            options.TrimEnd('&');

            return (url + options);
        }
         
        private List<RomMRom> DownloadROMData(ImportGamesArgs args, string url, RomMPlatform platform)
        {
            _logger.Info($"[Import Controller] Starting to fetch games for {platform.Name}.");

            int pagesize = 50;
            int offset = 0;
            bool hasMoreData = true;

            var romData = new List<RomMRom>();

            url += $"platform_ids={platform.Id}&";
            url += $"limit={pagesize}&";

            // Download data from RomM server
            while (hasMoreData)
            {
                if (args.CancelToken.IsCancellationRequested)
                    break;
                
                try
                {
                    var romURL = url + $"offset={offset}";

                    var request = HttpClientSingleton.RomMGetAsync(romURL).GetAwaiter().GetResult();
                    var roms = request?.RootElement.GetProperty("items").Deserialize<List<RomMRom>>() ?? throw new Exception(Loc.GetString("FailedToDeserialize", ("Object", "ROM data")));
                    romData.AddRange(roms);

                    _logger.Info($"[Import Controller] Parsed {roms.Count} roms for batch {offset / pagesize + 1}.");         

                    if (roms.Count < pagesize)
                    {
                        _logger.Info($"[Import Controller] Received less than {pagesize} roms for {platform.Name}, assuming no more games.");
                        hasMoreData = false;
                        break;
                    }

                    offset += pagesize;
                }
                catch (HttpRequestException ex)
                {
                    _playniteApi.Notifications.Add(new NotificationMessage($"graviton.pull.{platform.Id}.failed", Loc.GetString("DownloadROMDataFailed", ("PlatformName", platform.Name), ("Error", ex.Message)), NotificationSeverity.Error));
                    _logger.Error($"[Import Controller] Request exception: {ex}");
                    hasMoreData = false;
                }
            }

            return romData;
        }
    }

}
