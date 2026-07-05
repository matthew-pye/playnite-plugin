
using Graviton.Models.RomM.Saves;

namespace Graviton.Models.RomM.Rom
{
  
    public struct GameInstallInfo
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public bool HasMultipleFiles { get; set; }
        public string DownloadURL { get; set; }
        public EmulatorMapping? Mapping { get; set; }
    }

    public class RomMRomLocal
    {
        public int Id { get; set; }
        public string? PlayniteID { get; set; }
        public string? Name { get; set; }
        public string? SHA1 { get; set; }
        public string? FileName { get; set; }
        public bool HasMultipleFiles { get; set; }
        public string? DownloadURL { get; set; }
        public Guid MappingID { get; set; }

        public List<LocalSave> Saves { get; set; } = new List<LocalSave>();
        public List<LocalSaveState> SaveStates { get; set; } = new List<LocalSaveState>();

    }
}
