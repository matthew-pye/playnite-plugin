using CommunityToolkit.Mvvm.ComponentModel;

using Graviton.Models;
using Graviton.Models.RomM.Platform;

using Playnite;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Graviton.Settings
{
    public partial class GravitonPluginSettings : ObservableObject
    {
        private string _host = "";
        [ObservableProperty] private string _serverVersion = "---";
        [ObservableProperty] private string _clientToken = "";
        [ObservableProperty] private bool _useBasicAuth = false;
        [ObservableProperty] private string _username = "";
        [ObservableProperty] private string _password = "";
        private string _profilePath = "";
        [ObservableProperty] private string _user = "----";
        [ObservableProperty] private string _userType = "----";
        [ObservableProperty] private int _userID = -1;
        [ObservableProperty] private string _deviceID = "";

        [ObservableProperty] private DateTime? _lastAuthenticated;

        [ObservableProperty] private string _excludeGenres = "";
        [ObservableProperty] private bool _mergeRevisions = false;
        [ObservableProperty] private bool _skipMissingFiles = false;
        [ObservableProperty] private bool _keepDeletedGames= false;

        [ObservableProperty] private bool _use7z = false;
        [ObservableProperty] private string _pathTo7z = "";      

        [ObservableProperty] private bool _keepStatusSynced = false;      
        [ObservableProperty] private bool _keepFavouritesSynced = false;      
        [ObservableProperty] private bool _keepPrivateNotesSynced = false;      
        [ObservableProperty] private bool _keepPublicNotesSynced = false;      

        [ObservableProperty] private ObservableCollection<RomMPlatform> _romMPlatforms = new ObservableCollection<RomMPlatform>();
        [ObservableProperty] private ObservableCollection<EmulatorMapping> _mappings = new ObservableCollection<EmulatorMapping>();

        public string Host
        {
            get => _host;
            set
            {
                _host = value.TrimEnd('/');
                OnPropertyChanged();
            }
        }

        public string ProfilePath
        {
            get => _profilePath;
            set
            {
                if(value.Contains('?'))
                    value = value.Substring(0, value.IndexOf('?'));
                
                _profilePath = string.IsNullOrEmpty(value) ? Path.Combine(GravitonPlugin.Instance.PluginDLLPath, @"profile.png") : value + "?" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                OnPropertyChanged(nameof(ProfilePath));
            }
        }

        public GravitonPluginSettings Clone()
        {
            return new GravitonPluginSettings()
            { 
                Host = this.Host,
                ClientToken = this.ClientToken,
                UseBasicAuth = this.UseBasicAuth,
                Username = this.Username,
                Password = this.Password,

                ProfilePath = this.ProfilePath,
                User = this.User,
                UserType = this.UserType,
                UserID = this.UserID,
                DeviceID = this.DeviceID,

                LastAuthenticated = this.LastAuthenticated,
                
                ExcludeGenres = this.ExcludeGenres,
                MergeRevisions = this.MergeRevisions,
                SkipMissingFiles = this.SkipMissingFiles,
                KeepDeletedGames = this.KeepDeletedGames,

                Use7z = this.Use7z,
                PathTo7z = this.PathTo7z,

                KeepStatusSynced = this.KeepStatusSynced,
                KeepFavouritesSynced = this.KeepFavouritesSynced,
                KeepPrivateNotesSynced = this.KeepPrivateNotesSynced,
                KeepPublicNotesSynced = this.KeepPublicNotesSynced,

                RomMPlatforms = this.RomMPlatforms,
                Mappings = this.Mappings

            };
        }

    }

    [INotifyPropertyChanged]
    public partial class GravitonSettingsHandler : PluginSettingsHandler
    {
        public static GravitonSettingsHandler? Instance { get; private set; }

        private static GravitonPlugin _plugin { get => GravitonPlugin.Instance; }
        private static IPlayniteApi PlayniteApi { get => GravitonPlugin.PlayniteApi ?? throw new Exception("Playnite API is null cannot continue!"); }
        private static readonly ILogger Logger = LogManager.GetLogger();

        [ObservableProperty] private GravitonPluginSettings settings = new();
        public bool InEditingMode { get; private set; }

        public GravitonSettingsHandler()
        {
            Instance = this;
        }

        public override UserControl GetEditView(GetSettingsViewArgs args)
        {
            return new GravitonSettingsView { DataContext = this };
        }

        public override async Task BeginEditAsync(BeginEditArgs args)
        {
            Settings = _plugin.Settings.Clone();
            InEditingMode = true;
            await Task.CompletedTask;
        }

        public override async Task CancelEditAsync(CancelEditArgs args)
        {
            InEditingMode = false;
            await Task.CompletedTask;
        }

        public override async Task EndEditAsync(EndEditArgs args)
        {
            _plugin.Settings = Settings;
            SaveSettings(PlayniteApi.UserDataDir, Settings);
            InEditingMode = false;
            await Task.CompletedTask;
        }

        public override async Task<ICollection<string>> VerifySettingsAsync(VerifySettingsArgs args)
        {
            await Task.CompletedTask;
            return [];
        }

        public static void SaveSettings(string dataDir, GravitonPluginSettings settings)
        {
            var setFile = Path.Combine(dataDir, "settings.json");
            File.WriteAllText(setFile, JsonSerializer.Serialize<GravitonPluginSettings>(settings));
        }

        public static GravitonPluginSettings LoadSettings(string dataDir)
        {
            GravitonPluginSettings? settings = null;
            var setFile = Path.Combine(dataDir, "settings.json");
            if (File.Exists(setFile))
            {
                try
                {
                    var file = File.ReadAllText(setFile);
                    settings = JsonSerializer.Deserialize<GravitonPluginSettings>(file);
                    foreach (var mapping in settings!.Mappings)
                    {
                        mapping.AvailablePlatforms = settings.RomMPlatforms;  
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to load plugin settings.");
                }
            }

            if (settings is null)
            {
                return new GravitonPluginSettings();
            }
            else
            {
                return settings;
            }
        }       
    }

    // Used to load profile image into cache so it can be changed while the application is running
    public class ImageCacheConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
            object parameter, System.Globalization.CultureInfo culture)
        {

            var path = (string)value;
            path = path.Contains('?') ? path.Substring(0, path.IndexOf('?')) : path;

            if (string.IsNullOrEmpty(path))
                return new BitmapImage();

            if (!File.Exists(path))
                return new BitmapImage();

            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
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