using Graviton.Models.Install;
using Graviton.Models.Notifications;
using Graviton.Models.RomM.Rom;
using Graviton.Models.Saves;
using Graviton.Notifications;

using Playnite;

using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Graviton.Models.ROM
{
  
    public struct GameInstallInfo
    {
        public int Id { get; set; }
        public string GameName { get; set; }
        public string FileName { get; set; }
        public bool HasMultipleFiles { get; set; }
        public string DownloadURL { get; set; }
        public string InstallPath { get; set; }

        public string? SaveTarget { get; set; } 
        public string? TitleID { get; set; }

        public int PatchFileID { get; set; }

        public EmulatorMapping? Mapping { get; set; }


        public static GameInstallInfo Build(RomMRomLocal localROM, EmulatorMapping mapping)
        {
            return new()
            {
                Id = localROM.Id,
                FileName = localROM.FileName ?? "",
                GameName = localROM.Name ?? "",
                HasMultipleFiles = localROM.HasMultipleFiles,
                DownloadURL = localROM.DownloadURL ?? "",
                InstallPath = localROM.InstallPath ?? "",
                PatchFileID = localROM.PatchFileId,
                Mapping = mapping,
                SaveTarget = localROM.SaveTarget,
                TitleID = localROM.TitleID,
            };
        }
    }

    public class RomMRomLocal
    {
        public int Id { get; set; }
        public string? PlayniteID { get; set; }
        public string Name { get; set; } = "Unknown Game";
        public string? SHA1 { get; set; }

        public string? FileName { get; set; }
        public int PatchFileId { get; set; } = -1;

        public bool HasMultipleFiles { get; set; }
        public string DownloadURL { get; set; } = "";
        public string InstallPath { get; set; } = "";
        public string? InstalledPath { get; set; }
        public bool IsInstalledPathDirectory { get; set; }

        public string? TitleID { get; set; }
        public string? SaveTarget { get; set; }
        public string? SaveTargetLayout { get; set; }
        public Guid MappingID { get; set; }

        public int? ChannelFileId { get; set; }
        public LocalSave? CurrentSave { get; set; }
        public ObservableCollection<PlaythroughLocal> Playthroughs { get; set; } = new();
        [JsonIgnore] public PlaythroughLocal? ActivePlaythrough => Playthroughs.FirstOrDefault(x => x.IsActive);


        public ObservableCollection<UpdateDLCCandidate> UpdateCandidates { get; set; } = new();
        public ObservableCollection<UpdateDLCCandidate> DLCCandidates { get; set; } = new();

        internal static RomMRomLocal? Build(Guid MappingID, RomMRom ROM, string PlayniteID, IGravitonContext? plugin)
        {
            RomMRomLocal toSave = new RomMRomLocal();

            // Save base ROM data
            toSave.Id = ROM.Id;
            toSave.Name = ROM.Name ?? "Unknown Game";
            toSave.SHA1 = ROM.SHA1;
            toSave.HasMultipleFiles = ROM.HasMultipleFiles;
            toSave.PlayniteID = string.IsNullOrEmpty(PlayniteID) ? GravitonPlugin.Instance.ImportedGames[$"{ROM.Id}"].PlayniteID : PlayniteID;

            toSave.TitleID = ROM.TitleID;
            toSave.SaveTarget = ROM.SaveTarget;
            toSave.SaveTargetLayout = ROM.SaveTargetLayout;
            toSave.ChannelFileId = DetermineChannelFileId(ROM);

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
                toSave.DownloadURL = $"/api/roms/{ROM.Id}/content/{Uri.EscapeDataString(romfile.FileName)}";
            }
            else
            {
                var fileIDs = ROM.Files.Where(x => x.Category == RomMCategory.Game).Select(y => y.Id.ToString()).ToList();

                if(fileIDs == null || fileIDs.Count() <= 0)
                {
                    GravitonPlugin.Logger.Error("Unable to save ROM data as there are no files in the game category!");
                    return null;
                }

                toSave.FileName = Path.GetFileName(ROM.FileName);
                toSave.DownloadURL = $"/api/roms/{ROM.Id}/content/{Uri.EscapeDataString(ROM.FileName)}?file_ids={string.Join(',', fileIDs)}";
            }
            toSave.MappingID = MappingID;

            if (plugin == null)
                toSave.Save();
            else
                toSave.Save(plugin);

            return toSave;
        }

        internal void Resync(RomMRom ROM) => Resync(GravitonPlugin.Logger, ROM);

        internal void Resync(GravitonLogger logger, RomMRom ROM, IGravitonContext? plugin = null)
        {
            Id = ROM.Id;
            Name = ROM.Name ?? "Unknown Game";
            SHA1 = ROM.SHA1;
            HasMultipleFiles = ROM.HasMultipleFiles;
            InstallPath = EmulatorMapping.InstallPathToken + ROM.FullPath?.Replace(ROM.FileSystemPath ?? "", "");

            TitleID = ROM.TitleID;
            SaveTarget = ROM.SaveTarget;
            SaveTargetLayout = ROM.SaveTargetLayout;
            ChannelFileId = DetermineChannelFileId(ROM);

            if (!ROM.HasMultipleFiles)
            {
                var romfile = DetermineFile(ROM);
                if (romfile == null)
                {
                    logger.Error("[Importer] Unable to save ROM data as there is no rom file!");
                    return;
                }

               FileName = Path.GetFileName(romfile.FileName);
               DownloadURL = $"/api/roms/{ROM.Id}/content/{Uri.EscapeDataString(romfile.FileName)}";
            }
            else
            {
                var fileIDs = ROM.Files.Where(x => x.Category == RomMCategory.Game).Select(y => y.Id.ToString()).ToList();

                if (fileIDs == null || fileIDs.Count() <= 0)
                {
                    logger.Error("Unable to save ROM data as there are no files in the game category!");
                    return;
                }

                FileName = Path.GetFileName(ROM.FileName);
                DownloadURL = $"/api/roms/{ROM.Id}/content/{Uri.EscapeDataString(ROM.FileName)}?file_ids={string.Join(',', fileIDs)}";
            }

            if (plugin == null)
                Save();
            else
                Save(plugin);
        }

        internal void Save() => Save(GravitonPlugin.Instance);

        internal void Save(IGravitonContext plugin)
        {
            try
            {
                // Write data to file
                File.WriteAllText($"{plugin.PluginDataPath}/Games/{Id}.json", JsonSerializer.Serialize(this));

                if (plugin.ImportedGames.ContainsKey($"{Id}"))
                    plugin.ImportedGames[$"{Id}"] = this;       
                else
                    plugin.ImportedGames.TryAdd($"{Id}", this);

            }
            catch (Exception ex)
            {
                GravitonNotify.Notify($"graviton.write.rom.{Id}", Loc.GetString("SaveROMDataFailed", ("Error", ex.Message)), GravitonSeverity.Error, ex);
            }
        }

        private static RomMFile? DetermineFile(RomMRom ROM)
        {
            if (ROM.Files == null)
                return null;

            if (ROM.Files.Count > 1)
                return ROM.Files.Where(x => x.Category == RomMCategory.Game).OrderBy(f => f.FullPath.Count(c => c == '/')).FirstOrDefault();

            return ROM.Files.FirstOrDefault();
        }

        static readonly HashSet<string> LoaderExtensions = new(StringComparer.OrdinalIgnoreCase) { "cue", "gdi", "ccd", "mds", "toc" };

        static int? DetermineChannelFileId(RomMRom ROM)
        {
            if (ROM.Files == null)
                return null;

            var gameFiles = ROM.Files.Where(f => string.IsNullOrEmpty(f.Category) || f.Category == RomMCategory.Game).OrderBy(f => f.FileName.ToLowerInvariant(), StringComparer.Ordinal).ToList();
            var loadersFiles = gameFiles.Where(f => LoaderExtensions.Contains(Path.GetExtension(f.FileName).TrimStart('.'))).ToList();
            
            return (loadersFiles.Count > 0 ? loadersFiles : gameFiles).FirstOrDefault()?.Id;
        }

    }
}