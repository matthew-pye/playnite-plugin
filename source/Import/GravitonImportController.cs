using Graviton.Models;
using Graviton.Models.Notifications;
using Graviton.Models.RomM.Platform;
using Graviton.Models.RomM.Rom;

using Playnite;

using Svg;

using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

using static Playnite.Plugin;

namespace Graviton.Import
{
    public class GravitonImportController
    {
        private GravitonPlugin _plugin { get => GravitonPlugin.Instance; }
        private IPlayniteApi _playniteAPI { get => GravitonPlugin.PlayniteApi; }
        private ILogger _logger { get => GravitonPlugin.Logger; }

        private static readonly Regex _SHA1Regex = new Regex("^[a-fA-F0-9]{40}$");
        private static readonly Regex _platformSlugRegex = new Regex("^[a-zA-Z0-9_\\-]+$");

        public async Task<List<Game>> Import(ImportGamesArgs args)
        {
            var enabledMappings = _plugin.Settings.Mappings.Where(m => m.Enabled).ToList();
            if (!enabledMappings.Any())
            {
                _playniteAPI.Notifications.Add(new NotificationMessage($"graviton.emulators.notconfigured", Loc.GetString("NoEmulatorsConfigured"), NotificationSeverity.Error));
                return new List<Game>();
            }

            IList<RomMPlatform>? apiPlatforms = await FetchPlatforms();
            if (apiPlatforms == null)
                return new List<Game>();

            _plugin.Settings.RomMPlatforms = apiPlatforms.ToObservableCollection();

            string url = BuildGeneralROMUrl();

            // Pull ROM data for each enabled mapping and add the games to playnite
            List<Task<(List<Game> NewGames, List<string> ProcessedGames)>> tasks = new();
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
                    GravitonNotify.Add(new GravitonNotification($"graviton.platform.{mapping.RomMPlatform!.Id}.notfound", Loc.GetString("PlatformNotFound", ("PlatformName", mapping.RomMPlatform.Name), ("PlatformID", mapping.RomMPlatformId)), GravitonSeverity.Error));
                    continue;
                }

                // Pull data from server
                _logger.Debug($"[Import Controller] Started parsing response for {apiPlatform.Name}.");
                var rommROMs = await DownloadROMData(args, url, apiPlatform);

                if (args.CancelToken.IsCancellationRequested)
                    break;

                if (rommROMs == null)
                    continue;
                else
                    _logger.Debug($"[Import Controller] Finished parsing response for {apiPlatform.Name}.");


                _logger.Debug($"[Import Controller] Creating new import task for {apiPlatform.Name}.");
                tasks.Add(new GravitonImport(args.CancelToken, mapping, rommROMs).ProcessData());

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
                await RemoveMissingGames(proccessedgames);

            return games;
        }

        public async Task<List<RomMPlatform>?> FetchPlatforms()
        {
            var result = await HttpClientSingleton.RomMGetAsync("/api/platforms");
            if (result == null)
                return null;

            var platforms = result.RootElement.Deserialize<List<RomMPlatform>>() ?? throw new Exception("Failed to deseralize plaforms from server!");

            if (!Directory.Exists($"{_plugin.PluginDataPath}/Platforms/"))
                Directory.CreateDirectory($"{_plugin.PluginDataPath}/Platforms/");

            foreach (var platform in platforms)
            {
                try
                {
                    if(_platformSlugRegex.IsMatch(platform.Slug!))
                    {
                        using Stream stream = await HttpClientSingleton.Instance!.GetStreamAsync($"{_plugin.Settings.Host}/assets/platforms/{platform.Slug}.svg");
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

                    var request = await HttpClientSingleton.RomMGetAsync(romURL);
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
                    GravitonNotify.Add(new GravitonNotification($"graviton.GET.roms.{platform.Id}.failed", Loc.GetString("DownloadROMDataFailed", ("PlatformName", platform.Name), ("Error", ex.Message)), GravitonSeverity.Error));
                    hasMoreData = false;
                }
            }

            return romData;
        }

        private async Task RemoveMissingGames(List<string> ImportedGames)
        {

            var gamesInDatabase = _playniteAPI.Library.Games.Where(g =>
                        g.SourceId != null && g.SourceId == GravitonPlugin.Id
                    );

            _logger.Info($"[Importer] Starting to remove not found games.");

            foreach (var game in gamesInDatabase)
            {
                var splitID = game.LibraryGameId?.Split(':');
                if (splitID == null || splitID.Length != 2)
                    continue;

                string gameSHA1 = splitID[1]!;
                if(!_SHA1Regex.IsMatch(gameSHA1))
                    continue;

                if (!File.Exists($"{_plugin.PluginDataPath}/Games/{gameSHA1}.json"))
                    continue;

                var gamejson = JsonSerializer.Deserialize<RomMRomLocal>(File.ReadAllText($"{_plugin.PluginDataPath}/Games/{splitID[1]}.json"));

                var mapping = _plugin.Settings.Mappings.FirstOrDefault(x => x.MappingId == gamejson?.MappingID);
                if (mapping != null)
                {
                    // Don't remove games from mappings that are disabled
                    if (!mapping.Enabled)
                        continue;
                    if (ImportedGames.Contains(game.LibraryGameId!))
                        continue;
                }

                var rootgamerelation = _playniteAPI.Library.GameRelations.FirstOrDefault(x => x.PrimaryGame == game.Id);
                if (rootgamerelation != null)
                {
                    await _playniteAPI.Library.GameRelations.RemoveAsync(rootgamerelation.Id);
                }
                else
                {
                    var linkedRelations = _playniteAPI.Library.GameRelations.Where(x => x.LinkedGames.Any(y => y == game.Id)).ToList();
                    foreach (var gamerelation in linkedRelations)
                    {
                        gamerelation.LinkedGames.Remove(game.Id);
                        await _playniteAPI.Library.GameRelations.UpdateAsync(gamerelation);
                    }
                }

                await _playniteAPI.Library.Games.RemoveAsync(game.Id);
                

                File.Delete($"{_plugin.PluginDataPath}/Games/{splitID[1]}.json");

                _logger.Info($"[Importer] Removing {game.Name}");
            }

            _logger.Info($"[Importer] Finished removing not found games");
        }
    }

}
