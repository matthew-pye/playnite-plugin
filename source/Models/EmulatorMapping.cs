using CommunityToolkit.Mvvm.ComponentModel;

using Emunight;

using Graviton.Models.Install;
using Graviton.Models.RomM.Platform;
using Graviton.Models.Saves;

using Playnite;

using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json.Serialization;


namespace Graviton.Models
{

    public partial class EmulatorMapping : ObservableObject
    {
        public static readonly string SavePathToken = "{MappingSavePath}";
        public static readonly string InstallPathToken = "{MappingInstallPath}";

        private GravitonPlugin _plugin { get => GravitonPlugin.Instance; }

        [JsonConstructor] public EmulatorMapping() { }

        public EmulatorMapping(ObservableCollection<EmulatorBase> emulators, ObservableCollection<RomMPlatform> romMPlatforms)
        {
            MappingId = Guid.NewGuid();
            AvailablePlatforms = romMPlatforms;
            AvailableEmulators = emulators;
        }

        [ObservableProperty] private Guid _mappingId;
        [ObservableProperty] private bool _enabled = true;
        [ObservableProperty] private bool _autoExtract = false;
        [ObservableProperty] private bool _preferM3U = false;

        // Emulator Config
        [JsonIgnore] private EmulatorBase? _emulator;
        [JsonIgnore] private ObservableCollection<EmulatorBase> _availableEmulators = new();
        [ObservableProperty][NotifyPropertyChangedFor(nameof(IsSetup))] private string? _emulatorId;

        [JsonIgnore] private EmulatorProfile? _emulatorProfile;
        [ObservableProperty][NotifyPropertyChangedFor(nameof(IsSetup))] private string? _emulatorProfileId;

        [JsonIgnore] private RomMPlatform? _emulatedPlatform;
        [JsonIgnore] private ObservableCollection<RomMPlatform> _availablePlatforms = new();     
        [ObservableProperty] private int _romMPlatformId = -1;

        // Base Game
        [ObservableProperty][NotifyPropertyChangedFor(nameof(IsSetup))] private string _destinationPath = "";

        // Updates
        [ObservableProperty] private InstallStyle _updateInstallStyle = InstallStyle.None;
        [ObservableProperty] private InstallMode _updateInstallMode = InstallMode.SelectOne;
        [ObservableProperty] private string? _updateInstallPath;
        [ObservableProperty] private string? _updateCLIDefinitionID;
        [ObservableProperty] private ObservableCollection<DynamicArgument> _updateCLIUserArgs = new();

        // DLCs
        [ObservableProperty] private InstallStyle _DLCInstallStyle = InstallStyle.None;
        [ObservableProperty] private InstallMode _DLCInstallMode = InstallMode.All;
        [ObservableProperty] private string? _DLCInstallPath;
        [ObservableProperty] private string? _DLCCLIDefinitionID;
        [ObservableProperty] private ObservableCollection<DynamicArgument> _DLCCLIUserArgs = new();

        // Saves
        [ObservableProperty] private SaveLayoutStyle _findSaveLayout = SaveLayoutStyle.Disabled;
        [ObservableProperty] private string _findSaveFileExtensions = "";
        [ObservableProperty] private string _savePath = "";
        [ObservableProperty] private bool _extractArchivedSaves = true;
        [ObservableProperty] private string _saveStatePath = "";
        [ObservableProperty] private MemoryCardSave? _memoryCardSave = null;


        // UI
        [ObservableProperty] [property: JsonIgnore] private bool _isSelected = false;

        [property: JsonIgnore] public bool IsSetup => !string.IsNullOrEmpty(EmulatorId) && 
                                                      (IsImportedEmulator ? !string.IsNullOrEmpty(EmulatorProfileId) : true) &&
                                                      RomMPlatformId >= 0 && 
                                                      !string.IsNullOrEmpty(DestinationPath);

        [JsonIgnore]
        public ObservableCollection<EmulatorBase> AvailableEmulators
        {
            get => _availableEmulators;
            set { 
                _availableEmulators = value; 
                OnPropertyChanged();

                if (_availableEmulators != null && !string.IsNullOrEmpty(EmulatorId))
                {
                    Emulator = _availableEmulators.FirstOrDefault(x => x.Id == EmulatorId);

                    if (IsImportedEmulator && !string.IsNullOrEmpty(EmulatorProfileId))
                    {
                        Profile = AvailableProfiles.FirstOrDefault(x => x.Id == EmulatorProfileId);
                    }
                }

            }
        }

