
namespace RomMLibrary.Models.RomM.Rom
{
    enum MainSibling
    {
        None = -1,
        Current = 0,
        Other = 1
    }

    public struct GameInstallInfo
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public bool HasMultipleFiles { get; set; }
        public string DownloadURL { get; set; }
        public EmulatorMapping Mapping { get; set; }
    }

    public class RomMRomLocal
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? SHA1 { get; set; }
        public string? FileName { get; set; }
        public bool HasMultipleFiles { get; set; }
        public string? DownloadURL { get; set; }
        public Guid MappingID { get; set; }

    }

    public class RomMSave
    {
        public int UserID;
        public int SaveID;
        public string FileName = "";
        public string FilePath = "";
        public int Slot;
        public bool SyncStatus;
    }
    public class RomMSaveState
    {
        
    }
}
