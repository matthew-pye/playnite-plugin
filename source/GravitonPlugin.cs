using Emunight;

using Graviton.GameEdit;
using Graviton.Import;
using Graviton.Install;
using Graviton.Install.Downloads;
using Graviton.Models.Install;
using Graviton.Models.Notifications;
using Graviton.Models.ROM;
using Graviton.Notifications;
using Graviton.Play;
using Graviton.Saves;
using Graviton.Settings;
using Graviton.Status;

using Playnite;
using Playnite.WebViews;

using Svg;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;


namespace Graviton
{
    internal interface IGravitonContext
    {
        public IEmunightAPI? EmunightAPI { get; }

        GravitonImportController? ImportController { get; }
        SaveController? SaveController { get; }
        GameSessionHandler? GameSessionHandler { get; }
        StatusController? StatusController { get; }
        DownloadQueueController? DownloadQueueController { get; }
        GravitonInstallController? InstallController { get; }
        GravitonRemoteInstallController? RemoteInstallController { get; set; }

        public RomMAuthentication? Account { get; }

        string PluginDLLPath { get; }
        string PluginDataPath { get; }

        bool PluginInitialized { get; set; }
        bool ImportInProgress { get; set; }

        GravitonPluginSettings Settings { get; set; }

        ConcurrentDictionary<string, RomMRomLocal> ImportedGames { get; }
    }

    public class GravitonPlugin : Plugin, IGravitonContext
    {
        public static readonly string Id = "Matthew-Pye.Graviton";
        public static readonly string ExternalIdType = "graviton";
        public static readonly string ExternalIdName = "Graviton (RomM Library)";
        public static readonly Version Version = new Version(0, 4, 0);

        public string PluginDLLPath { get; private set; } = "";
        public string PluginDataPath { get; private set; } = "";

        public GravitonPluginSettings Settings
        {
            get
            {
                if (SettingsHandler != null && SettingsHandler.InEditingMode)
                    return SettingsHandler.Settings;

                return _settings;
            }
            set
            { _settings = value; }
        }

        public ConcurrentDictionary<string, RomMRomLocal> ImportedGames { get; private set; } = new();

        internal static GravitonPlugin Instance { get; private set; } = null!;
        internal static IPlayniteApi PlayniteApi { get; private set; } = null!;
        internal static GravitonLogger Logger { get; private set; } = new();
        internal static RomMServer RomMServer { get; private set; } = null!;

        public IEmunightAPI? EmunightAPI { get; private set; }

        public GravitonImportController? ImportController { get; private set; }
        public SaveController? SaveController { get; private set; }
        public GameSessionHandler? GameSessionHandler { get; private set; }
        public StatusController? StatusController { get; private set; }
        public DownloadQueueController? DownloadQueueController { get; private set; }
        public GravitonPlayController? PlayController { get; private set; }
        public GravitonInstallController? InstallController { get; private set; }
        public GravitonRemoteInstallController? RemoteInstallController { get; set; }


        private GravitonPluginSettings _settings = new();

        internal GravitonSettingsHandler? SettingsHandler { get; set; }
        public RomMAuthentication? Account { get; private set; }

        private RomMDownloadsAppViewItem? _downloadsAppView { get; set; }
        private DownloadQueueViewModel? _downloadsViewModel;

        public bool PluginInitialized { get; set; } = false;
        public bool ImportInProgress { get; set; } = false;


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
                    BuiltInGameDataId.DesktopCover,

                    BuiltInGameDataId.Genres,
                    BuiltInGameDataId.Tags,
                    BuiltInGameDataId.Features,
                    BuiltInGameDataId.Platforms,
                    BuiltInGameDataId.Categories,
                    BuiltInGameDataId.Series,
                    BuiltInGameDataId.AgeRating,
                    BuiltInGameDataId.Region,
                    BuiltInGameDataId.CommunityScore,
                    BuiltInGameDataId.ReleaseDate,
                    BuiltInGameDataId.EstimatedInstallSize,

                    BuiltInGameDataId.CompletionStatus,
                    BuiltInGameDataId.UserScore,             
                    BuiltInGameDataId.ObtainedDate,
                    BuiltInGameDataId.LastPlayedDate,
                    BuiltInGameDataId.Favorite,
                    BuiltInGameDataId.Hidden,

                    BuiltInGameDataId.Links,
                    BuiltInGameDataId.ExternalIds,

