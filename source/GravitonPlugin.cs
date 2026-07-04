using Graviton.Import;
using Graviton.Install;
using Graviton.Install.Downloads;
using Graviton.Models.Notifications;
using Graviton.Models.RomM.Collection;
using Graviton.Models.RomM.Rom;
using Graviton.Saves;
using Graviton.Settings;
using Graviton.Status;

using Playnite;

using Svg;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;


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

        internal GravitonImportController? ImportController { get; private set; }
        internal StatusController? StatusController { get; private set; }
        internal DownloadQueueController? DownloadQueueController { get; private set; }
        internal SaveController? SaveController { get; private set; }

        internal ConcurrentDictionary<string, RomMRomLocal>? ImportedGames { get; private set; }

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
        internal RomMAccount? Account { get; private set; }

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

            GravitonNotify.Initialize(Instance, PlayniteApi, Logger);
            HttpClientSingleton.Initialize(Instance);

            SettingsHandler = new(Instance, PlayniteApi, Logger);
            ImportController = new(Instance, PlayniteApi, Logger);
            StatusController = new(Instance, PlayniteApi, Logger);
            SaveController = new(Instance, PlayniteApi, Logger);
            Account = new(Instance, PlayniteApi, Logger);

            ImportedGames = new ConcurrentDictionary<string, RomMRomLocal>();
            foreach (var rompath in Directory.EnumerateFiles($"{PluginDataPath}/Games/"))
            {
                try
                {
                    var rom = JsonSerializer.Deserialize<RomMRomLocal>(File.ReadAllBytes(rompath));
                    if (rom != null && !string.IsNullOrEmpty(rom!.PlayniteID))
                    {
                        ImportedGames.TryAdd(rom.PlayniteID!, rom);
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
            DownloadQueueController = new(_downloadsViewModel, maxConcurrent: 10);
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
                    HttpClientSingleton.ConfigureBasicAuth(Settings.UsernameNP, Settings.PasswordNP);
                }
                else
                {
                    HttpClientSingleton.ConfigureClientToken(Settings.ClientTokenNP);
                }

                if (Account == null)
                    throw new Exception("Account hasn't been initailized, cannot continue!");

                // Check server exists
                var result = await Account.Heartbeat();
                if (result != null)
                {
                    Settings.AccountState.ServerVersion = result.Value.Version;

                    if (await Account.SyncPlatforms())
                        Logger.Info(Loc.GetString("PlatformsSynced", [("PlaformCount", Settings.AccountState.RomMPlatforms.Count)]));
             
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
            return Task.FromResult<MetadataProvider?>(new GravitonMetadataProvider());
        }

        public override async Task OnGameCollectionChange(DataCollectionChangeArgs<Game> args)
        {

            if (args.UpdatedItems?.Count > 0 && args.UpdatedItems.Any(x => x.OldData.LibraryId == Id))
            {
                RomMCollection? favouriteCollection = null;
                if (Settings.KeepFavouritesSynced)
                {
                    favouriteCollection = await StatusController!.PullFavourites();
                    if (favouriteCollection == null)
                        return;
                }
                
                foreach (var updatedGame in args.UpdatedItems.Where(x => x.OldData.LibraryId == Id))
                {
                    foreach (var prop in updatedGame.ChangedProperties)
                    {
                        Logger.Info($"Game: {updatedGame.OldData.Name} | Prop: {prop}");
                    }

                    if (Settings.KeepStatusSynced && updatedGame.ChangedProperties.Contains(nameof(Game.CompletionStatusId)))
                    {
                        await StatusController!.UpdateStatus(updatedGame.NewData);
                    }

                    if (Settings.KeepFavouritesSynced && favouriteCollection != null && updatedGame.ChangedProperties.Contains(nameof(Game.Favorite)))
                    {
                        int romMID;
                        if (!int.TryParse(updatedGame.OldData.LibraryGameId?.Split(':')[0], out romMID))
                        {
                            GravitonNotify.Add(new GravitonNotification($"graviton.{updatedGame.OldData.LibraryGameId}.update.status.failed", $"{updatedGame.OldData.LibraryGameId}: {Loc.GetString("LibraryIdConvertFailed")}", GravitonSeverity.Error));
                            continue;
                        }

                        if (updatedGame.NewData.Favorite)
                            favouriteCollection.RomIDs.Add(romMID);
                        else
                            favouriteCollection.RomIDs.Remove(romMID);

                        favouriteCollection.HasBeenUpdated = true;
                    }
                }
                if (Settings.KeepFavouritesSynced && favouriteCollection != null && favouriteCollection.HasBeenUpdated)
                {
                    await StatusController!.UpdateFavorites(favouriteCollection);
                }
            }

            if(args.RemovedItems?.Count > 0 && args.RemovedItems.Any(x => x.LibraryId == Id))
            {
                foreach (var removed in args.RemovedItems)
                {
                    ImportedGames!.TryRemove(removed.LibraryGameId!, out _);
                    if (File.Exists($"{PluginDataPath}/Games/{removed.LibraryGameId!.Split(':')[1]}.json"))
                        File.Delete($"{PluginDataPath}/Games/{removed.LibraryGameId!.Split(':')[1]}.json");
                }
            }
        }

        public override Task<List<Game>> ImportGamesAsync(ImportGamesArgs args)
        {
            return ImportController?.Import(args) ?? throw new Exception("Import controller is null, cannot continue");
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
                    Mapping = Settings.Mappings.FirstOrDefault(x => x.MappingId == gameinfo.MappingID)
                };

                if (installInfo.Mapping == null)
                    throw new Exception("Couldn't find mapping!");
                
                return [new GravitonInstallController(args.Game, installInfo)];      
            }
            catch (Exception ex)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.install.idmalformed", $"Failed to install - {ex.Message}", GravitonSeverity.Error, ex));
                return [];
            }
        }
        
        public override Task OnGameStartingAsync(OnGameStartingEventArgs args)
        {
            if (args.Game.LibraryId == Id && args.Game.LibraryGameId != null)
            {
                if(Settings.SaveSyncEnabled)
                {
                    // Sync Saves
                }

                if (Settings.SaveStateSyncEnabled)
                {
                    // Sync Saves States
                }

                _ = Task.Run(async () => await StatusController?.StartActivityHeartbeat(args.Game.LibraryGameId)!);
            }

            return base.OnGameStartingAsync(args);
        }
        public override Task OnGameStoppedAsync(OnGameStoppedEventArgs args)
        {
            var stoppedTime = DateTime.UtcNow;

            if(args.StartingArgs.Game.LibraryId == Id)
            { 
                StatusController?.StopActivityHeartbeat();
                StatusController?.PushPlaySession(args.StartingArgs.Game.LibraryGameId!, stoppedTime, args.StoppedArgs.SessionLength*1000);

                if (Settings.SaveSyncEnabled)
                {
                    // Sync Saves
                }

                if (Settings.SaveStateSyncEnabled)
                {
                    // Sync Saves States
                }

                //if (args.StartingArgs.Game.ExternalIdentifiers != null && args.StartingArgs.Game.ExternalIdentifiers.Any(x => x.TypeId == "retroachievements"))
                //{
                //
                //}
            }

            return base.OnGameStoppedAsync(args);
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

                if (Settings.AccountState.UserID < 0)
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.open.library", Loc.GetString("NotAuthenticated"), GravitonSeverity.Error));
                    return null;
                }

                if (args.ItemId == "graviton.open.account")
                {
                    return [new MenuItemImpl("Open RomM profile", (_) => { Process.Start(new ProcessStartInfo($"{Settings.Host}/user/{Settings.AccountState.UserID}") { UseShellExecute = true })?.Dispose(); })];
                }
            }    

            return null;
        }

        public override ICollection<MenuItemDescriptor> GetGameMenuItemDescriptors(GetGameMenuItemDescriptorsArgs args)
        {
            return
            [
                new MenuItemDescriptor("graviton.manage.saves", "Manage Saves"),
                new MenuItemDescriptor("graviton.manage.savestates", "Manage Save States")
            ];
        }

        public override ICollection<MenuItemImpl>? GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            if (args.Games.Count != 1 || args.Games[0].LibraryId != Id)
                return null;

            if (args.ItemId == "graviton.manage.saves")
            {
                return [new MenuItemImpl("Manage Saves", (_) =>
                {
                    var mappingID = args.Games[0].ExternalIdentifiers?.FirstOrDefault(y => y.TypeId == "gravitonmappingid");
                    if(mappingID == null)
                        return;

                    var mapping = Settings.Mappings.FirstOrDefault(x => x.MappingId.ToString() == mappingID.IdValue);
                    if (mapping == null) 
                        return;

                    var tab = new Saves.SinglegameSaveTab();
                    tab.LoadForGame(args.Games[0], mapping);
                    new Saves.SaveManagerWindow("Manage Saves", tab) { Owner = System.Windows.Application.Current.MainWindow }.ShowDialog();
                })];
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

        #endregion

    }
}