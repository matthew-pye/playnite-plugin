using CommunityToolkit.Mvvm.ComponentModel;

namespace Graviton.Models.Saves
{
    public enum ChannelStatus
    {
        Unknown,
        PendingCreate,
        Available,
        Deleted,
        Detached
    }

    public partial class PlaythroughLocal : ObservableObject
    {
        public string ChannelId { get; set; } = Guid.NewGuid().ToString();
        [ObservableProperty] private string _name = "default";         
        [ObservableProperty] private bool _isActive;

        public ChannelStatus Status { get; set; } = ChannelStatus.PendingCreate;

        public int? HeldSnapshotId { get; set; }           
        public string? HeldHash { get; set; }               
        public int? UnresolvedBranchId { get; set; }       

        public int? LastSeenRemoteCurrentId { get; set; }
        public DateTimeOffset? LastSeenRemoteCurrentCreatedAt { get; set; }
        public DateTimeOffset? LastSeenRemoteUpdatedAt { get; set; }
        public DateTimeOffset? LastSeenRemoteAt { get; set; }
    }
}