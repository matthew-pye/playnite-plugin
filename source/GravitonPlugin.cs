using Graviton.Import;
using Graviton.Install;
using Graviton.Install.Downloads;
using Graviton.Models.Notifications;
using Graviton.Models.RomM.Rom;
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
using System.Text.RegularExpressions;
using System.Windows;


namespace Graviton
{
    public class GravitonPlugin : Plugin
    {
        public static readonly string Id = "Matthew-Pye.Graviton";
        public static readonly string ExternalIdType = "graviton";
        public static readonly string ExternalIdName = "Graviton (RomM Library)";
        public static readonly Version Version = new Version(0,2,0);

        internal string PluginDLLPath { get; private set; } = "";
        internal string PluginDataPath { get; private set; } = "";

        internal static GravitonPlugin Instance { get; private set; } = null!;
        internal static IPlayniteApi PlayniteApi { get; private set; } = null!;
        internal static ILogger Logger { get; private set; } = null!;
        internal static RomMServer RomMServer { get; private set; } = null!;

        internal GravitonImportController? ImportController { get; private set; }
        internal SaveController? SaveController { get; private set; }
        internal List<GameSessionHandler> GameSessionHandlers { get; private set; } = new();
        internal StatusController? StatusController { get; private set; }
        internal DownloadQueueController? DownloadQueueController { get; private set; }

        internal ConcurrentDictionary<string, RomMRomLocal> ImportedGames { get; private set; } = new();

        internal GravitonPluginSettings Settings 
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

        private GravitonPluginSettings _settings = new();

        internal GravitonSettingsHandler? SettingsHandler { get; set; }
        internal RomMAuthentication? Account { get; private set; }

        private RomMDownloadsAppViewItem? _downloadsAppView { get; set; }
        private DownloadQueueViewModel? _downloadsViewModel;

        internal static Regex SHA1Regex = new Regex("^[a-fA-F0-9]{40}$");

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
            // Mitigate svg containing potential malicious external images/elements
            SvgDocument.ResolveExternalImages = ExternalType.None;
            SvgDocument.ResolveExternalElements = ExternalType.None;

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
            await PlayniteApi.Library.ExternalIdentifierTypes.AddAsync(new ExternalIdentifierType("gravitonmappingid", "MappingID"));
            await PlayniteApi.Library.ExternalIdentifierTypes.AddAsync(new ExternalIdentifierType("igdb", "IGDB"));
            await PlayniteApi.Library.ExternalIdentifierTypes.AddAsync(new ExternalIdentifierType("screenscraper", "Screenscraper"));
            await PlayniteApi.Library.ExternalIdentifierTypes.AddAsync(new ExternalIdentifierType("hasheous", "Hasheous"));
            await PlayniteApi.Library.ExternalIdentifierTypes.AddAsync(new ExternalIdentifierType("retroachievements", "RetroAchievements"));
            await PlayniteApi.Library.ExternalIdentifierTypes.AddAsync(new ExternalIdentifierType("howlongtobeat", "HowLongToBeat"));

            await PlayniteApi.Library.CompletionStatuses.AddAsync(new CompletionStatus("never_playing", "Never Playing"));

            PluginDataPath = PlayniteApi.UserDataDir;
            PluginDLLPath = args.PluginInstallDir;

            if (!Directory.Exists($"{PluginDataPath}/Platforms/"))
                Directory.CreateDirectory($"{PluginDataPath}/Platforms/");

            if(!Directory.Exists($"{PluginDataPath}/Games/"))
                Directory.CreateDirectory($"{PluginDataPath}/Games/");

            if (!Directory.Exists($"{PluginDataPath}/temp/"))
                Directory.CreateDirectory($"{PluginDataPath}/temp/");

            GravitonNotify.Initialize(Instance, PlayniteApi, Logger);
            
            RomMServer = new(Instance);
            SettingsHandler = new(Instance, PlayniteApi, Logger, RomMServer);
            ImportController = new(Instance, PlayniteApi, Logger, RomMServer);
            SaveController = new(Instance, PlayniteApi, Logger, RomMServer);
            StatusController = new(Instance, PlayniteApi, Logger, RomMServer);
            
