using Graviton.Models.Notifications;
using Graviton.Models.Saves;

using Playnite;

using System.IO;
using System.Text.Json;

namespace Graviton.Models.RomM.Rom
{
  
    public struct GameInstallInfo
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public bool HasMultipleFiles { get; set; }
        public string DownloadURL { get; set; }
        public string InstallPath { get; set; }

        public int PatchFileID { get; set; }

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
        public string? InstallPath { get; set; }
        public Guid MappingID { get; set; }

        public int PatchFileId { get; set; } = -1;

        public GravitonSave? LocalSave { get; set; }
        public LocalSaveState SaveStates { get; set; } = new();

        public static RomMRomLocal? Build(Guid MappingID, RomMRom ROM, string PlayniteID = "")
        {
            RomMRomLocal toSave = new RomMRomLocal();

            // Save base ROM data
            toSave.Id = ROM.Id;
            toSave.Name = ROM.Name;
            toSave.SHA1 = ROM.SHA1;
            toSave.HasMultipleFiles = ROM.HasMultipleFiles;
            toSave.PlayniteID = string.IsNullOrEmpty(PlayniteID) ? GravitonPlugin.Instance.ImportedGames[$"{ROM.Id}:{ROM.SHA1}"].PlayniteID : PlayniteID;

            toSave.InstallPath = EmulatorMapping.InstallPathToken + ROM.FullPath?.Replace(ROM.FileSystemPath ?? "", "");

            if (!ROM.HasMultipleFiles)
            {
                var romfile = DetermineFile(ROM);
                if (romfile == null)
                {
                    GravitonPlugin.Logger.Error("Unable to save ROM data as there is no rom file!");
                    return null;
                }

                toSave.FileName = Path.GetFileName(romfile.FileName);
                toSave.DownloadURL = $"/api/roms/{ROM.Id}/content/{romfile.FileName}";
            }
            else
            {
                var fileIDs = ROM.Files.Where(x => x.Category == "game").Select(y => y.Id.ToString()).ToList();

                if(fileIDs == null || fileIDs.Count() <= 0)
                {
                    GravitonPlugin.Logger.Error("Unable to save ROM data as there are no files in the game category!");
                    return null;
                }

                toSave.FileName = Path.GetFileName(ROM.FileName);
                toSave.DownloadURL = $"/api/roms/{ROM.Id}/content/{ROM.FileName}?file_ids={string.Join(',', fileIDs)}";
            }
            toSave.MappingID = MappingID;

            toSave.Save();
            return toSave;
        }

        public void Resync(RomMRom ROM)
        {
            Id = ROM.Id;
            Name = ROM.Name;
            SHA1 = ROM.SHA1;
            HasMultipleFiles = ROM.HasMultipleFiles;
            InstallPath = EmulatorMapping.InstallPathToken + ROM.FullPath?.Replace(ROM.FileSystemPath ?? "", "");

            if (!ROM.HasMultipleFiles)
            {
                var romfile = DetermineFile(ROM);
                if (romfile == null)
                {
                    GravitonPlugin.Logger.Error("[Importer] Unable to save ROM data as there is no rom file!");
                    return;
                }

               FileName = Path.GetFileName(romfile.FileName);
               DownloadURL = $"/api/roms/{ROM.Id}/content/{romfile.FileName}";
            }
            else
            {
                var fileIDs = ROM.Files.Where(x => x.Category == "game").Select(y => y.Id.ToString()).ToList();

                if (fileIDs == null || fileIDs.Count() <= 0)
                {
                    GravitonPlugin.Logger.Error("Unable to save ROM data as there are no files in the game category!");
                    return;
                }

                FileName = Path.GetFileName(ROM.FileName);
                DownloadURL = $"/api/roms/{ROM.Id}/content/{ROM.FileName}?file_ids={string.Join(',', fileIDs)}";
            }   

            Save();
        }

        public void Save()
        {
            try
            {
                if (LocalSave != null)
                    LocalSave.SourceFilePaths = LocalSave.SourceFilePaths.Select(x => x.Replace("\\", "/")).ToObservableCollection();

                // Write data to file
                File.WriteAllText($"{GravitonPlugin.Instance.PluginDataPath}/Games/{SHA1}.json", JsonSerializer.Serialize(this));
                if (GravitonPlugin.Instance.ImportedGames.ContainsKey($"{Id}:{SHA1}"))
                    GravitonPlugin.Instance.ImportedGames[$"{Id}:{SHA1}"] = this;       
                else
                    GravitonPlugin.Instance.ImportedGames.TryAdd($"{Id}:{SHA1}", this);

            }
            catch (Exception ex)
            {
                GravitonNotify.Add(new GravitonNotification($"graviton.write.rom.{Id}", Loc.GetString("ROMDataSaveFailed", ("Error", ex.Message)), GravitonSeverity.Error, ex));
            }
        }

        private static RomMFile? DetermineFile(RomMRom ROM)
        {
            if (ROM.Files == null)
                return null;

            if (ROM.Files.Count > 1)
                return ROM.Files.Where(x => x.Category == "Game").OrderBy(f => f.FullPath.Count(c => c == '/')).FirstOrDefault();

            return ROM.Files.FirstOrDefault();
        }

    }
}
