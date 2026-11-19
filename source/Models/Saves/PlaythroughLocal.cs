using CommunityToolkit.Mvvm.ComponentModel;

namespace Graviton.Models.Saves
{
    public partial class PlaythroughLocal : ObservableObject
    {
        public string ChannelId { get; set; } = Guid.NewGuid().ToString();
        [ObservableProperty] private string _name = "default";         
        [ObservableProperty] private bool _isActive;

        public bool PendingCreate { get; set; } = true;

        public int? HeldSnapshotId { get; set; }           
        public string? HeldHash { get; set; }               
        public int? UnresolvedBranchId { get; set; }       

        public int? LastSeenRemoteCurrentId { get; set; }
        public DateTimeOffset? LastSeenRemoteAt { get; set; }
    }
}