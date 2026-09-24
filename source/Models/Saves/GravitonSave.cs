using CommunityToolkit.Mvvm.ComponentModel;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Graviton.Models.Saves
{
    public enum SaveConflictResolve
    {
        [Description("Ask")] Ask,
        [Description("Prefer Remote")] PreferRemote,
        [Description("Prefer Local")] PreferLocal
    }

    public enum SaveLayoutStyle
    {
        [Description("Single File")] SingleFile,
        [Description("Fixed Set")] FixedSet,
        [Description("Folder")] WholeFolder,
        [Description("Memory Card")] MemoryCard,
        [Description("Disabled")] Disabled
    }

    public enum SaveStatus
    {
        Synced,
        LocalNewer,
        RemoteNewer,
        Conflicted,
        ServerOnly,
        UntrackedLocal,
        TempRestored,
        MissingFiles,
        Unknown
    }

    public partial class GravitonSave : ObservableObject
    {
        [ObservableProperty] private bool _enabled = true;
        [ObservableProperty] private Guid _localID = Guid.NewGuid();
        [ObservableProperty] private ObservableCollection<string> _sourceFilePaths = new();
        [ObservableProperty] private string _filename = string.Empty;

        [ObservableProperty] private int _rOMID = -1;
        [ObservableProperty] private int _saveID = -1;
        [ObservableProperty] private string? _slot = "Autosave";

        [ObservableProperty] private SaveStatus _status;
        [JsonIgnore] private DateTime? _lastSyncedAt;
        [ObservableProperty] private string? _contentHash;
        [ObservableProperty] private long _fileSize;

        [ObservableProperty] private string? _lastSyncedContentHash;

        [ObservableProperty] private string? _serverHash;
        [ObservableProperty] private DateTime? _serverLastUpdatedAt;

        [ObservableProperty] private bool _isTempRestored = false;
        [ObservableProperty] private List<string> _missingFiles = new();

        public DateTime? LastSyncedAt
        {
            get => IsHistoric ? ServerLastUpdatedAt : _lastSyncedAt;
            set
            {
                _lastSyncedAt = value;
                OnPropertyChanged();
            }
        }

        #region UI Only
        [JsonIgnore] public string GameName { get; set; } = string.Empty;

        [JsonIgnore] public ObservableCollection<GravitonSave>? HistoricSaves { get; set; } = new();

        [ObservableProperty] [property:JsonIgnore] private bool _isExpanded = false;
        [ObservableProperty] [property:JsonIgnore] private bool _isCurrent = false;
        [ObservableProperty] [property:JsonIgnore] private bool _isHistoric = false;

        [JsonIgnore]
        public string LastSyncedUI
        { 
            get
            {
                if (Status == SaveStatus.ServerOnly && !IsHistoric)
                    return Playnite.Loc.GetString("LastSyncedNever");

                if (Status == SaveStatus.UntrackedLocal && !IsHistoric)
                    return Playnite.Loc.GetString("FoundOnDisk");

                if(LastSyncedAt == null)
                    return Playnite.Loc.GetString("SaveStatusUnknown");

                var difference = DateTime.Now - LastSyncedAt.Value;

                if (difference.TotalSeconds < 60)
                    return Playnite.Loc.GetString("TimeSecondsAgo", ("Count", difference.TotalSeconds.ToString("F0")));

                if (difference.TotalMinutes < 60)
                    return Playnite.Loc.GetString("TimeMinutesAgo", ("Count", difference.TotalMinutes.ToString("F0")));

                var daysAgo = (DateTime.Today - LastSyncedAt.Value.Date).Days;

                if (daysAgo == 0)
                    return Playnite.Loc.GetString("TimeHoursAgo", ("Count", difference.TotalHours.ToString("F0")));

                if (daysAgo == 1)
                    return Playnite.Loc.GetString("TimeYesterday", ("Time", LastSyncedAt.Value.ToLocalTime().ToString("t")));

                return Playnite.Loc.GetString("TimeDaysAgo", ("Count", daysAgo));

            }
        }

        [JsonIgnore]
        public string FileSizeString
        {
            get
            {
                if (FileSize <= 0)
                    return Playnite.Loc.GetString("Unknown");

                if (FileSize < 1000)
                {
                    return $"{FileSize} B";
                }
                else if (FileSize < 1000000)
                {
                    return $"{((float)FileSize / 1000).ToString("F1")}KB";
                }
                else
                {
                    return $"{((float)FileSize / 1000000).ToString("F1")}MB";
                }
            }
        }

        [JsonIgnore] public ObservableCollection<SaveDirectoryTree>? SaveDirectoryTrees { get; set; }

        #endregion
    }
}