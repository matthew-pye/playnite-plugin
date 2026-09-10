using Graviton.Models;
using Graviton.Models.Notifications;
using Graviton.Models.RomM.Collection;
using Graviton.Models.RomM.Platform;
using Graviton.Models.RomM.Rom;
using Graviton.Notifications;
using Graviton.Settings;

using Playnite;

using Svg;

using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

using static Playnite.Plugin;

namespace Graviton.Import
{
    public class GravitonImportController
    {
        private GravitonPlugin _plugin;
        private IPlayniteApi _playniteAPI;
        private GravitonLogger _logger;
        private IRomMServer _romMServer;

        private static readonly Regex _SHA1Regex = new Regex("^[a-fA-F0-9]{40}$");
        private static readonly Regex _platformSlugRegex = new Regex("^[a-zA-Z0-9_\\-]+$");

        public GravitonImportController(GravitonPlugin plugin, IPlayniteApi playniteAPI, GravitonLogger logger, IRomMServer server)
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;
            _romMServer = server;
        }

        public async Task<List<Game>> Import(ImportGamesArgs args)
        {
            var enabledMappings = _plugin.Settings.Mappings.Where(m => m.Enabled).ToList();
            if (!enabledMappings.Any())
            {
                GravitonNotify.Notify($"graviton.emulators.notconfigured", Loc.GetString("NoEmulatorsConfigured"), GravitonSeverity.Warn);
                _plugin.ImportInProgress = false;
                return new List<Game>();
            }

            IList<RomMPlatform>? apiPlatforms = await FetchPlatforms();
            if (apiPlatforms == null)
            {
                _plugin.ImportInProgress = false;
                return new List<Game>();
            }
                
            _plugin.Settings.RomMPlatforms = apiPlatforms.ToObservableCollection();
            foreach (var mapping in _plugin.Settings.Mappings)
            {
                mapping.AvailablePlatforms = _plugin.Settings.RomMPlatforms.Where(x => x.RomCount > 0).ToObservableCollection();
            }
            GravitonSettingsHandler.SaveSettings(_plugin.PluginDataPath, _plugin.Settings);

            var collections = await FetchCollections(args);

            string url = BuildGeneralROMUrl();

            List<EmulatorMapping> processedMappings = new();

            // Pull ROM data for each enabled mapping and add the games to playnite
            List<Task<(List<Game> NewGames, List<string> ProcessedGames)>> tasks = new();
            foreach (var mapping in enabledMappings)
            {
                if (args.CancelToken.IsCancellationRequested)
                    break;

                // Check mapping has an Emulator, Profile & Platform assigned to it
                if (!mapping.IsSetup)
                {
                    GravitonNotify.Notify($"graviton.mapping.incomplete", $"One or more mappings are not fully setup, those mapping have been skipped", GravitonSeverity.Warn);
                    continue;
                }

                RomMPlatform? apiPlatform = apiPlatforms.FirstOrDefault(p => p.Id == mapping.RomMPlatformId);
                if (apiPlatform == null)
                {
                    GravitonNotify.Notify($"graviton.platform.{mapping.RomMPlatform!.Id}.notfound", Loc.GetString("PlatformNotFound", ("PlatformName", mapping.RomMPlatform.Name), ("PlatformID", mapping.RomMPlatformId)), GravitonSeverity.Error);
                    continue;
                }

                // Pull data from server
                _logger.Debug($"[Import Controller] Started parsing response for {apiPlatform.Name}.");
                var rommROMs = await DownloadROMData(args, url, apiPlatform);

                if (args.CancelToken.IsCancellationRequested)
                    break;

                if (rommROMs.Count() <= 0)
                    continue;
                else
                    _logger.Debug($"[Import Controller] Finished parsing response for {apiPlatform.Name}.");

                // Remove ROMs that are in the exclusion list
                foreach (var rom in rommROMs.ToList())
                {
                    if (args.Exclusions?.Any(x => x.GameId == rom.Id.ToString()) ?? false)
                        rommROMs.Remove(rom);   
                }


                _logger.Debug($"[Import Controller] Creating new import task for {apiPlatform.Name}.");
                tasks.Add(new GravitonImport(_plugin, _playniteAPI, _logger, args, mapping, rommROMs, collections).ProcessData());
                processedMappings.Add(mapping);
            }

            await Task.WhenAll(tasks);

            List<Game> games = new List<Game>();
            List<string> proccessedgames = new List<string>();
            foreach (var task in tasks)
            {
                games.AddRange(task.Result.NewGames);
                proccessedgames.AddRange(task.Result.ProcessedGames);
            }

            if (!_plugin.Settings.KeepDeletedGames)
                await RemoveMissingGames(proccessedgames, processedMappings);

            _plugin.ImportInProgress = false;
            var sessionsstring = JsonSerializer.Serialize(_playniteAPI.Library.GameSessions, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText($"{_plugin.PluginDataPath}/temp/sessions.json", sessionsstring);
            return games;
        }

        public async Task<List<RomMPlatform>?> FetchPlatforms()
        {
            var result = await _romMServer.GETAsync("/api/platforms");
            if (result == null)
            {
                _plugin.Settings.AccountState.LastAuthenticated = null;
                return null;
            }
                
            var platforms = result.RootElement.Deserialize<List<RomMPlatform>>() ?? throw new Exception("Failed to deseralize plaforms from server!");

            if (!Directory.Exists($"{_plugin.PluginDataPath}/Platforms/"))
                Directory.CreateDirectory($"{_plugin.PluginDataPath}/Platforms/");

            foreach (var platform in platforms)
            {
                try
                {
                    if(_platformSlugRegex.IsMatch(platform.Slug!))
                    {
                        var rawResponse = await _romMServer.RawGETAsync($"/assets/platforms/{platform.Slug}.svg");
                        if (rawResponse == null || rawResponse.Content == null)
                            throw new Exception("Failed to get response from server");

                        Stream stream = await rawResponse.Content.ReadAsStreamAsync();
                        var svg = SvgDocument.Open<SvgDocument>(stream);
                        
                        var image = svg.Draw();
                        image.Save($"{_plugin.PluginDataPath}/Platforms/{platform.Slug}.png", ImageFormat.Png);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn($"[Import Controller] Failed to download/convert platform icon for {platform.Slug}: {ex.Message}");
                }
            }

            return platforms;
        }

        private string BuildGeneralROMUrl()
        {
            string url = $"/api/roms";
            string options = "?";

            options += $"genres_logic=none&";
            options += $"order_by=name&";
            options += $"with_siblings=true&";
            options += $"with_files=true&";
            options += $"order_dir=asc&";

            if (_plugin.Settings.SkipMissingFiles)
            {
                options += "missing=false&";
            }

            // Exclude genres from import
            if(!string.IsNullOrEmpty(_plugin.Settings.ExcludeGenres))
            {
                List<string> excludeGenres = _plugin.Settings.ExcludeGenres.TrimEnd(' ').TrimEnd(';').Split(';').ToList();
                if (excludeGenres.Count > 0)
                {
                    foreach (var genre in excludeGenres)
                    {
                        options += $"genres={HttpUtility.UrlEncode(genre)}&";
                    }
                }
            }

            return url + options;
        }
         
        private async Task<List<RomMRom>> DownloadROMData(ImportGamesArgs args, string url, RomMPlatform platform)
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

                    var request = await _romMServer.GETAsync(romURL);
                    if(request == null)
                        throw new Exception("Server returned null data");

                    var roms = request?.RootElement.GetProperty("items").Deserialize<List<RomMRom>>() ?? throw new Exception("Deserialize failed");
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
                catch (Exception ex)
                {
                    romData.Clear();
                    GravitonNotify.Notify($"graviton.GET.roms.{platform.Id}.failed", Loc.GetString("DownloadROMDataFailed", ("PlatformName", platform.Name), ("Error", ex.Message)), GravitonSeverity.Error, ex);
                    hasMoreData = false;
                }
            }

            return romData;
        }

