using CommunityToolkit.Mvvm.ComponentModel;

using Graviton.Models;
using Graviton.Models.Notifications;
using Graviton.Models.RomM.Platform;
using Graviton.Models.RomM.Saves;

using Playnite;

using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Graviton.Settings
{

    public partial class GravitonAccountState : ObservableObject
    {
        [ObservableProperty] private string _serverVersion = "---";
        [ObservableProperty] private string _user = "----";
        [ObservableProperty] private string _userType = "----";
        [ObservableProperty] private int _userID = -1;
        [ObservableProperty] private string _deviceID = "";

        [ObservableProperty] private DateTime? _lastAuthenticated;
        [ObservableProperty] [property:JsonIgnore] private HttpStatusCode? _authenticateFailed;

        [ObservableProperty] private ObservableCollection<RomMPlatform> _romMPlatforms = new ObservableCollection<RomMPlatform>();
    }

    public partial class GravitonPluginSettings : ObservableObject
    {
        private string _host = "";
       
        [ObservableProperty] private string _clientToken = "";
        [ObservableProperty] private bool _useBasicAuth = false;
        [ObservableProperty] private string _username = "";
        [ObservableProperty] private string _password = "";
        [ObservableProperty] private ObservableCollection<CustomHTTPHeader> _customHeaders = new ObservableCollection<CustomHTTPHeader>();

        private string _profilePath = ""; 
 
        [ObservableProperty] private string _excludeGenres = "";
        [ObservableProperty] private bool _mergeRevisions = false;
        [ObservableProperty] private bool _skipMissingFiles = false;
        [ObservableProperty] private bool _keepDeletedGames = false;
        [ObservableProperty] private bool _importGamePatchesAsSiblings = false;

        [ObservableProperty] private bool _use7z = false;
        [ObservableProperty] private string _pathTo7z = "";      

        [ObservableProperty] private bool _keepStatusSynced = false;      
        [ObservableProperty] private bool _keepFavouritesSynced = false;      
        [ObservableProperty] private bool _keepPrivateNotesSynced = false;      
        [ObservableProperty] private bool _keepPublicNotesSynced = false;      


        [ObservableProperty] private bool _saveSyncEnabled = true;      
        [ObservableProperty] private bool _saveStateSyncEnabled = false;      
        [ObservableProperty] private bool _downloadSaveOnLaunch = true;      
        [ObservableProperty] private bool _uploadSaveOnFinished = true;      
        [ObservableProperty] private SaveConflictStyle _saveConflictStyle = SaveConflictStyle.Ask;
        [ObservableProperty] private bool _autoCleanupSaves = false;
        [ObservableProperty] private int _autoCleanupSavesLimit = 10;

        [ObservableProperty] private ObservableCollection<EmulatorMapping> _mappings = new ObservableCollection<EmulatorMapping>();

        public GravitonAccountState AccountState { get; init; } = new();

        public string Host
        {
            get => _host;
            set
            {

                if(Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    _host = value.TrimEnd('/');
                }
                else
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.host.invalid.scheme", Loc.GetString("InvaildScheme"), GravitonSeverity.Error));
                    _host = string.Empty;
                }
                              
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
        [JsonIgnore]
        public string UsernameNP
        {
            get => string.IsNullOrEmpty(Username) ? string.Empty : UnProtect(Username);
            set
            {
                Username = string.IsNullOrEmpty(value) ? string.Empty : Protect(value);
                OnPropertyChanged();
            }
        }
        [JsonIgnore]
        public string PasswordNP
        {
            get => string.IsNullOrEmpty(Password) ? string.Empty : UnProtect(Password);
            set
            {
                Password = string.IsNullOrEmpty(value) ? string.Empty : Protect(value);
                OnPropertyChanged();
            }
        }
        [JsonIgnore]
        public string ClientTokenNP
        {
            get => string.IsNullOrEmpty(ClientToken) ? string.Empty : UnProtect(ClientToken);
            set
            {
                ClientToken = string.IsNullOrEmpty(value) ? string.Empty : Protect(value);
                OnPropertyChanged();
            }
        }

        public GravitonPluginSettings Clone()
        {
            return new GravitonPluginSettings()
            { 
                Host = this.Host,
                ClientTokenNP = this.ClientTokenNP,
                UseBasicAuth = this.UseBasicAuth,
                UsernameNP = this.UsernameNP,
                PasswordNP = this.PasswordNP,

                ProfilePath = this.ProfilePath,
                
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

                SaveSyncEnabled = this.SaveSyncEnabled,
                SaveStateSyncEnabled = this.SaveStateSyncEnabled,
                DownloadSaveOnLaunch = this.DownloadSaveOnLaunch,
                UploadSaveOnFinished = this.UploadSaveOnFinished,
                SaveConflictStyle = this.SaveConflictStyle,
                AutoCleanupSaves = this.AutoCleanupSaves,
                AutoCleanupSavesLimit = this.AutoCleanupSavesLimit,

                Mappings = new(this.Mappings),

                AccountState = this.AccountState
            };
        }

        private string Protect(string PlainText)
        {
            if (string.IsNullOrEmpty(PlainText)) 
                return string.Empty;

            byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(PlainText), Encoding.UTF8.GetBytes(GravitonPlugin.ExternalIdType), DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        private string UnProtect(string ProtectedText)
        {
            if (string.IsNullOrEmpty(ProtectedText)) 
                return string.Empty;

            try
            {
                byte[] decrypted = ProtectedData.Unprotect(Convert.FromBase64String(ProtectedText),
                    Encoding.UTF8.GetBytes(GravitonPlugin.ExternalIdType), DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                GravitonPlugin.Logger?.Error($"Failed to decrypt credential: {ex.Message}");
                return string.Empty;
            }
        }
    }
}