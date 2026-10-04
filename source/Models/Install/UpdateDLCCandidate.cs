using CommunityToolkit.Mvvm.ComponentModel;

using Graviton.Install;

using System.Text.Json.Serialization;

namespace Graviton.Models.Install
{
    public partial class UpdateDLCCandidate : ObservableObject
    {
        public string Name { get; set; }
        public string FileName { get; set; }
        public string? Category { get; set; }

        public IReadOnlyCollection<int> FileIDs { get; set; } = [];
        [ObservableProperty] public long _size;

        public List<int> InstalledFileIDs { get; set; } = [];
        public List<string> InstalledTopPaths { get; set; } = [];

        [ObservableProperty] public InstallStatus _status = InstallStatus.NotInstalled;
        [ObservableProperty] public long _installedSize;

        public InstallStyles PreviousInstallStyle { get; set; }


        public string? CandidateRoot { get; set; }
        public string? SingleFileRelativePath { get; set; }
        [JsonIgnore] public bool IsSelected { get; set; } = false;

        public UpdateDLCCandidate(string name, string filename, string category, IReadOnlyCollection<int> fileIDs, string? candidateRoot = null, string? relativePath = null, long size = 0)
        {
            Name = name;
            FileName = filename;
            Category = category;
            FileIDs = fileIDs;
            CandidateRoot = candidateRoot;
            SingleFileRelativePath = relativePath;
            Size = size;
        }

        [JsonIgnore]
        public string FileSizeUI
        {
            get
            {
                if (Size <= 0)
                    return Playnite.Loc.GetString("Unknown");

                if (Size < 1000)
                {
                    return $"{Size} B";
                }
                else if (Size < 1000000)
                {
                    return $"{((float)Size / 1000).ToString("F1")}KB";
                }
                else if (Size < 1000000000)
                {
                    return $"{((float)Size / 1000000).ToString("F1")}MB";
                }
                else
                {
                    return $"{((float)Size / 1000000000).ToString("F1")}GB";
                }
            }
        }

        [JsonIgnore]
        public string InstalledSizeUI
        {
            get
            {
                if (InstalledSize <= 0)
                    return Playnite.Loc.GetString("NotInstalled");

                if (InstalledSize < 1000)
                {
                    return $"{InstalledSize} B";
                }
                else if (InstalledSize < 1000000)
                {
                    return $"{((float)InstalledSize / 1000).ToString("F1")}KB";
                }
                else if (InstalledSize < 1000000000)
                {
                    return $"{((float)InstalledSize / 1000000).ToString("F1")}MB";
                }
                else
                {
                    return $"{((float)InstalledSize / 1000000000).ToString("F1")}GB";
                }
            }
        }

    }
}
