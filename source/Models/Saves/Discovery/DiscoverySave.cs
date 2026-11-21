using CommunityToolkit.Mvvm.ComponentModel;

using Graviton.Models.ROM;

namespace Graviton.Models.Saves.Discovery
{
    public enum DiscoveryStatus 
    { 
        Pending, 
        Accepted, 
        Ignored 
    }

    public class DiscoveryCandidate
    {
        public RomMRomLocal Rom { get; set; } = null!;
        public SaveMatchStyle MatchedStyle { get; set; }
    }

    public partial class DiscoveredSave : ObservableObject
    {
        public Guid MappingId { get; set; }
        public List<string> Paths { get; set; } = new();     // files or folder(s) on disk
        public SaveLayoutTypes Type { get; set; }
        public long SizeBytes { get; set; }
        public int FileCount { get; set; }
        public DateTime LatestFileWrite { get; set; } = DateTime.MinValue;
        public List<DiscoveryCandidate> Candidates { get; set; } = new();
        [ObservableProperty] private DiscoveryStatus _status;

    }
}