        private async Task RemoveMissingGames(List<string> ImportedGames, List<EmulatorMapping> processedMappings)
        {

            _logger.Info($"[Importer] Starting to remove not found games.");

            foreach (var game in _plugin.ImportedGames.ToList())
            {
                if (ImportedGames.Contains(game.Key))
                    continue;

                if (!GravitonHelper.TryParseGameID(game.Key, out var id))
                    continue;

                if (File.Exists($"{_plugin.PluginDataPath}/Games/{id}.json"))
                {
                    var gamejson = JsonSerializer.Deserialize<RomMRomLocal>(File.ReadAllText($"{_plugin.PluginDataPath}/Games/{id}.json"));

                    var mapping = _plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == gamejson?.MappingID);
                    if (mapping != null)
                    {
                        // Don't remove games from mappings that are disabled or where skipped on import
                        if (!mapping.Enabled || !processedMappings.Contains(mapping))
                            continue;
                    }
                }

                var rootgamerelation = _playniteAPI.Library.GameRelations.FirstOrDefault(x => x.PrimaryGame == game.Value.PlayniteID);
                if (rootgamerelation != null)
                {
                    await _playniteAPI.Library.GameRelations.RemoveAsync(rootgamerelation.Id);
                }
                else
                {
                    var linkedRelations = _playniteAPI.Library.GameRelations.Where(x => x.LinkedGames.Any(y => y == game.Value.PlayniteID)).ToList();
                    foreach (var gamerelation in linkedRelations)
                    {
                        gamerelation.LinkedGames.Remove(game.Value.PlayniteID!);
                        await _playniteAPI.Library.GameRelations.UpdateAsync(gamerelation);
                    }
                }

                await _playniteAPI.Library.Games.RemoveAsync(game.Value.PlayniteID!);
                _plugin.ImportedGames.TryRemove(game.Key, out _);
                
                File.Delete($"{_plugin.PluginDataPath}/Games/{id}.json");

                _logger.Info($"[Importer] Removing {id}");
            }

