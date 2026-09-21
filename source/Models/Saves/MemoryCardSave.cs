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
                    return "Never synced";

                if (Status == SaveStatus.UntrackedLocal && !IsHistoric)
                    return "Found on disk";

                if(LastSyncedAt == null)
                    return "Unknown";

                var difference = DateTime.Now - LastSyncedAt.Value;

                if (difference.TotalSeconds < 60)
                    return $"{difference.TotalSeconds:F0}s ago";

                if (difference.TotalMinutes < 60)
                    return $"{difference.TotalMinutes:F0}m ago";

                var daysAgo = (DateTime.Today - LastSyncedAt.Value.Date).Days;

                if (daysAgo == 0)
                    return $"{difference.TotalHours:F0}h ago";

                if (daysAgo == 1)
                    return $"Yesterday, {LastSyncedAt.Value.ToLocalTime():t}";

                return $"{daysAgo}d ago";

            }
        }

        [JsonIgnore]
        public string FileSizeString
        {
            get
            {
                if (FileSize <= 0)
                    return "Unknown";

                if (FileSize < 1000)
                {
                    return $"{FileSize} Bytes";
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