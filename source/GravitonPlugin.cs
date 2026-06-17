using Graviton.Import;
using Graviton.Install.Downloads;
using Graviton.Models.Notifications;
using Graviton.Settings;
using Graviton.Status;

using Playnite;

using System.Diagnostics;
using System.IO;


namespace Graviton
{
    public class GravitonPlugin : Plugin
    {
        public static readonly string Id = "Matthew-Pye.Graviton";
        public static readonly string ExternalIdType = "graviton";
        public static readonly string ExternalIdName = "Graviton (RomM Library)";
        public static readonly Version Version = new Version(0,1,0);

        internal string PluginDLLPath { get; private set; } = "";
        internal string PluginDataPath { get; private set; } = "";

        internal static GravitonPlugin Instance { get; private set; } = null!;
        internal static IPlayniteApi PlayniteApi { get; private set; } = null!;
        internal static ILogger Logger { get; private set; } = null!;

        internal GravitonImportController? ImportController { get; private set; }
        internal StatusController? StatusController { get; private set; }
        internal DownloadQueueController? DownloadQueueController { get; private set; }

        internal GravitonPluginSettings Settings 
        { 
            get
            {
                if (SettingsHandler.InEditingMode)
                    return SettingsHandler.Settings;

                return _settings;
            }
            set
            { _settings = value; } 
        } 

        private GravitonPluginSettings _settings = new();

        internal GravitonSettingsHandler SettingsHandler { get; set; } = new();
        internal RomMAccount? Account { get; private set; }

        private RomMDownloadsAppViewItem? _downloadsAppView { get; set; }
        private DownloadQueueViewModel? _downloadsViewModel;

