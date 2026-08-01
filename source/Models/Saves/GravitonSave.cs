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
        TempRestored,
        MissingFiles,
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
        [ObservableProperty] private DateTime _lastSyncedAt;
        [ObservableProperty] private string? _contentHash;
        [ObservableProperty] private long _fileSize;

        [ObservableProperty] private string? _lastSyncedContentHash;

        [ObservableProperty] private string? _serverHash;
        [ObservableProperty] private DateTime? _serverLastUpdatedAt;

        [ObservableProperty] private bool _isTempRestored = false;
        [ObservableProperty] private List<string> _missingFiles = new();

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
                    return "Never synced";

                if (Status == SaveStatus.UntrackedLocal && !IsHistoric)
                    return "Found on disk";

                var difference = DateTime.Now - LastSyncedAt;

                if (difference.TotalSeconds < 60)
                    return $"{difference.TotalSeconds:F0}s ago";

                if (difference.TotalMinutes < 60)
                    return $"{difference.TotalMinutes:F0}m ago";

                var daysAgo = (DateTime.Today - LastSyncedAt.Date).Days;

                if (daysAgo == 0)
                    return $"{difference.TotalHours:F0}h ago";

                if (daysAgo == 1)
                    return $"Yesterday, {LastSyncedAt.ToLocalTime():t}";

                return $"{daysAgo}d ago";

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