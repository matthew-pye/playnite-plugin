namespace Graviton.Models.Saves
{
    public partial class LocalSave
    {
        public string? SnapshotId { get; set; }

        public List<string> SourcePaths { get; set; } = new();        
        public SaveLayoutTypes Type { get; set; } = SaveLayoutTypes.Single;
        public SaveMatchStyle MatchedBy { get; set; } = SaveMatchStyle.None; 

        public long Size { get; set; }
        public DateTimeOffset LastWriteUtc { get; set; }
        public string? Hash { get; set; }                              


        public bool SessionOpen { get; set; }
        public string? SessionStartHash { get; set; }
    }

    public partial class LocalCardSave
    {
        public string? SnapshotId { get; set; }

        public List<int> LinkedROMs { get; set; } = new();

        public string? SourcePath { get; set; }
        public SaveLayoutTypes Type { get; set; } = SaveLayoutTypes.Card;
        public SaveMatchStyle MatchedBy { get; set; } = SaveMatchStyle.None;

        public long Size { get; set; }
        public DateTimeOffset LastWriteUtc { get; set; }
        public string? Hash { get; set; }


        public bool SessionOpen { get; set; }
        public string? SessionStartHash { get; set; }
    }
}