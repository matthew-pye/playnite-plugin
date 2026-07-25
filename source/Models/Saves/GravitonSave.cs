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
        Unknown
    }

    public partial class GravitonSave : ObservableObject
    {
        [ObservableProperty] private bool _enabled = true;
        [ObservableProperty] private Guid _localID = Guid.NewGuid();
        [ObservableProperty] private List<string> _sourceFilePaths = new();
        [ObservableProperty] private string _filename = string.Empty;

        [ObservableProperty] private int _rOMID = -1;
        [ObservableProperty] private int _saveID = -1;
        [ObservableProperty] private string? _slot = "Autosave";

        [ObservableProperty] private SaveStatus _status;
        [ObservableProperty] private string? _contentHash;
        [ObservableProperty] private string? _serverHash;
        [ObservableProperty] private long _fileSize;
        [ObservableProperty] private DateTime _lastSyncedAt;
        [ObservableProperty] private DateTime? _serverLastUpdatedAt;

        #region UI Only
        [JsonIgnore] public string GameName { get; set; } = string.Empty;

        [JsonIgnore] public List<GravitonSave> HistoricSaves = new();

        [ObservableProperty] [property:JsonIgnore] private bool _isExpanded = false;

        [JsonIgnore]
        public string LastSyncedString
        { 
            get
            {
                if (Status == SaveStatus.ServerOnly)
                    return "Never synced";

                if (Status == SaveStatus.UntrackedLocal)
                    return "Found on disk";

                var now = DateTime.Now;
                var differance = now.ToUniversalTime() - LastSyncedAt;

                if(differance.TotalSeconds < 59)
                {
                    return $"{differance.TotalSeconds}s ago";
                }
                else if (differance.TotalMinutes < 59)
                {
                    return $"{differance.TotalMinutes}m ago";
                }
                else if (differance.TotalHours < 12)
                {
                    return $"{differance.TotalHours}h ago";
                }
                else if(DateTime.Today - LastSyncedAt.Date == TimeSpan.FromDays(1))
                {
                    var localSyncTime = LastSyncedAt.ToLocalTime();
                    return $"Yesterday, {localSyncTime.ToString("t")}";
                }
                else
                {
                    return $"{differance.TotalDays}d ago";
                }
 
            }
        }

        [JsonIgnore]
        public string FileSizeString
        {
            get
            {
                if (Status == SaveStatus.ServerOnly || Status == SaveStatus.UntrackedLocal)
                    return "";

                if(FileSize < 1000)
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