            _logger.Info($"[Importer] Finished removing not found games");
        }

        private async Task<List<RomMCollection>> FetchCollections(ImportGamesArgs args)
        {
            List<RomMCollection> collections = new List<RomMCollection>();
            if (_plugin.Settings.AddCollectiontoPlayniteCategory)
            {
                if (args.CancelToken.IsCancellationRequested)
                    return collections;

                var result = await _romMServer.GETAsync("/api/collections");
                if (result != null)
                {
                    try
                    {
                        var manualcollections = result.RootElement.Deserialize<List<RomMCollection>>();
                        if (manualcollections != null)
                            collections.AddRange(manualcollections);
                    }
                    catch (Exception ex)
                    {
                        GravitonNotify.Notify($"graviton.fetchcollection.failed", $"Failed to get manual collections: {ex.Message}", GravitonSeverity.Error, ex);
                    } 
                }
            }

            if(_plugin.Settings.AddSmartCollectiontoPlayniteCategory)
            {
                if (args.CancelToken.IsCancellationRequested)
                    return collections;

                var result = await _romMServer.GETAsync("/api/collections/smart");
                if (result != null)
                {
                    try
                    {
                        var manualcollections = result.RootElement.Deserialize<List<RomMCollection>>();
                        if (manualcollections != null)
                            collections.AddRange(manualcollections);
                    }
                    catch (Exception ex)
                    {
                        GravitonNotify.Notify($"graviton.fetchcollection.failed", $"Failed to get smart collections: {ex.Message}", GravitonSeverity.Error, ex);
                    }
                }
            }

            if (_plugin.Settings.AddVirtualCollectiontoPlayniteCategory)
            {
                if (args.CancelToken.IsCancellationRequested)
                    return collections;

                var result = await _romMServer.GETAsync("/api/collections/virtual/identifiers");
                if (result == null)
                    return collections;

                var collectionIDs = result.RootElement.Deserialize<List<string>>();
                if(collectionIDs == null)
                    return collections;

                foreach (var id in collectionIDs)
                {
                    if (args.CancelToken.IsCancellationRequested)
                        break;

                    result = await _romMServer.GETAsync($"/api/collections/virtual/{id}");
                    if (result == null)
                        continue;

                    try
                    {
                        collections.Add(result.RootElement.Deserialize<RomMCollection>() ?? throw new Exception("Failed to deserialze collection"));
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }

            return collections;
        }



    }
}