            Account = new(Instance, PlayniteApi, Logger, RomMServer);

            ImportedGames = new ConcurrentDictionary<string, RomMRomLocal>();
            
            foreach (var rompath in Directory.EnumerateFiles($"{PluginDataPath}/Games/"))
            {
                try
                {
                    var rom = JsonSerializer.Deserialize<RomMRomLocal>(File.ReadAllBytes(rompath));
                    if (rom != null)
                    {
                        ImportedGames.TryAdd($"{rom.Id}:{rom.SHA1}", rom);
                        continue;
                    }

                    throw new Exception($"ROM / PlayniteID was null, failed to add {Path.GetFileName(rompath)}");
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex);
                }
            }

            _downloadsViewModel = new();
            DownloadQueueController = new(Instance, PlayniteApi, Logger, RomMServer, _downloadsViewModel, maxConcurrent: 10);
            _downloadsAppView = new();

        }

        public override async Task OnApplicationStartupAsync(OnApplicationStartupArgs args)
        {
            Settings = GravitonSettingsHandler.LoadSettings(PluginDataPath);
            Settings.ProfilePath = string.IsNullOrEmpty(Settings.ProfilePath) ? Path.Combine(PluginDLLPath, @"profile.png") : Settings.ProfilePath;

            if (Settings.AccountState.LastAuthenticated != null)
            {
                if (Settings.UseBasicAuth)
                {
                    RomMServer.ConfigureBasicAuth(Settings.UsernameNP, Settings.PasswordNP);
                }
                else
                {
                    RomMServer.ConfigureClientToken(Settings.ClientTokenNP);
                }

                if (Account == null)
                    throw new Exception("Account hasn't been initailized, cannot continue!");

                // Check server exists
                var result = await Account.Heartbeat();
                if (result != null)
                {
                    Settings.AccountState.ServerVersion = result.Value.Version;

                    if (await Account.SyncPlatforms())
                        Logger.Info(Loc.GetString("PlatformsSynced", [("PlatformCount", Settings.AccountState.RomMPlatforms.Count)]));
             
                    await Account.SyncUserData();
                }      
            } 

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

            if (args.UpdatedItems?.Count > 0 && args.UpdatedItems.Any(x => x.OldData.LibraryId == Id))
            {
                await StatusController!.GameDataChanged(args.UpdatedItems.Where(x => x.OldData.LibraryId == Id));
            }

            if(args.RemovedItems?.Count > 0 && args.RemovedItems.Any(x => x.LibraryId == Id))
            {
                foreach (var removed in args.RemovedItems)
                {
                    ImportedGames.TryRemove(removed.LibraryGameId!, out _);
                    if (File.Exists($"{PluginDataPath}/Games/{removed.LibraryGameId!.Split(':')[1]}.json"))
                        File.Delete($"{PluginDataPath}/Games/{removed.LibraryGameId!.Split(':')[1]}.json");
                }
            }
        }

        public override async Task<List<Game>> ImportGamesAsync(ImportGamesArgs args)
        {
            return await ImportController!.Import(args) ?? throw new Exception("Import controller is null, cannot continue");
        }

        public override async Task<List<InstallController>> GetInstallActionsAsync(GetInstallActionsArgs args)
        {
            var idParts = args.Game.LibraryGameId?.Split(':');

            try
            {
                if (idParts == null || idParts.Length != 2 || !SHA1Regex.IsMatch(idParts[1]))
                    throw new Exception("GameID is malformed!");

                if (!File.Exists($"{PluginDataPath}/Games/{idParts[1]}.json"))
                    throw new Exception("Game info file doesn't exist!");


                var gameinfo = JsonSerializer.Deserialize<RomMRomLocal>(File.ReadAllText($"{PluginDataPath}/Games/{idParts[1]}.json"));

                if (gameinfo == null || gameinfo.FileName == null || gameinfo.DownloadURL == null)
                    throw new Exception("Game info is corrupted!");

                GameInstallInfo installInfo = new()
                {
                    Id = gameinfo.Id,
                    FileName = gameinfo.FileName,
                    HasMultipleFiles = gameinfo.HasMultipleFiles,
                    DownloadURL = gameinfo.DownloadURL,
                    PatchFileID = gameinfo.PatchFileId,
                    Mapping = Settings.Mappings.FirstOrDefault(x => x.MappingId == gameinfo.MappingID)
                };

                if (installInfo.Mapping == null)
                    throw new Exception("Couldn't find mapping!");
                
                return [new GravitonInstallController(args.Game, installInfo)];      
            }
            catch (Exception ex)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.install.idmalformed", Loc.GetString("InstallFailed", ("Error", ex.Message)), GravitonSeverity.Error, ex));
                return [];
            }
        }
        
        public override async Task OnGameStartingAsync(OnGameStartingEventArgs args)
        {
            if (args.Game.LibraryId == Id && args.Game.LibraryGameId != null)
            {
                var newSession = new GameSessionHandler(Instance, PlayniteApi, Logger);
                await newSession.GameStarting(args.Game.LibraryGameId);
                GameSessionHandlers.Add(newSession);
            }
        }

        public override async Task OnGameStartedAsync(OnGameStartedEventArgs args)
        {
            if (args.StartingArgs.Game.LibraryId == Id && args.StartingArgs.Game.LibraryGameId != null)
            {
                var gameSession = GameSessionHandlers.FirstOrDefault(x => x.GameID == args.StartingArgs.Game.LibraryGameId);
                _ = gameSession?.GameStarted(args.StartedArgs.StartedProcessId, args.StartingArgs.Game.LibraryGameId);
            }
        }

        public override async Task OnGameStoppedAsync(OnGameStoppedEventArgs args)
        {
            if (args.StartingArgs.Game.LibraryId == Id)
            {
                var gameSession = GameSessionHandlers.FirstOrDefault(x => x.GameID == args.StartingArgs.Game.LibraryGameId);
                if(gameSession != null)
                {
                    await gameSession.GameStopped(args.StartingArgs.Game.LibraryGameId, args.StoppedArgs.SessionLength);
                    GameSessionHandlers.Remove(gameSession);
                }
            }
        }

        public override Task OnGamepadButtonStateChangedAsync(OnGamepadButtonStateChangedArgs args)
        {
            return Task.CompletedTask;
        }

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
                if (args.ItemId == "graviton.manage.saves")
                {
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
                    GravitonNotify.Add(new GravitonNotification("graviton.open.library", Loc.GetString("HostNotSet"), GravitonSeverity.Error));
                    return null;
                }

                if (!Uri.IsWellFormedUriString(Settings.Host, UriKind.Absolute))
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.open.library", Loc.GetString("HostInvaild"), GravitonSeverity.Error));
                    return null;
                }

                if (args.ItemId == "graviton.test.controller")
                {
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
                    return [new MenuItemImpl(Loc.GetString("OpenRomMLibrary"), (_) => { Process.Start(new ProcessStartInfo(Settings.Host) { UseShellExecute = true })?.Dispose(); })];
                }

                if (Settings.AccountState.UserID < 0)
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.open.library", Loc.GetString("NotAuthenticated"), GravitonSeverity.Error));
                    return null;
                }

                if (args.ItemId == "graviton.open.account")
                {
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

            if (args.ItemId == "graviton.manage.saves")
            {
                return [new MenuItemImpl(Loc.GetString("ManageSaves"), (_) =>
                    {
                        List<RomMRomLocal> roms = new();
                        foreach (var game in args.Games.Where(x => x.LibraryId == Id))
                        {
                            if(game.LibraryGameId != null && ImportedGames.ContainsKey(game.LibraryGameId))
                                roms.Add(ImportedGames[game.LibraryGameId]);
	                    }

                         var window = PlayniteApi.CreateWindow(new WindowCreationOptions
                         {
                             ShowMinimizeButton = false,
                             ShowMaximizeButton = true,
                             ShowCloseButton = true,
                             DefaultWidth = 1600,
                             DefaultHeight = 900
                         });

                        var manageSavesView = new SaveManagementWindow(roms);

                        window.Title = Loc.GetString("SaveManagerTitle");
                        window.Content = manageSavesView;
                        window.Owner = PlayniteApi.GetLastActiveWindow();
                        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                        window.ShowDialog();

                    })];
            }

            return null;
        }

        #endregion

    }
}