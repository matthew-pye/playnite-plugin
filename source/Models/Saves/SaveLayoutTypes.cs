
namespace Graviton.Models.Saves
{
    public enum SaveLayoutTypes
    {
        Auto,
        Single,
        Multi,
        Folder,
        Card
    }

    [Flags]
    public enum SaveMatchStyle 
    { 
        None,
        Filename, 
        SaveTarget, 
        Both = Filename | SaveTarget, 
        Manual 
    }

    public enum SaveSyncState
    {
        NotTracked,
        Unknown,
        InSync,
        LocalAhead,
        RemoteAhead,
        Diverged,   
        BranchKept,
        PushQueued,
        PushFailed
    }
}
