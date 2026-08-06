using CommunityToolkit.Mvvm.ComponentModel;

using Emunight;

using Graviton.Models.RomM.Platform;
using Graviton.Models.Saves;

using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json.Serialization;


namespace Graviton.Models
{
    public partial class EmulatorMapping : ObservableObject
    {
        public static readonly string MappingPathToken = "{MappingSavePath}";

        private GravitonPlugin _plugin { get => GravitonPlugin.Instance; }

        [ObservableProperty] private Guid _mappingId;
        [ObservableProperty] private bool _enabled = true;
        [ObservableProperty] private bool _autoExtract = false;
        [ObservableProperty] private bool _useM3U = false;

        [JsonIgnore] private EmulatorBase? _emulator;
        [JsonIgnore] private ObservableCollection<EmulatorBase> _availableEmulators = new();
        [ObservableProperty][NotifyPropertyChangedFor(nameof(IsSetup))] private string? _emulatorId;

        [JsonIgnore] private RomMPlatform? _emulatedPlatform;
        [JsonIgnore] private ObservableCollection<RomMPlatform> _availablePlatforms = new();     
        [ObservableProperty] private int _romMPlatformId = -1;

        [ObservableProperty][NotifyPropertyChangedFor(nameof(IsSetup))] private string _destinationPath = "";

        [ObservableProperty] private SaveLayoutStyle _findSaveLayout = SaveLayoutStyle.Disabled;
        [ObservableProperty] private string _findSaveFileExtensions = "";
        [ObservableProperty] private string _savePath = "";
        [ObservableProperty] private bool _extractArchivedSaves = true;
        [ObservableProperty] private string _saveStatePath = "";
              
        [ObservableProperty] [property: JsonIgnore] private bool _isSelected = false;

        [property: JsonIgnore] public bool IsSetup => !string.IsNullOrEmpty(EmulatorId) && 
                                                      RomMPlatformId >= 0 && 
                                                      !string.IsNullOrEmpty(DestinationPath);

        [JsonConstructor]
        public EmulatorMapping() {}

        public EmulatorMapping(ObservableCollection<EmulatorBase> emulators, ObservableCollection<RomMPlatform> romMPlatforms)
        {
            MappingId = Guid.NewGuid();
            AvailablePlatforms = romMPlatforms;
            AvailableEmulators = emulators;
        }

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

                OnPropertyChanged();
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


        public string GetDescriptionLines()
        {
            return $"{nameof(EmulatorId)}: {EmulatorId}\n" +
                   $"{nameof(RomMPlatformId)}: {RomMPlatformId}\n" +
                   $"{nameof(RomMPlatform)}: {RomMPlatform?.Name ?? "<Unknown>"}\n" +
                   $"{nameof(DestinationPath)}: {DestinationPath ?? "<Unknown>"}\n" +
                   $"{nameof(DestinationPathResolved)}: {DestinationPathResolved ?? "<Unknown>"}\n" +
                   $"{nameof(Emulator)}: {Emulator?.Name ?? "<Unknown>"}\n" +
                   $"Emulator Install Path: {Emulator?.InstallDir ?? "<Unknown>"}\n";
        }
    }
}