                    BuiltInGameDataId.TimeToBeatEstimated
                ]
            };
            AchievementsSettings = new()
            {
                SupportedLibraries = [Id],
            };
        }

        public override async Task InitializeAsync(InitializeArgs args)
        {
            // Mitigate svg containing potential malicious external images/elements
            SvgDocument.ResolveExternalImages = ExternalType.None;
            SvgDocument.ResolveExternalElements = ExternalType.None;

            PlayniteApi = args.Api ?? throw new Exception("Failed to set playnite instance!");
            Loc.Api = args.Api ?? throw new Exception("Failed to set localization api instance!");

            PluginDataPath = PlayniteApi.UserDataDir;
            PluginDLLPath = args.PluginInstallDir;

            if (!Directory.Exists($"{PluginDataPath}/Platforms/"))
                Directory.CreateDirectory($"{PluginDataPath}/Platforms/");

            if (!Directory.Exists($"{PluginDataPath}/Games/"))
                Directory.CreateDirectory($"{PluginDataPath}/Games/");

            if (!Directory.Exists($"{PluginDataPath}/temp/"))
                Directory.CreateDirectory($"{PluginDataPath}/temp/");

            Logger = new();
            Logger.Initialize();
            Logger.Trace("Logger Initialized");

            GravitonNotify.Initialize(Instance, PlayniteApi, Logger);
            Logger.Trace("Notifications Initialized");

            CLIInstallDefinitions.Initialize();
            TitleIDInstallDefinitions.Initialize();

            await PlayniteApi.Library.Sources.AddAsync(new Source(Id, "Graviton"));
            Logger.Trace("Added Graviton to sources");

            await PlayniteApi.Library.WebLinkTypes.AddAsync(new WebLinkType("romm", "RomM"));
            await PlayniteApi.Library.WebLinkTypes.AddAsync(new WebLinkType("igdb", "IGDB"));
            await PlayniteApi.Library.WebLinkTypes.AddAsync(new WebLinkType("screenscraper", "Screenscraper"));
            await PlayniteApi.Library.WebLinkTypes.AddAsync(new WebLinkType("hasheous", "Hasheous"));
            await PlayniteApi.Library.WebLinkTypes.AddAsync(new WebLinkType("retroachievements", "RetroAchievements"));
            await PlayniteApi.Library.WebLinkTypes.AddAsync(new WebLinkType("howlongtobeat", "HowLongToBeat"));
            Logger.Trace("Added RomM, IGDB, Screenscraper, Hasheous, RetroAchievements, HowLongToBeat to WebLinkTypes");

            await PlayniteApi.Library.ExternalIdentifierTypes.AddAsync(new ExternalIdentifierType("romm", "RomM"));
            await PlayniteApi.Library.ExternalIdentifierTypes.AddAsync(new ExternalIdentifierType("igdb", "IGDB"));
            await PlayniteApi.Library.ExternalIdentifierTypes.AddAsync(new ExternalIdentifierType("screenscraper", "Screenscraper"));
            await PlayniteApi.Library.ExternalIdentifierTypes.AddAsync(new ExternalIdentifierType("hasheous", "Hasheous"));
            await PlayniteApi.Library.ExternalIdentifierTypes.AddAsync(new ExternalIdentifierType("retroachievements", "RetroAchievements"));
            await PlayniteApi.Library.ExternalIdentifierTypes.AddAsync(new ExternalIdentifierType("howlongtobeat", "HowLongToBeat"));
            Logger.Trace("Added RomM, IGDB, Screenscraper, Hasheous, RetroAchievements, HowLongToBeat to ExternalIdentifierTypes");

            await PlayniteApi.Library.CompletionStatuses.AddAsync(new CompletionStatus("never_playing", "Never Playing"));
            Logger.Trace("Added Never Playing to CompletionStatuses");

            RomMServer = new(Instance, PlayniteApi, Logger);
            Logger.Trace("Created RomMServer Controller");

            SettingsHandler = new(Instance, PlayniteApi, Logger, RomMServer);
            Logger.Trace("Created Settings Handler");

            ImportController = new(Instance, PlayniteApi, Logger, RomMServer);
            Logger.Trace("Created Import Controller");

            InstallController = new(Instance, PlayniteApi, Logger, RomMServer);
            Logger.Trace("Created Install Controller");

            SaveController = new(Instance, PlayniteApi, Logger, RomMServer);
            Logger.Trace("Created Save Controller");

            StatusController = new(Instance, PlayniteApi, Logger, RomMServer);
            Logger.Trace("Created Status Controller");

            Account = new(Instance, PlayniteApi, Logger, RomMServer);
            Logger.Trace("Created Account Controller");

            _downloadsViewModel = new();
            DownloadQueueController = new(Instance, PlayniteApi, Logger, RomMServer, _downloadsViewModel, maxConcurrent: 10);
            Logger.Trace("Created Download Queue Controller");
            _downloadsAppView = new();

            ImportedGames = new ConcurrentDictionary<string, RomMRomLocal>();
            List<int> FailedCacheAdds = new List<int>();

            foreach (var rompath in Directory.EnumerateFiles($"{PluginDataPath}/Games/"))
            {
                try
                {
                    var rom = JsonSerializer.Deserialize<RomMRomLocal>(File.ReadAllBytes(rompath));
                    if (rom != null)
                    {
                        if (!ImportedGames.TryAdd($"{rom.Id}", rom))
                            FailedCacheAdds.Add(rom.Id);

                        continue;
                    }

                    throw new Exception($"ROM / PlayniteID was null, failed to add {Path.GetFileName(rompath)}");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
            }

            if(FailedCacheAdds.Count > 0)
                Logger.Info($"Failed to add [{string.Join(", ", FailedCacheAdds)}] to Imported Games cache");

            Logger.Info("Finished Plugin Initialization");
        }

        public override async Task PostInitializationAsync(PostInitializationArgs args)
        {
            var result = await PlayniteApi.CallPluginAsync(new("Crow.Emunight", "get_api"));
            if (result?.Success == true && result.Value is Emunight.IEmunightAPI emunightApi)
            {
                EmunightAPI = emunightApi;
                Logger.Trace("Found Emunight plugin");
            }

            PlayController = new(Instance, PlayniteApi, Logger, EmunightAPI ?? throw new Exception("EmunightAPI not found"));
            Logger.Trace("Created Play Controller");

            Logger.Info("Finished Post Initialization");

            PluginInitialized = true;
        }

        public override async Task OnApplicationStartupAsync(OnApplicationStartupArgs args)
        {
            Settings = GravitonSettingsHandler.LoadSettings(PluginDataPath);
            Settings.ProfilePath = string.IsNullOrEmpty(Settings.ProfilePath) ? Path.Combine(PluginDLLPath, @"profile.png") : Settings.ProfilePath;
            Logger.Trace("Checked profile image file still exists");

            if (Settings.AccountState.LastAuthenticated != null)
            {
                if (Settings.UseBasicAuth)
                    RomMServer.ConfigureBasicAuth(Settings.UsernameNP, Settings.PasswordNP);
                else
                    RomMServer.ConfigureClientToken(Settings.ClientTokenNP);

                if (Account == null)
                    throw new Exception("Account hasn't been initailized, cannot continue!");

                // Check server exists
                var result = await Account.Heartbeat();
                if (result != null)
                {
                    Settings.AccountState.ServerVersion = result.Value.Version;
                    Logger.Trace($"Set server version to {result.Value.Version}");

                    bool loginSuccessful = await Account.Login(true);

                    if (loginSuccessful)
                    {
                        await InstallController!.RecoverDownloads();
                    }
                    else
                    {
                        Logger.Warn("Login failed; skipping download recovery.");
                    } 
                }
            } 
            else
            {
                Logger.Trace("Last Authenticated was null, skipping login");
            }

            Logger.Info("Completed Application Startup");
        }

        public override async Task OnApplicationShutdownAsync(OnApplicationShutdownArgs args)
        {
            if (RemoteInstallController != null)
                await RemoteInstallController.Disconnect();
        }

        public override Task<PluginSettingsHandler?> GetSettingsHandlerAsync(GetSettingsHandlerArgs args)
        {
            return Task.FromResult<PluginSettingsHandler?>(SettingsHandler);
        }

        public override Task<MetadataProvider?> GetMetadataProviderAsync(GetMetadataProviderArgs args)
        {
            return Task.FromResult<MetadataProvider?>(new GravitonMetadataProvider(Instance, PlayniteApi, Logger, RomMServer));
        }

        public override async Task OnGameCollectionChange(DataCollectionChangeArgs<Game> args)
        {
            if (!ImportInProgress && args.UpdatedItems?.Count > 0 && args.UpdatedItems.Any(x => x.OldData.LibraryId == Id))
            {
                Logger?.Trace($"Game Collection Change has updated items");

                await StatusController!.GameDataChanged(args.UpdatedItems.Where(x => x.OldData.LibraryId == Id));
            }

            if(args.RemovedItems?.Count > 0 && args.RemovedItems.Any(x => x.LibraryId == Id))
            {
                Logger?.Trace($"Game Collection Change has removed items");

                foreach (var removed in args.RemovedItems.Where(x => x.LibraryId == Id))
                {
                    ImportedGames.TryRemove(removed.LibraryGameId!, out var game);
                    if (File.Exists($"{PluginDataPath}/Games/{removed.LibraryGameId}.json"))
                        File.Delete($"{PluginDataPath}/Games/{removed.LibraryGameId}.json");
                }
            }
        }

        public override async Task<List<Game>> ImportGamesAsync(ImportGamesArgs args)
        {
            Logger?.Trace($"Started game import");
            ImportInProgress = true;
            return await ImportController!.Import(args) ?? throw new Exception("Import controller is null, cannot continue");
        }

        public override async Task<List<ImportableAchievements>> GetAchievementsAsync(GetAchievementsArgs args)
        {
            Logger?.Trace($"Started achievements fetch");
            return await StatusController!.GetAchievements(args);
        }

        public override async Task<GameEditSessionHandler?> GetGameEditHandlerAsync(GetGameEditHandlerArgs args)
        {
            if (args.Games.Count != 1 || args.Games[0].LibraryId != Id)
                return null;

            return new GravitonGameEditHandler(this, PlayniteApi, Logger, args.Games[0]);
        }

        public override async Task<List<InstallController>> GetInstallActionsAsync(GetInstallActionsArgs args)
        {
            if (args.Game.LibraryId == Id)
            {
                Logger?.Trace($"Started getting install actions");

                try
                {
                    if (!ImportedGames.ContainsKey(args.Game.LibraryGameId ?? ""))
                        throw new Exception(Loc.GetString("InstallGameIdNotFound", ("GameID", args.Game.LibraryGameId ?? "")));

                    var localROM = ImportedGames[args.Game.LibraryGameId!];
                    var mapping = Settings.Mappings.FirstOrDefault(x => x.MappingId == localROM.MappingID);

                    if (mapping == null)
                        throw new Exception(Loc.GetString("InstallMappingNotFound"));

                    var installInfo = GameInstallInfo.Build(localROM, mapping);
                    Logger?.Trace($"Created install info\n{JsonSerializer.Serialize(installInfo, new JsonSerializerOptions { WriteIndented = true })}");

                    return [new GravitonPlayniteInstallController(Instance, PlayniteApi, Logger!, RomMServer, args.Game, installInfo)];
                }
                catch (Exception ex)
                {
                    GravitonNotify.Notify("graviton.install.idmalformed", Loc.GetString("InstallFailed", ("Error", ex.Message)), GravitonSeverity.Error, ex);
                    return [];
                }
            }

            return [];
        }

        public override async Task<List<UninstallController>> GetUninstallActionsAsync(GetUninstallActionsArgs args)
        {
            if (args.Game.LibraryId == Id)
            {
                Logger?.Trace($"Started getting uninstall actions");

                try
                {
                    if (!ImportedGames.ContainsKey(args.Game.LibraryGameId ?? ""))
                        throw new Exception(Loc.GetString("InstallGameIdNotFound", ("GameID", args.Game.LibraryGameId ?? "")));

                    return [new GravitonUninstallController(Instance, PlayniteApi, Logger!, args.Game)];

                }
                catch (Exception ex)
                {
                    GravitonNotify.Notify("graviton.install.idmalformed", Loc.GetString("InstallFailed", ("Error", ex.Message)), GravitonSeverity.Error, ex);
                    return [];
                }
            }

            return [];
        }

        #region Game Session
        public override async Task<List<PlayController>> GetPlayActionsAsync(GetPlayActionsArgs args)
        {    
            if (args.Game.LibraryId == Id && ImportedGames.ContainsKey(args.Game.LibraryGameId!))
            {
                Logger?.Trace($"Started getting play actions");

                return await PlayController!.GetPlayActionsAsync(args);
            }

            return [];
        }

        public override async Task OnGameStartingAsync(OnGameStartingEventArgs args)
        {
            if (args.Game.LibraryId == Id && args.Game.LibraryGameId != null)
            {
                if (GameSessionHandler != null && GameSessionHandler.IsAGameRunning)
                {
                    GravitonNotify.Notify("graviton.sync.alreadyrunning", Loc.GetString("SyncAlreadyRunning"), GravitonSeverity.Info);
                    args.CancelStartup = true;
                    return;
                }

                GameSessionHandler = new GameSessionHandler(Instance, PlayniteApi, Logger);
                Logger?.Trace($"Created new game session");

                // Check to see if game starts then add the new session to the list
                await GameSessionHandler.GameStarting(args);

            }
        }

        public override async Task OnGameStartedAsync(OnGameStartedEventArgs args)
        {
            if (args.StartingArgs.Game.LibraryId == Id && args.StartingArgs.Game.LibraryGameId != null)
            {
                if(GameSessionHandler != null)
                    await GameSessionHandler.GameStarted(args);
            }
        }

        public override async Task OnGameStoppedAsync(OnGameStoppedEventArgs args)
        {
            if (args.StartingArgs.Game.LibraryId == Id)
            {
                if(GameSessionHandler != null)
                {
                    await GameSessionHandler.GameStopped(args);
                    Logger?.Trace($"Game session stopped");
                }
            }
        }

        public override async Task OnGameStartupCancelledAsync(OnGameStartupCancelledEventArgs args)
        {
            if (args.SessionArgs.Game.LibraryId == Id)
            {
                if (GameSessionHandler != null)
                {
                    await GameSessionHandler.GameCancelled(args);
                    Logger?.Trace($"Game session cancelled");
                }
            }
        }

        #endregion

        #region Views

        // Download tab
        public override ICollection<AppViewItemDescriptor>? GetAppViewItemDescriptors(GetAppViewItemDescriptorsArgs args)
        {
            return
            [
                new AppViewItemDescriptor(
                $"graviton.downloads",
                Loc.GetString("DownloadViewName"),
                // Icon used for sidebar item:
                (iconArgs) => UIIcon.FromBitmapFile($"{PluginDLLPath}/pluginiconBW.png"),
                // Icon used for when the view is activated:
                (iconArgs) => UIIcon.FromBitmapFile($"{PluginDLLPath}/pluginicon.png"))
            ];
        }
        public override AppViewItem? GetAppViewItem(GetAppViewItemsArgs args)
        {
            if (args.ViewId == $"graviton.downloads")
                return _downloadsAppView;

            return null;
        }

        public override ICollection<MenuItemDescriptor> GetAppMenuItemDescriptors(GetAppMenuItemDescriptorsArgs args)
        {
            return
            [
                new MenuItemDescriptor($"graviton.open.web", Loc.GetString("OpenRomMLibrary")),
                new MenuItemDescriptor($"graviton.open.account", Loc.GetString("OpenRomMProfile")),
                new MenuItemDescriptor($"graviton.manage.saves", Loc.GetString("ManageSaves")),
                new MenuItemDescriptor($"graviton.test.controller", "RomM Test Controller")
            ];
        }
        public override ICollection<MenuItemImpl>? GetAppMenuItems(GetAppMenuItemsArgs args)
        {
            if (args.ItemId.StartsWith("graviton."))
            {
                Logger?.Trace($"Getting app menu items");

                if (args.ItemId == "graviton.manage.saves")
                {
                    Logger?.Trace($"Returning 'ManageSaves' item");

                    return [new MenuItemImpl(Loc.GetString("ManageSaves"), (_) => 
                    {

                         var window = PlayniteApi.CreateWindow(new WindowCreationOptions
                         {
                             ShowMinimizeButton = false,
                             ShowMaximizeButton = true,
                             ShowCloseButton = true,
                             DefaultWidth = 1600,
                             DefaultHeight = 900
                         });

                        var manageSavesView = new SaveManagementWindow();

                        window.Title = Loc.GetString("SaveManagerTitle");
                        window.Content = manageSavesView;
                        window.Owner = PlayniteApi.GetLastActiveWindow();
                        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                        window.ShowDialog();

                    })];
                }

                if (string.IsNullOrEmpty(Settings.Host))
                {
                    GravitonNotify.Notify("graviton.get.appmenuitems", Loc.GetString("HostNotConfigured"), GravitonSeverity.Error);
                    return null;
                }

                if (!Uri.IsWellFormedUriString(Settings.Host, UriKind.Absolute))
                {
                    GravitonNotify.Notify("graviton.get.appmenuitems", Loc.GetString("HostInvalid"), GravitonSeverity.Error);
                    return null;
                }

                if (args.ItemId == "graviton.test.controller")
                {
                    Logger?.Trace($"Returning 'RomM Test Controller' item");
                    return [new MenuItemImpl("RomM Test Controller", async (_) =>
                    {
                        var webview = PlayniteApi.WebView.CreateView(new WebViewSettings()
                        {
                            JavaScriptEnabled = true,
                            WindowWidth = 1600,
                            WindowHeight = 900,
                            CacheEnabled = true,
                        });
                        
                        webview.WindowHost.Closed += (s, e) => webview.Dispose();

                        await webview.OpenAsync();
                        await webview.NavigateAndWaitAsync($"{Settings.Host}/controller-debug");

                    })];
                }

                if (args.ItemId == "graviton.open.web")
                {
                    Logger?.Trace($"Returning 'OpenRomMLibrary' item");
                    return [new MenuItemImpl(Loc.GetString("OpenRomMLibrary"), (_) => { Process.Start(new ProcessStartInfo(Settings.Host) { UseShellExecute = true })?.Dispose(); })];
                }

                if (Settings.AccountState.UserID < 0)
                {
                    GravitonNotify.Notify("graviton.open.library", Loc.GetString("NotAuthenticated"), GravitonSeverity.Error);
                    return null;
                }

                if (args.ItemId == "graviton.open.account")
                {
                    Logger?.Trace($"Returning 'OpenRomMProfile' item");
                    return [new MenuItemImpl(Loc.GetString("OpenRomMProfile"), (_) => { Process.Start(new ProcessStartInfo($"{Settings.Host}/user/{Settings.AccountState.UserID}") { UseShellExecute = true })?.Dispose(); })];
                }
            }    

            return null;
        }

        public override ICollection<MenuItemDescriptor> GetGameMenuItemDescriptors(GetGameMenuItemDescriptorsArgs args)
        {
            return
            [
                new MenuItemDescriptor("graviton.manage.saves", Loc.GetString("ManageSaves")),
                new MenuItemDescriptor("graviton.manage.savestates", Loc.GetString("ManageSaveStates"))
            ];
        }

        public override ICollection<MenuItemImpl>? GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            if (!args.Games.Any(x => x.LibraryId == Id))
                return null;

            Logger?.Trace($"Getting game menu items");

            if (args.ItemId == "graviton.manage.saves")
            {
                Logger?.Trace($"Returning 'ManageSaves' item");
                return [new MenuItemImpl(Loc.GetString("ManageSaves"), (_) =>
                    {
                        List<RomMRomLocal> roms = new();
                        foreach (var game in args.Games.Where(x => x.LibraryId == Id))
                        {
                            if(game.LibraryGameId != null && ImportedGames.ContainsKey(game.LibraryGameId))
                                roms.Add(ImportedGames[game.LibraryGameId]);
	                    }
                        Logger?.Trace($"Gathered games for manage saves window\n{string.Join(',', roms.Select(x => x.Name))}");

                         var window = PlayniteApi.CreateWindow(new WindowCreationOptions
                         {
                             ShowMinimizeButton = false,
                             ShowMaximizeButton = true,
                             ShowCloseButton = true,
                             DefaultWidth = 1600,
                             DefaultHeight = 900
                         });
                        Logger?.Trace($"Created window for save management");

                        var manageSavesView = new SaveManagementWindow(roms);
                        Logger?.Trace($"Created save management view");

                        window.Title = Loc.GetString("SaveManagerTitle");
                        window.Content = manageSavesView;
                        window.Owner = PlayniteApi.GetLastActiveWindow();
                        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

                        Logger?.Trace($"Showing save management window");
                        window.ShowDialog();
                        Logger?.Trace($"Closed save management window");

                    })];
            }

            return null;
        }

        #endregion

        public override Task OnGamepadButtonStateChangedAsync(OnGamepadButtonStateChangedArgs args)
        {
            return Task.CompletedTask;
        }

    }
}