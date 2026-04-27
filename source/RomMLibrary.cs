using Playnite;

using RomM.Import;

using RomMLibrary.Install.Downloads;
using RomMLibrary.Settings;
using RomMLibrary.Status;

using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace RomMLibrary
{
    public static class HttpClientSingleton
    {
        private static readonly HttpClient httpClient = new HttpClient();
        public static HttpClient Instance => httpClient;

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
    }

    public class RomMLibraryPlugin : Plugin
    {
        public static readonly string Id = "Matthew-Pye.RomMLibrary";
        public static readonly string ExternalIdType = "romm";
        public static readonly string ExternalIdName = "RomM";
        public static readonly Version Version = new Version(0,0,4);

        public static IPlayniteApi? PlayniteApi { get; private set; }
        public static ILogger? Logger { get; private set; }
        public RomMLibraryPluginSettings Settings { get; set; } = new();

        public string PluginDLLPath { get; private set; } = "";
        public string PluginDataPath { get; private set; } = "";

        public StatusController? StatusController { get; private set; }

        private DownloadQueueViewModel? DownloadsViewModel;
        internal RomMDownloadsAppViewItem? DownloadsAppView { get; private set; }
        public DownloadQueueController? DownloadQueueController { get; private set; }
        

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
                    BuiltInGameDataId.Description,
                    BuiltInGameDataId.Note,
                    BuiltInGameDataId.DesktopCover,
                    BuiltInGameDataId.Genres,
                    BuiltInGameDataId.Tags,
                    BuiltInGameDataId.Features,
                    BuiltInGameDataId.Platforms,
                    BuiltInGameDataId.Categories,
                    BuiltInGameDataId.Series,
                    BuiltInGameDataId.AgeRating,
                    BuiltInGameDataId.Region,
                    BuiltInGameDataId.CompletionStatus,
                    BuiltInGameDataId.UserScore,
                    BuiltInGameDataId.CommunityScore,
                    BuiltInGameDataId.ReleaseDate,
                    BuiltInGameDataId.ObtainedDate,
                    BuiltInGameDataId.LastPlayedDate,
                    BuiltInGameDataId.Favorite,
                    BuiltInGameDataId.Links,
                    BuiltInGameDataId.TTBMainEstimated,
                    BuiltInGameDataId.TTBMainSidesEstimated,
                    BuiltInGameDataId.TTBCompletionEstimated,
                ]
            };
        }

        public override async Task InitializeAsync(InitializeArgs args)
        {
            PlayniteApi = args.Api;
            Loc.Api = args.Api;
            Logger = LogManager.GetLogger();

            await PlayniteApi.Library.Sources.AddAsync(new Source(Id, "RomM"));

            PluginDataPath = PlayniteApi.UserDataDir;
            PluginDLLPath = args.PluginInstallDir;

            Settings = RomMLibrarySettingsHandler.LoadSettings(PlayniteApi.UserDataDir);
            Settings.ProfilePath = Path.Combine(PluginDLLPath, @"profile.png");

            StatusController = new StatusController(this);

            DownloadsViewModel = new DownloadQueueViewModel();
            DownloadQueueController = new DownloadQueueController(this, DownloadsViewModel, maxConcurrent: 10);
            DownloadsAppView = new RomMDownloadsAppViewItem(this);

            await Task.CompletedTask;
        }

        public override async Task<PluginSettingsHandler?> GetSettingsHandlerAsync(GetSettingsHandlerArgs args)
        {
            await Task.CompletedTask;
            return new RomMLibrarySettingsHandler(this, args);
        }

        public override async Task<MetadataProvider?> GetMetadataProviderAsync(GetMetadataProviderArgs args)
        {
            return new RomMLibraryMetadataProvider(this);
        }

        #region Views
        // Download tab
        public override ICollection<AppViewItemDescriptor>? GetAppViewItemDescriptors(GetAppViewItemDescriptorsArgs args)
        {
            return
            [
                new AppViewItemDescriptor(
                $"{Id}.Downloads",
                Loc.GetString("DownloadViewName"),
                // Icon used for sidebar item:
                (iconArgs) => UIIcon.FromBitmapFile(Path.Combine(PluginDLLPath, "pluginiconBW.png")),
                // Icon used for when the view is activated:
                (iconArgs) => UIIcon.FromBitmapFile(Path.Combine(PluginDLLPath, "pluginicon.png")))
            ];
        }
        public override AppViewItem? GetAppViewItem(GetAppViewItemsArgs args)
        {
            if (args.ViewId == $"{Id}.Downloads")
                return DownloadsAppView;

            return null;
        }

        public override ICollection<MenuItemDescriptor> GetAppMenuItemDescriptors(GetAppMenuItemDescriptorsArgs args)
        {
            return
            [
                new MenuItemDescriptor("RomM.open.web", "Open RomM library"),
            new MenuItemDescriptor("RomM.open.account", "Open RomM profile")
            ];
        }
        public override ICollection<MenuItemImpl>? GetAppMenuItems(GetAppMenuItemsArgs args)
        {
            if(!string.IsNullOrEmpty(Settings.Host))
            {
                if (args.ItemId == "RomM.open.web")
                    return [new MenuItemImpl("Open RomM library", () => System.Diagnostics.Process.Start(Settings.Host)?.Dispose())];

                if (args.ItemId == "RomM.open.account" && Settings.UserID >= 0)
                    return [new MenuItemImpl("Open RomM profile", () => System.Diagnostics.Process.Start($"{Settings.Host}/user/{Settings.UserID}")?.Dispose())];
            }
            
            return null;
        }
        #endregion

        public override Task<List<Game>> ImportGamesAsync(ImportGamesArgs args)
        {
            return base.ImportGamesAsync(args);
        }

        public override Task OnGameStartingAsync(OnGameStartingEventArgs args)
        {
            if(File.Exists($"{PluginDataPath}//Games//{args.Game.LibraryGameId}.json"))
            {

            }

            return base.OnGameStartingAsync(args);
        }
        public override Task OnGameStoppedAsync(OnGameStoppedEventArgs args)
        {
            return base.OnGameStoppedAsync(args);
        }

    }
}