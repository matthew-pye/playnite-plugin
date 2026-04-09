using Playnite;

using RomMLibrary.Install.Downloads;
using RomMLibrary.Settings;

using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;


namespace RomMLibrary
{
    public static class HttpClientSingleton
    {
        private static readonly HttpClient httpClient = new HttpClient();

        static HttpClientSingleton()
        {
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public static void ConfigureBasicAuth(string username, string password)
        {
            var base64Credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);
        }
        public static void ConfigureClientToken(string clientToken)
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);
        }

        public static HttpClient Instance => httpClient;
    }


    public class RomMLibraryPlugin : Plugin
    {
        public static readonly string Id = "Matthew-Pye.RomMLibrary";
        public static readonly string ExternalIdType = "romm";
        public static readonly string ExternalIdName = "RomM";

        public static IPlayniteApi? PlayniteApi { get; private set; }
        public static ILogger? Logger { get; private set; }
        public RomMLibraryPluginSettings Settings { get; set; } = new();

        public string PluginDLLPath { get; private set; } = "";
        public string PluginDataPath { get; private set; } = "";

        private readonly DownloadQueueViewModel DownloadsViewModel;
        public DownloadQueueController DownloadQueueController { get; private set; }
        internal RomMDownloadsSidebarItem DownloadsSidebar { get; private set; }

        public RomMLibraryPlugin() : base()
        {
            XamlId = "RomM";
            LibrarySettings = new()
            {
                LibraryName = "RomM",
                ClientName = "RomM",
                ProvidesStoreMetadata = true,
                HasCustomGameImport = true,
            };
            MetadataSettings = new()
            {
                Name = "RomM Metadata",
                SupportedDataIds = [
                BuiltInGameDataId.Name,
                BuiltInGameDataId.ReleaseDate,
                BuiltInGameDataId.DesktopCover,
                BuiltInGameDataId.Genres,
                BuiltInGameDataId.Description,
                BuiltInGameDataId.CriticScore,
                BuiltInGameDataId.Links,
                BuiltInGameDataId.Features,
                BuiltInGameDataId.Series,
                BuiltInGameDataId.Platforms,
            ]
            };

            DownloadsViewModel = new DownloadQueueViewModel();
            DownloadQueueController = new DownloadQueueController(this, DownloadsViewModel, maxConcurrent: 10);
            DownloadsSidebar = new RomMDownloadsSidebarItem(this);
           
        }

        public override async Task InitializeAsync(InitializeArgs args)
        {
            PlayniteApi = args.Api;
            Loc.Api = args.Api;
            Logger = LogManager.GetLogger();

            Settings = RomMLibrarySettingsHandler.LoadSettings(PlayniteApi.UserDataDir);

            PluginDataPath = PlayniteApi.UserDataDir;
            PluginDLLPath = args.PluginInstallDir;

            await Task.CompletedTask;
        }

        public override async Task<PluginSettingsHandler?> GetSettingsHandlerAsync(GetSettingsHandlerArgs args)
        {
            await Task.CompletedTask;
            return new RomMLibrarySettingsHandler(this, args);
        }





    }
}