        public GravitonPlugin() : base()
        {
            if (Instance != null)
                throw new InvalidOperationException("GravitonPlugin instance already initialized.");

            Instance = this;

            XamlId = "Graviton.RomM";
            LibrarySettings = new()
            {
                LibraryName = ExternalIdName,
                ClientName = "RomM",
                ProvidesStoreMetadata = true,
                HasCustomGameImport = true,
                CanImportPlaySessions = true
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
            PlayniteApi = args.Api ?? throw new Exception("Failed to set playnite instance!");
            Loc.Api = args.Api ?? throw new Exception("Failed to set localization api instance!");
            Logger = LogManager.GetLogger();

            await PlayniteApi.Library.Sources.AddAsync(new Source(Id, "Graviton"));

            await PlayniteApi.Library.WebLinkTypes.AddAsync(new WebLinkType("igdb", "IGDB"));
            await PlayniteApi.Library.WebLinkTypes.AddAsync(new WebLinkType("screenscraper", "Screenscraper"));
            await PlayniteApi.Library.WebLinkTypes.AddAsync(new WebLinkType("hasheous", "Hasheous"));
            await PlayniteApi.Library.WebLinkTypes.AddAsync(new WebLinkType("retroachievements", "RetroAchievements"));
            await PlayniteApi.Library.WebLinkTypes.AddAsync(new WebLinkType("howlongtobeat", "HowLongToBeat"));

            await PlayniteApi.Library.ExternalIdentifierTypes.AddAsync(new ExternalIdentifierType("romm", "RomM"));
            await PlayniteApi.Library.ExternalIdentifierTypes.AddAsync(new ExternalIdentifierType("igdb", "IGDB"));
            await PlayniteApi.Library.ExternalIdentifierTypes.AddAsync(new ExternalIdentifierType("screenscraper", "Screenscraper"));
            await PlayniteApi.Library.ExternalIdentifierTypes.AddAsync(new ExternalIdentifierType("hasheous", "Hasheous"));
            await PlayniteApi.Library.ExternalIdentifierTypes.AddAsync(new ExternalIdentifierType("retroachievements", "RetroAchievements"));
            await PlayniteApi.Library.ExternalIdentifierTypes.AddAsync(new ExternalIdentifierType("howlongtobeat", "HowLongToBeat"));

            PluginDataPath = PlayniteApi.UserDataDir;
            PluginDLLPath = args.PluginInstallDir;

            if (!Directory.Exists($"{PluginDataPath}/Platforms/"))
                Directory.CreateDirectory($"{PluginDataPath}/Platforms/");

            if(!Directory.Exists($"{PluginDataPath}/Games/"))
                Directory.CreateDirectory($"{PluginDataPath}/Games/");

            ImportController = new();
            StatusController = new();
            Account = new();

            _downloadsViewModel = new();
            DownloadQueueController = new(_downloadsViewModel, maxConcurrent: 10);
            _downloadsAppView = new();

        }

        public override async Task OnApplicationStartupAsync(OnApplicationStartupArgs args)
        {
            Settings = GravitonSettingsHandler.LoadSettings(PluginDataPath);
            Settings.ProfilePath = string.IsNullOrEmpty(Settings.ProfilePath) ? Path.Combine(PluginDLLPath, @"profile.png") : Settings.ProfilePath;

            if (Settings.LastAuthenticated != null)
            {
                if (Settings.UseBasicAuth)
                {
                    HttpClientSingleton.ConfigureBasicAuth(Settings.UsernameNP, Settings.PasswordNP);
                }
                else
                {
                    HttpClientSingleton.ConfigureClientToken(Settings.ClientTokenNP);
                }

                if (Account == null)
                    throw new Exception("Account hasn't been initailized, cannot continue!");
                
                var result = await Account.Heartbeat();
                if (result != null)
                {
                    Settings.ServerVersion = result.Value.Version;
                    if (await Account.SyncPlatforms())
                        Logger.Info(Loc.GetString("PlatformsSynced", [("PlaformCount", Settings.RomMPlatforms.Count)]));
                }
                   
            } 
        }

        public override Task<PluginSettingsHandler?> GetSettingsHandlerAsync(GetSettingsHandlerArgs args)
        {
            return Task.FromResult<PluginSettingsHandler?>(SettingsHandler);
        }

        public override Task<MetadataProvider?> GetMetadataProviderAsync(GetMetadataProviderArgs args)
        {
            return Task.FromResult<MetadataProvider?>(new GravitonMetadataProvider());
        }
      
        public override Task<List<Game>> ImportGamesAsync(ImportGamesArgs args)
        {
            return ImportController?.Import(args) ?? throw new Exception("Import controller is null, cannot continue");
        }

        public override async Task OnGameCollectionChange(DataCollectionChangeArgs<Game> args)
        {
            if(args.UpdatedItems?.Count > 0 && args.UpdatedItems.Any(x => x.OldData.LibraryId == Id))
            {
                foreach(var updatedGame in args.UpdatedItems.Where(x => x.OldData.LibraryId == Id))
                {
                    foreach (var prop in updatedGame.ChangedProperties)
                    {
                        Logger.Info($"Game: {updatedGame.OldData.Name} | Prop: {prop}");
                    }

                    if(Settings.KeepStatusSynced && updatedGame.ChangedProperties.Contains(nameof(Game.CompletionStatusId)))
                    {
                        await StatusController!.UpdateStatus(updatedGame.NewData);
                    }

                    if (Settings.KeepFavouritesSynced && updatedGame.ChangedProperties.Contains(nameof(Game.Favorite)))
                    {
                        await StatusController!.UpdateFavorites(updatedGame.NewData);
                    }

                }
            }
        }

        public override Task OnGameStartingAsync(OnGameStartingEventArgs args)
        {
            return base.OnGameStartingAsync(args);
        }
        public override Task OnGameStoppedAsync(OnGameStoppedEventArgs args)
        {
            return base.OnGameStoppedAsync(args);
        }

        #region Views

        // Download tab
        public override ICollection<AppViewItemDescriptor>? GetAppViewItemDescriptors(GetAppViewItemDescriptorsArgs args)
        {
            return
            [
                new AppViewItemDescriptor(
                $"{ExternalIdType}.Downloads",
                Loc.GetString("DownloadViewName"),
                // Icon used for sidebar item:
                (iconArgs) => UIIcon.FromBitmapFile($"{PluginDLLPath}/pluginiconBW.png"),
                // Icon used for when the view is activated:
                (iconArgs) => UIIcon.FromBitmapFile($"{PluginDLLPath}/pluginicon.png"))
            ];
        }
        public override AppViewItem? GetAppViewItem(GetAppViewItemsArgs args)
        {
            if (args.ViewId == $"{ExternalIdType}.Downloads")
                return _downloadsAppView;

            return null;
        }

        public override ICollection<MenuItemDescriptor> GetAppMenuItemDescriptors(GetAppMenuItemDescriptorsArgs args)
        {
            return
            [
                new MenuItemDescriptor($"graviton.open.web", "Open RomM library"),
                new MenuItemDescriptor($"graviton.open.account", "Open RomM profile")
            ];
        }
        public override ICollection<MenuItemImpl>? GetAppMenuItems(GetAppMenuItemsArgs args)
        {
            if (args.ItemId == "graviton.open.web" || args.ItemId == "graviton.open.account")
            {
                if (string.IsNullOrEmpty(Settings.Host))
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.open.library", Loc.GetString("HostNotSet"), GravitonSeverity.Error));
                    return null;
                }

                if (!Uri.IsWellFormedUriString(Settings.Host, UriKind.Absolute))
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.open.library", Loc.GetString("HostInvaild"), GravitonSeverity.Error));
                    return null;
                }

                if (args.ItemId == "graviton.open.web")
                {
                    return [new MenuItemImpl("Open RomM library", (_) => { Process.Start(new ProcessStartInfo(Settings.Host) { UseShellExecute = true })?.Dispose(); })];
                }

                if (Settings.UserID < 0)
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.open.library", Loc.GetString("NotAuthenticated"), GravitonSeverity.Error));
                    return null;
                }

                if (args.ItemId == "graviton.open.account")
                {
                    return [new MenuItemImpl("Open RomM profile", (_) => { Process.Start(new ProcessStartInfo($"{Settings.Host}/user/{Settings.UserID}") { UseShellExecute = true })?.Dispose(); })];
                }
            }    

            return null;
        }

        //public override ICollection<MenuItemDescriptor> GetGameMenuItemDescriptors(GetGameMenuItemDescriptorsArgs args)
        //{
        //    //return
        //    //[
        //    //    new MenuItemDescriptor("graviton.open.manual", "Open RomM manual")
        //    //];
        //}

        public override ICollection<MenuItemImpl>? GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            if (args.Games.Count != 1)
                return null;

            //if (args.ItemId == "graviton.open.manual" && args.Games[0].LibraryId == Id)
            //{
            //    var sha1 = args.Games[0].LibraryId.Split(':')[1];
            //    if (Regex.IsMatch(sha1, @"^[0-9a-fA-F]{40}$"))
            //    {
            //        //return [new MenuItemImpl("Open game manual", (_) => ProcessStarter.StartProcess(manualFile))];
            //    }
            //
            //}

            return null;
        }

        #endregion

    }
}