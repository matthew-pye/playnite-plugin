using Newtonsoft.Json;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace RomM.Models.RomM.Rom
{
    public enum SaveSyncStatus
    { 
        NotEnabled,
        RemoteNewer,
        LocalNewer,
        InSync,
        NotUploaded
    }

    public class RomMSave : ObservableObject
    {
        [JsonIgnore] private int _id;
        [JsonIgnore] private int _romID;
        [JsonIgnore] private string _fileName;
        [JsonIgnore] private long _fileSize;
        [JsonIgnore] private bool _missingFromFS;
        [JsonIgnore] private DateTime _lastUpdated;
        [JsonIgnore] private string _saveFolder;
        [JsonIgnore] private bool _syncEnabled = false;
        [JsonIgnore] private SaveSyncStatus _isInSync = SaveSyncStatus.NotEnabled;
        [JsonIgnore] private string _gameName;


        [JsonProperty("id")]
        public int ID
        {
            get => _id;
            set
            {
                _id = value;
                OnPropertyChanged();
            }
        }

        [JsonProperty("rom_id")]
        public int ROMID
        {
            get => _romID;
            set
            {
                _romID = value;
                OnPropertyChanged();
            }
        }

        [JsonProperty("file_name")]
        public string FileName
        {
            get => _fileName;
            set
            {
                _fileName = value;
                OnPropertyChanged();
            }
        }

        [JsonProperty("file_size_bytes")]
        public long FileSize
        {
            get => _fileSize;
            set
            {
                _fileSize = value;
                OnPropertyChanged();
            }
        }

        [JsonProperty("missing_from_fs")]
        public bool MissingFromFS
        {
            get => _missingFromFS;
            set
            {
                _missingFromFS = value;
                OnPropertyChanged();
            }
        }

        [JsonProperty("updated_at")]
        public DateTime LastUpdated
        {
            get => _lastUpdated;
            set
            {
                // Round date time to the second
                _lastUpdated = value.AddTicks(-(value.Ticks % TimeSpan.TicksPerSecond));
                OnPropertyChanged();
            }
        }

        public string SaveFolder
        {
            get => _saveFolder;
            set
            {
                if(value == null)
                    _saveFolder = "";
                else
                    _saveFolder = value.TrimEnd('/');

                OnPropertyChanged();
            }
        }
        public bool SyncEnabled
        {
            get => _syncEnabled;
            set
            {
                _syncEnabled = value;
                OnPropertyChanged();
            }
        }
        public SaveSyncStatus IsInSync
        {
            get => _isInSync;
            set
            {
                _isInSync = value;
                OnPropertyChanged();
            }
        }

        [JsonIgnore] public string GameName
        {
            get => _gameName;
            set
            {
                _gameName = value;
                OnPropertyChanged();
            }
        }
        [JsonIgnore] public string LastUpdatedString
        {
            get => $"(Last Updated: {LastUpdated})";
            set
            {
                OnPropertyChanged();
            }
        }
    }
    public class PossibleSave
    {
        public FileInfo File { get; set; }
        public RomMRevision Game { get; set; }
        public bool IsSelected { get; set; }

    }
}