        [JsonIgnore]
        public EmulatorBase? Emulator
        {
            get => _emulator;
            set
            {
                _emulator = value;
                EmulatorId = value?.Id;

                if (value is not ImportedEmulator imported || imported.ProfileSettings?.Any(p => p.ProfileId == EmulatorProfileId) != true)
                {
                    EmulatorProfileId = null;
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(AvailableProfiles));
                OnPropertyChanged(nameof(IsImportedEmulator));
                OnPropertyChanged(nameof(IsCustomEmulator));
                OnPropertyChanged(nameof(IsSetup));
            }
        }

        [JsonIgnore]
        public bool IsImportedEmulator => Emulator is ImportedEmulator;
        [JsonIgnore]
        public bool IsCustomEmulator => Emulator is CustomEmulator;

        [JsonIgnore]
        public IEnumerable<EmulatorProfile> AvailableProfiles
        { 
            get
            {
                if(!IsImportedEmulator)
                    return Enumerable.Empty<EmulatorProfile>();

                var emunightEmulator = _plugin.EmunightAPI?.GetKnownEmulators().FirstOrDefault(x => x.Id == (Emulator as ImportedEmulator)!.EmulatorId);
                if (emunightEmulator == null)
                    return Enumerable.Empty<EmulatorProfile>();

                return emunightEmulator.Profiles;
            }
        }


        [JsonIgnore]
        public EmulatorProfile? Profile
        {
            get => _emulatorProfile;
            set
            {
                _emulatorProfile = value;
                EmulatorProfileId = value?.Id;

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSetup));
            }
        }


        [JsonIgnore]
        public ObservableCollection<RomMPlatform> AvailablePlatforms
        {
            get => _availablePlatforms;
            set
            {
                _availablePlatforms = value;
                OnPropertyChanged();

                if (_availablePlatforms != null && RomMPlatformId != -1)
                {
                    RomMPlatform = AvailablePlatforms?.FirstOrDefault(x => x.Id == RomMPlatformId);
                }
            }
        }

        [JsonIgnore]
        public RomMPlatform? RomMPlatform
        {
            get => _emulatedPlatform;
            set
            {
                _emulatedPlatform = value;             
                if(value != null)
                {
                    RomMPlatformId = value.Id;
                }
                else
                {
                    RomMPlatformId = -1;
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(PlatformIcon));
                OnPropertyChanged(nameof(IsSetup));
            }
        }

        [JsonIgnore]
        public string? PlatformIcon
        {
            get => (RomMPlatformId != -1 && File.Exists($"{_plugin.PluginDataPath}/Platforms/{RomMPlatform?.Slug}.png")) ?          
                    $"{_plugin.PluginDataPath}/Platforms/{RomMPlatform?.Slug}.png" : 
                    $"{_plugin.PluginDLLPath}/platform.png";
        }

        [JsonIgnore]
        public string DestinationPathResolved
        {
            get
            {
                //IPlayniteApi playnite = GravitonPlugin.PlayniteApi ?? throw new Exception("PlayniteApi is not initialised");
                //return playnite.Paths.IsPortable ? DestinationPath?.Replace(playnite.ExpandableVariables.PlayniteDirectory, playnite.AppInfo.ApplicationDirectory) : DestinationPath;
                return DestinationPath;
            }
        }

        [JsonIgnore] ObservableCollection<CLIInstallDefinition> CLIDefinitions => CLIInstallDefinitions.All.ToObservableCollection();

        [JsonIgnore]
        public CLIInstallDefinition? UpdateCLIDefinition
        {
            get => CLIInstallDefinitions.Get(UpdateCLIDefinitionID ?? "");
            set
            {
                UpdateCLIDefinitionID = value?.ID;
                OnPropertyChanged();
            }
        }

        [JsonIgnore]
        public CLIInstallDefinition? DLCCLIDefinition
        {
            get => CLIInstallDefinitions.Get(DLCCLIDefinitionID ?? "");
            set
            {
                DLCCLIDefinitionID = value?.ID;
                OnPropertyChanged();
            }
        }

    }
}
