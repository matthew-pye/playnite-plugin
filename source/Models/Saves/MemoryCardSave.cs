using CommunityToolkit.Mvvm.ComponentModel;

using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Graviton.Models.Saves
{

    public partial class MemoryCardSave : ObservableObject
    {
        [ObservableProperty] private bool _enabled = true;
        [ObservableProperty] private Guid _EmulatorMappingID;
        [ObservableProperty] private ObservableCollection<string> _sourceFilePaths = new();
        [ObservableProperty] private string _filename = string.Empty;

        [ObservableProperty] private int _memoryCardID = -1;

        [ObservableProperty] private SaveStatus _status;
        [JsonIgnore] private DateTime? _lastSyncedAt;
        [ObservableProperty] private DateTime? _createdAt;
        [ObservableProperty] private string? _contentHash;
        [ObservableProperty] private long _fileSize;


        public DateTime? LastSyncedAt
        {
            get => _lastSyncedAt;
            set
            {
                _lastSyncedAt = value;
                OnPropertyChanged();
            }
        }

        #region UI Only
        [JsonIgnore] public string GameName { get; set; } = string.Empty;

        [JsonIgnore] public ObservableCollection<MemoryCardSave>? HistoricSaves { get; set; } = new();

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