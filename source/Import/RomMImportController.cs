using Graviton.Models;
using Graviton.Models.Notifications;
using Graviton.Models.RomM.Platform;
using Graviton.Models.RomM.Rom;
using Graviton.Settings;

using Playnite;

using SharpCompress.Compressors.Xz;

using Svg;

using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Web;
using System.Windows.Media.Imaging;

using static Playnite.Plugin;

namespace Graviton.Import
{
    public class RomMImportController
    {
        //public List<NotificationMessage> Notifications = new List<NotificationMessage>> ();

        private IPlayniteApi _playniteAPI { get => GravitonPlugin.PlayniteApi ?? throw new Exception("Playnite API is null, cannot continue!"); }
        private ILogger _logger { get => GravitonPlugin.Logger ?? throw new Exception("Logger is null, cannot continue!"); }
        private GravitonPlugin _plugin {get => GravitonPlugin.Instance; }

        public async Task<List<Game>> Import(ImportGamesArgs args)
        {
            IEnumerable<EmulatorMapping> enabledMappings = _plugin.Settings.Mappings.Where(m => m.Enabled);
            if (enabledMappings == null || !enabledMappings.Any())
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
                var rommROMs = DownloadROMData(args, url, apiPlatform);
                if (rommROMs == null)
                {
                    _logger.Error($"[Import Controller] Failed to get ROMs for {apiPlatform.Name}.");
                    continue;
                }
                else
                    _logger.Debug($"[Import Controller] Finished parsing response for {apiPlatform.Name}.");


                _logger.Debug($"[Import Controller] Creating new import task for {apiPlatform.Name}.");
                tasks.Add(new RomMImport(args.CancelToken, mapping, rommROMs).ProcessData());

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
                RemoveMissingGames(proccessedgames);

            return games;
        }

        public async Task<List<RomMPlatform>?> FetchPlatforms()
        {
            var result = await HttpClientSingleton.RomMGetAsync("/api/platforms");
            if (result == null)
                return null;

            var platforms = JsonSerializer.Deserialize<List<RomMPlatform>>(result) ?? throw new Exception("Failed to deseralize plaforms from server!");

            if (!Directory.Exists($"{_plugin.PluginDataPath}/Platforms/"))
                Directory.CreateDirectory($"{_plugin.PluginDataPath}/Platforms/");

            Stream stream = await HttpClientSingleton.Instance?.GetStreamAsync($"{_plugin.Settings.Host}/assets/default-C7fJO_0F.ico")!;

            Bitmap? png = null;
            using (var iconStream = new MemoryStream())
            {
                var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
                using (var pngStream = new MemoryStream())
                {
                    var encoder = new PngBitmapEncoder();
                    foreach (var frame in decoder.Frames)
                    {
                        encoder.Frames.Add(frame);
                    }
                    encoder.Save(pngStream);
                    png = (Bitmap)Bitmap.FromStream(pngStream);
                }
            }
            png.Save($"{_plugin.PluginDataPath}/Platforms/general.png", ImageFormat.Png);

            foreach (var platform in platforms)
            {
                try
                {
                    stream = await HttpClientSingleton.Instance?.GetStreamAsync($"{_plugin.Settings.Host}/assets/platforms/{platform.Slug}.svg")!;
                    var svg = SvgDocument.Open<SvgDocument>(stream);
                    var image = svg.Draw();

                    image.Save($"{_plugin.PluginDataPath}/Platforms/{platform.Slug}.png", ImageFormat.Png);
                }
                catch {}
                
            }

            return platforms;
        }


        private string BuildGeneralROMUrl()
        {
            string url = $"/api/roms";
            string options = "?";

            options += $"genres_logic=none&";
            options += $"order_by=name&";
            options += $"order_dir=asc&";

            if (_plugin.Settings.SkipMissingFiles)
            {
                options += "missing=false&";
            }

            // Exclude genres from import
            if(!string.IsNullOrEmpty(_plugin.Settings.ExcludeGenres))
            {
                List<string> excludeGenres = _plugin.Settings.ExcludeGenres.TrimEnd(' ').TrimEnd(';').Split(';').ToList() ?? new List<string>();
                if (excludeGenres.Count > 0)
                {
                    foreach (var genre in excludeGenres)
                    {
                        options += $"genres={HttpUtility.UrlEncode(genre)}&";
                    }
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
                if (!File.Exists($"{_plugin.PluginDataPath}/Games/{game.LibraryGameId?.Split(':')[1]}.json"))
                    continue;

                var gamejson = JsonSerializer.Deserialize<RomMRomLocal>(File.ReadAllText($"{_plugin.PluginDataPath}/Games/{game.LibraryGameId?.Split(':')[1]}.json"));

                if(_plugin.Settings.Mappings.Any(x => x.MappingId == gamejson?.MappingID))
                {
                    // Don't remove games from mappings that are disabled
                    if (!_plugin.Settings.Mappings.First(x => x.MappingId == gamejson?.MappingID).Enabled)
                        continue;

                    if (ImportedGames.Contains(game.LibraryGameId!))
                        continue;
                }

                if (_playniteAPI.Library.GameRelations.Any(x => x.PrimaryGame == game.Id))
                {
                    var gamerelation = _playniteAPI.Library.GameRelations.First(x => x.PrimaryGame == game.Id);
                    await _playniteAPI.Library.GameRelations.RemoveAsync(gamerelation.Id);
                }   
                else if(_playniteAPI.Library.GameRelations.Any(x => x.LinkedGames.Any(y => y == game.Id)))
                {
                    var gamerelations = _playniteAPI.Library.GameRelations.Where(x => x.LinkedGames.Any(y => y == game.Id));
                    foreach( var gamerelation in gamerelations)
                    {
                        gamerelation.LinkedGames.Remove(game.Id);
                        await _playniteAPI.Library.GameRelations.UpdateAsync(gamerelation);
                    }
                }

                await _playniteAPI.Library.Games.RemoveAsync(game.Id);
                

                File.Delete($"{_plugin.PluginDataPath}/Games/{game.LibraryGameId?.Split(':')[1]}.json");

                _logger.Info($"[Importer] Removing {game.Name}");
            }

            _logger.Info($"[Importer] Finished removing not found games");
        }
    }

}
