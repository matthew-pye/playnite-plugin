using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;

using RomMLibrary.Models.RomM.Platform;


namespace RomMLibrary.Models
{
    public partial class EmulatorMapping : ObservableObject
    {
        
        [ObservableProperty] private Guid _mappingId;
        [ObservableProperty] private string _mappingName = "";
        [ObservableProperty] private bool _enabled = true;
        [ObservableProperty] private bool _autoExtract = false;
        [ObservableProperty] private bool _useM3U = false;
        //[JsonIgnore] private Emulator _emulator;
        [JsonIgnore] private Guid? _emulatorId;
        //[JsonIgnore] private EmulatorProfile _emulatorProfile;
        //[JsonIgnore] private IEnumerable<EmulatorProfile> _availableProfiles;
        [JsonIgnore] public string? _emulatorProfileId;
        [JsonIgnore] private RomMPlatform? _emulatedPlatform;
        [JsonIgnore] private IEnumerable<RomMPlatform>? _availablePlatforms;
        [ObservableProperty] public int _romMPlatformId = -1;
        [ObservableProperty] private string _destinationPath = "";

        public EmulatorMapping(List<RomMPlatform> romMPlatforms)
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
                RomMPlatformId = -1;
                if(value != null)
                {
                    RomMPlatformId = value.Id;

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
                OnPropertyChanged();
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
        public IEnumerable<RomMPlatform>? AvailablePlatforms
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

        //[JsonIgnore]
        //public string DestinationPathResolved
        //{
        //    get
        //    {
        //        var playnite = SettingsViewModel.Instance.PlayniteAPI;
        //        return playnite.Paths.IsPortable ? DestinationPath?.Replace(ExpandableVariables.PlayniteDirectory, playnite.Paths.ApplicationPath) : DestinationPath;
        //    }
        //}

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


        //public IEnumerable<string> GetDescriptionLines()
        //{
        //    yield return $"{nameof(_emulatorId)}: {_emulatorId}";
        //    yield return $"{nameof(Emulator)}*: {Emulator?.Name ?? "<Unknown>"}";
        //    yield return $"{nameof(EmulatorProfileId)}: {EmulatorProfileId ?? "<Unknown>"}";
        //    yield return $"{nameof(EmulatorProfile)}*: {EmulatorProfile?.Name ?? "<Unknown>"}";
        //    yield return $"{nameof(PlatformId)}: {PlatformId}";
        //    yield return $"{nameof(Platform)}*: {Platform?.Name ?? "<Unknown>"}";
        //    yield return $"{nameof(DestinationPath)}: {DestinationPath ?? "<Unknown>"}";
        //    yield return $"{nameof(DestinationPathResolved)}*: {DestinationPathResolved ?? "<Unknown>"}";
        //    yield return $"{nameof(EmulatorBasePathResolved)}*: {EmulatorBasePathResolved ?? "<Unknown>"}";
        //}
    }
}
