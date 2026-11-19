namespace Graviton.Models.Saves
{
    public partial class PendingPush
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public int RomId { get; set; }
        public string ChannelId { get; set; } = "";
        public int? ExpectedCurrentId { get; set; }
        public int? ParentSnapshotId { get; set; }
        public string? StagedPath { get; set; }
        public string ContentHash { get; set; } = "";
        public string Shape { get; set; } = "SINGLE";  
        public string? ScreenshotPath { get; set; }
        public int Attempts { get; set; }
        public string? LastError { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}