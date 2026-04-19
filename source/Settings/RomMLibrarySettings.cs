using CommunityToolkit.Mvvm.ComponentModel;

using Playnite;

using RomMLibrary.Models;
using RomMLibrary.Models.RomM;
using RomMLibrary.Models.RomM.Platform;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace RomMLibrary.Settings
{
    public partial class RomMLibraryPluginSettings : ObservableObject
    {
        private string _host = "";
        [ObservableProperty] private string _serverVersion = "---";
        [ObservableProperty] private string _clientToken = "";
        [ObservableProperty] private bool _useBasicAuth = false;
        [ObservableProperty] private string _username = "";
        [ObservableProperty] private string _password = "";
        [ObservableProperty] private string _profilePath = "";
        [ObservableProperty] private string _user = "----";
        [ObservableProperty] private string _userType = "----";

        [ObservableProperty] private string _excludeGenres = "";
        [ObservableProperty] private bool _mergeRevisions = false;
        [ObservableProperty] private bool _skipMissingFiles = false;
        [ObservableProperty] private bool _keepDeletedGames= false;
        [ObservableProperty] private bool _scanGamesInFullScreen = false;

        [ObservableProperty] private bool _use7z = false;
        [ObservableProperty] private string _pathTo7z = "";      
        [ObservableProperty] private bool _notifyOnInstallComplete = false;

        [ObservableProperty] private bool _keepRomMSynced = false;      

        [ObservableProperty] private ObservableCollection<RomMPlatform> _romMPlatforms = new ObservableCollection<RomMPlatform>();
        [ObservableProperty] private ObservableCollection<EmulatorMapping> _mappings = new ObservableCollection<EmulatorMapping>();

        [ObservableProperty][property: JsonIgnore] private bool _connectionSuccess = false;
        [ObservableProperty][property: JsonIgnore] private bool _connectionFailed = false;
        [ObservableProperty][property: JsonIgnore] private bool _platformSynced = false;
        [ObservableProperty][property: JsonIgnore] private bool _platformSyncFailed = false;

        public string Host
        {
            get => _host;
            set
            {
                _host = value.Trim('/');
                OnPropertyChanged();
            }
        }

    }

    [INotifyPropertyChanged]
    public partial class RomMLibrarySettingsHandler : PluginSettingsHandler
    {
        private readonly RomMLibraryPlugin Plugin;
        private readonly IPlayniteApi PlayniteApi;
        private static readonly ILogger Logger = LogManager.GetLogger();
        private readonly Plugin.GetSettingsHandlerArgs SettingsArgs;

        public static RomMLibrarySettingsHandler? Instance { get; private set; }

        [ObservableProperty] private bool? isUserLoggedIn;
        [ObservableProperty] private RomMLibraryPluginSettings settings = new();

        public RomMLibrarySettingsHandler(RomMLibraryPlugin plugin, Plugin.GetSettingsHandlerArgs settingsArgs)
        {
            Plugin = plugin;
            PlayniteApi = RomMLibraryPlugin.PlayniteApi ?? throw new Exception("Playnite API is null cannot continue!");
            SettingsArgs = settingsArgs;
            Instance = this;
        }

        public override UserControl GetEditView(GetSettingsViewArgs args)
        {
            return new RomMLibrarySettingsView { DataContext = this };
        }

        public override async Task BeginEditAsync(BeginEditArgs args)
        {
            Settings = JsonSerializer.Deserialize<RomMLibraryPluginSettings>(JsonSerializer.Serialize(Plugin.Settings)) ?? throw new Exception("Failed to clone object via serialization");
            await Task.CompletedTask;
        }

        public override async Task CancelEditAsync(CancelEditArgs args)
        {
            await Task.CompletedTask;
        }

        public override async Task EndEditAsync(EndEditArgs args)
        {
            Plugin.Settings = Settings;
            SaveSettings(PlayniteApi.UserDataDir, Settings);
            await Task.CompletedTask;
        }

        public override async Task<ICollection<string>> VerifySettingsAsync(VerifySettingsArgs args)
        {
            await Task.CompletedTask;
            return [];
        }

        public static void SaveSettings(string dataDir, RomMLibraryPluginSettings settings)
        {
            var setFile = Path.Combine(dataDir, "settings.json");
            File.WriteAllText(setFile, JsonSerializer.Serialize<RomMLibraryPluginSettings>(settings));
        }

        public static RomMLibraryPluginSettings LoadSettings(string dataDir)
        {
            RomMLibraryPluginSettings? settings = null;
            var setFile = Path.Combine(dataDir, "settings.json");
            if (File.Exists(setFile))
            {
                try
                {
                    var file = File.ReadAllText(setFile);
                    settings = JsonSerializer.Deserialize<RomMLibraryPluginSettings>(file);                    
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to load plugin settings.");
                }
            }

            if (settings is null)
            {
                return new RomMLibraryPluginSettings();
            }
            else
            {
                return settings;
            }
        }

        public bool TestConnection()
        {
            try
            {
                if (string.IsNullOrEmpty(Settings.Host))
                {
                    throw new ArgumentException("host not set!");
                }

                if (Settings.UseBasicAuth)
                {
                    if (string.IsNullOrEmpty(Settings.Username) || string.IsNullOrEmpty(Settings.Password))
                    {
                        throw new ArgumentException("username or password not set!");
                    }

                    HttpClientSingleton.ConfigureBasicAuth(Settings.Username, Settings.Password);
                }
                else
                {
                    if (string.IsNullOrEmpty(Settings.ClientToken))
                    {
                        throw new ArgumentException("client token not set!");
                    }

                    HttpClientSingleton.ConfigureClientToken(Settings.ClientToken);
                }

                // Check server is present
                HttpResponseMessage response = HttpClientSingleton.Instance.GetAsync($"{Settings.Host}/api/heartbeat", HttpCompletionOption.ResponseContentRead, new System.Threading.CancellationToken()).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();

                Stream body = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();

                using (StreamReader reader = new StreamReader(body))
                {
                    var jsonResponse = JsonDocument.Parse(reader.ReadToEnd());
                    ServerInfo info = jsonResponse.RootElement.GetProperty("SYSTEM").Deserialize<ServerInfo>();

                    Settings.ServerVersion = info.Version;
                }

                // Get user info
                response = HttpClientSingleton.Instance.GetAsync($"{Settings.Host}/api/users/me", System.Net.Http.HttpCompletionOption.ResponseContentRead, new System.Threading.CancellationToken()).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();

                body = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                RomMUser userinfo;

                using (StreamReader reader = new StreamReader(body))
                {
                    var jsonResponse = JsonDocument.Parse(reader.ReadToEnd());
                    userinfo = jsonResponse.RootElement.Deserialize<RomMUser>() ?? throw new Exception("Unable to deserialize UserInfo!");
                }

                if (!string.IsNullOrEmpty(userinfo.IconPath))
                {
                    response = HttpClientSingleton.Instance.GetAsync($"{Settings.Host}/api/raw/assets/{userinfo.IconPath}", System.Net.Http.HttpCompletionOption.ResponseContentRead, new System.Threading.CancellationToken()).GetAwaiter().GetResult();
                    response.EnsureSuccessStatusCode();
                    var imagebytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    File.WriteAllBytes($"{PlayniteApi.UserDataDir}\\avatar.png", imagebytes);
                    Settings.ProfilePath = $"{PlayniteApi.UserDataDir}\\avatar.png";
                }
                else
                {
                    Settings.ProfilePath = Path.Combine(Plugin.PluginDLLPath, @"profile.png");
                }

                Settings.UserType = userinfo.Role;
                Settings.User = userinfo.Username;
                Settings.ConnectionFailed = false;
                Settings.ConnectionSuccess = true;
            }
            catch (Exception ex)
            {
                Settings.ConnectionFailed = true;
                Settings.ConnectionSuccess = false;
                Settings.ProfilePath = Path.Combine(Plugin.PluginDLLPath, @"profile.png");
                Settings.User = "----";
                Settings.UserType = "----";
                Settings.ServerVersion = "---";
                LogManager.GetLogger().Error($"Failed to read response! {ex}");
                PlayniteApi.Notifications.Add(new NotificationMessage(RomMLibraryPlugin.Id, $"{Loc.GetString("ServerPollFailed")}: {ex.Message}", NotificationSeverity.Error));
                return false;
            }
   
            return true;
        }
    }

    // Used to load profile image into cache so it can be changed while the application is running
    public class ImageCacheConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
            object parameter, System.Globalization.CultureInfo culture)
        {

            var path = (string)value;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();

            return image;

        }

        public object ConvertBack(object value, Type targetType,
            object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException("Not implemented.");
        }
    }
}