using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;

using Graviton.Models.RomM.Platform;

using Playnite;
using System.Collections.ObjectModel;
using System.IO;


namespace Graviton.Models
{
    public partial class EmulatorMapping : ObservableObject
    {
        private GravitonPlugin _plugin { get => GravitonPlugin.Instance; }

        [ObservableProperty] private Guid _mappingId;
        [ObservableProperty] [property: JsonIgnore] private string _mappingName = "Unknown Mapping";
        [ObservableProperty] private bool _enabled = true;
        [ObservableProperty] private bool _autoExtract = false;
        [ObservableProperty] private bool _useM3U = false;
        //[JsonIgnore] private Emulator _emulator;
        [JsonIgnore] private Guid? _emulatorId;
        //[JsonIgnore] private EmulatorProfile _emulatorProfile;
        //[JsonIgnore] private IEnumerable<EmulatorProfile> _availableProfiles;
        [JsonIgnore] private string? _emulatorProfileId;
        [JsonIgnore] private RomMPlatform? _emulatedPlatform = null;
        [JsonIgnore] private ObservableCollection<RomMPlatform> _availablePlatforms = new ObservableCollection<RomMPlatform>();
        [ObservableProperty] private int _romMPlatformId = -1;
        [ObservableProperty] private string _destinationPath = "";

        [ObservableProperty] [property: JsonIgnore] private bool _isSelected = false;


        [JsonConstructor]
        public EmulatorMapping() {}

        public EmulatorMapping(ObservableCollection<RomMPlatform> romMPlatforms)
        {
            MappingId = Guid.NewGuid();
            AvailablePlatforms = romMPlatforms;
        }

        //[JsonIgnore]
        //public Emulator Emulator
        //{
        //    get => _emulator;
        //    set 
        //    {
        //        if (value != null)
        //        {
        //            _emulator = value;
        //            _emulatorId = value.Id;
        //            AvailableProfiles = Emulator?.SelectableProfiles;
        //            RomMPlatform = new RomMPlatform();
        //            MappingName = value.Name;
        //            OnPropertyChanged();
        //        } 
        //    }
        //}
        public Guid? EmulatorId
        {
            get => _emulatorId;
            set
            {
                _emulatorId = value;
                //Emulator = SettingsViewModel.Instance.PlayniteAPI.Database.Emulators.FirstOrDefault(x => x.Id == _emulatorId);
                OnPropertyChanged();
            }
        }

        //[JsonIgnore]
        //public EmulatorProfile EmulatorProfile
        //{
        //    get => _emulatorProfile;
        //    set 
        //    {
        //        if (value != null)
        //        {
        //            _emulatorProfile = value;
        //            _emulatorProfileId = value.Id;
        //
        //            if (Emulator != null)
        //            {
        //                var name = Emulator.Name;
        //                if (EmulatorProfile != null  && EmulatorProfile.Name != "")
        //                    name += " - " + EmulatorProfile.Name;
        //                if (RomMPlatform != null && !string.IsNullOrEmpty(RomMPlatform.Name))
        //                    name += " - " + RomMPlatform.Name;
        //
        //                MappingName = name;
        //            }
        //        }
        //        OnPropertyChanged(); 
        //    }
        //}
        public string? EmulatorProfileId
        {
            get => _emulatorProfileId;
            set
            {
                _emulatorProfileId = value;
                //EmulatorProfile = Emulator?.SelectableProfiles.FirstOrDefault(x => x.Id == _emulatorProfileId);
                OnPropertyChanged();
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
                    MappingName = "Emulator test value - " + "Profile test value - " + value.Name;

                    //if(Emulator != null)
                    //{
                    //    var name = Emulator.Name;
                    //    if (EmulatorProfile != null && EmulatorProfile.Name != "")
                    //        name += " - " + EmulatorProfile.Name;
                    //    if (RomMPlatform != null && !string.IsNullOrEmpty(RomMPlatform.Name))
                    //        name += " - " + RomMPlatform.Name;
                    //
                    //    MappingName = name;
                    //}

                }
                else
                {
                    RomMPlatformId = -1;
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(PlatformIcon));
            }
        }


        //[JsonIgnore]
        //public static IEnumerable<Emulator> AvailableEmulators => SettingsViewModel.Instance.PlayniteAPI.Database.Emulators?.OrderBy(x => x.Name) ?? Enumerable.Empty<Emulator>();
        //[JsonIgnore]
        //public IEnumerable<EmulatorProfile> AvailableProfiles
        //{
        //    get => _availableProfiles;
        //    set
        //    {
        //        _availableProfiles = value;
        //        OnPropertyChanged();
        //    }
        //}    
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
                    RomMPlatform = AvailablePlatforms?.FirstOrDefault(x => x.Id == RomMPlatformId) ?? null;
                }
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
                IPlayniteApi playnite = GravitonPlugin.PlayniteApi ?? throw new Exception("");
                return playnite.AppInfo.ApplicationDirectory;
                //return playnite.Paths.IsPortable ? DestinationPath?.Replace(playnite.ExpandableVariables.PlayniteDirectory, playnite.AppInfo.ApplicationDirectory) : DestinationPath;
            }
        }

        //[JsonIgnore] public string EmulatorBasePath => Emulator?.InstallDir;

        //[JsonIgnore]
        //public string EmulatorBasePathResolved
        //{
        //    get
        //    {
        //        var playnite = SettingsViewModel.Instance.PlayniteAPI;
        //        var ret = Emulator?.InstallDir;
        //        if (playnite.Paths.IsPortable)
        //        {
        //            ret = ret?.Replace(ExpandableVariables.PlayniteDirectory, playnite.Paths.ApplicationPath);
        //        }
        //        return ret;
        //    }
        //}


        public string GetDescriptionLines()
        {

            return $"{nameof(EmulatorId)}: {EmulatorId}\n" +
                   $"{nameof(EmulatorProfileId)}: {EmulatorProfileId ?? "<Unknown>"}\n" +
                   $"{nameof(RomMPlatformId)}: {RomMPlatformId}\n" +
                   $"{nameof(RomMPlatform)}*: {RomMPlatform?.Name ?? "<Unknown>"}\n" +
                   $"{nameof(DestinationPath)}: {DestinationPath ?? "<Unknown>"}\n" +
                   $"{nameof(DestinationPathResolved)}*: {DestinationPathResolved ?? "<Unknown>"}";


            //yield return $"{nameof(Emulator)}*: {Emulator?.Name ?? "<Unknown>"}";
            //yield return $"{nameof(EmulatorProfile)}*: {EmulatorProfile?.Name ?? "<Unknown>"}";      
            //yield return $"{nameof(EmulatorBasePathResolved)}*: {EmulatorBasePathResolved ?? "<Unknown>"}";
        }
    }
}
