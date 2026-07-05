using Graviton.Models;
using Graviton.Models.RomM;
using Graviton.Models.RomM.Saves;

using Playnite;

using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Graviton.Saves
{
    internal class SaveController
    {
        private GravitonPlugin _plugin;
        private IPlayniteApi _playniteAPI;
        private ILogger _logger;

        public SaveController(GravitonPlugin plugin, IPlayniteApi playniteAPI, ILogger logger)
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;
        }


        public async Task NegotiateSaves()
        {

        }

        public async Task<List<SaveRow>?> GetLocalSaves(EmulatorMapping mapping)
        {
            var roms = _plugin.ImportedGames!.Where(x => x.Value.MappingID == mapping.MappingId).Select(y => y.Value);
            var negotiate = new RomMNegotiate();
            negotiate.DeviceID = _plugin.Settings.AccountState.DeviceID;

            foreach (var rom in roms)
            {
                foreach (var save in rom.Saves)
                {
                    RomMNegotiateSave negotitatesave = new()
                    {
                        ROMID = rom.Id,
                        Slot = save.Slot
                    };

                    if (save.SourceFilePaths.Count > 1)
                    {

                    }
                    else if(save.SourceFilePaths.Count == 1)
                    {
                        negotitatesave.FileSize = new FileInfo(save.SourceFilePaths[0]).Length;
                        negotitatesave.FileName = Path.GetFileName(save.SourceFilePaths[0]);

                        using (var md5 = MD5.Create())
                        {
                            using (var stream = File.OpenRead(save.SourceFilePaths[0]))
                            {
                                byte[] hash = md5.ComputeHash(stream);
                                negotitatesave.ContentHash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                            }
                        }
                    }

                    negotiate.Saves.Add(negotitatesave);
                }
            }

            var response = await HttpClientSingleton.RomMPostJsonAsync("/api/sync/negotiate", negotiate);
            if (response == null)
                return null;

            try
            {
                var result = JsonSerializer.Deserialize<RomMNegotiateResponse>(response);
                if (result == null)
                    return null;


                List<SaveRow> saverows = new();
                foreach (var save in result.Operations)
                {
                    var row = new SaveRow
                    {
                        GameName = roms.First(x => x.Id == save.ROMID).Name!,
                        SaveID = save.SaveID!,
                        SyncStatus = Enum.Parse<SaveSyncStatus>(save.Action!),
                        SyncEnabled = roms.First(x => x.Id == save.ROMID).Saves.First(y => y.SaveID == save.SaveID).Enabled
                    };

                    row.BuildSaveDirectoryView(roms.First(x => x.Id == save.ROMID).Saves.First(y => y.SaveID == save.SaveID).SourceFilePaths);

                    saverows.Add(row);
                }

                return saverows;
            }
            catch (Exception ex)
            { return null; }

            

        }

        public async Task<List<SaveRow>?> GetRemoteSaves(EmulatorMapping mapping)
        {
            var response = await HttpClientSingleton.RomMGetAsync($"/api/saves?platform_id={mapping.RomMPlatformId}&device_id={_plugin.Settings.AccountState.DeviceID}");
            if (response == null)
                return null;

            var result = JsonSerializer.Deserialize<List<RomMSave>>(response);
            if (result == null)
                return null;

            var roms = _plugin.ImportedGames!.Where(x => x.Value.MappingID == mapping.MappingId).Select(y => y.Value);

            List<SaveRow> saverows = new();
            foreach (var save in result)
            {
                var row = new SaveRow()
                {
                    GameName = roms.First(x => x.Id == save.ROMID).Name!,
                    SaveID = save.ID,
                    SyncStatus = SaveSyncStatus.download,
                };

                row.SaveDirectoryView.Add(new DirectorySaveFile
                {
                    Name = save.FileName!
                });

                saverows.Add(row);
            }

            return saverows;

        }

        public async Task<List<SaveRow>> ScanForPossibleSaves(EmulatorMapping mapping)
        {
            var roms = _plugin.ImportedGames!.Where(x => x.Value.MappingID == mapping.MappingId);
            var matchedsaves = new List<SaveRow>();

            string[]? extentions = null;
            if(!string.IsNullOrEmpty(mapping.SaveFileExtensions))
                extentions = mapping.SaveFileExtensions.Split(';');

            foreach (var file in Directory.EnumerateFiles("", "*", SearchOption.AllDirectories))
            {

                var matchedrom = roms.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x.Value.FileName) == Path.GetFileNameWithoutExtension(file)).Value;
                if(matchedrom != null)
                {
                    switch (mapping.SaveLayout)
                    {
                        case SaveLayoutStyle.SingleFile:
                            if(extentions != null && extentions.Length == 1)
                            {
                                var saverow = new SaveRow
                                {
                                    GameName = matchedrom.Name!,

                                };

                                matchedsaves.Add(saverow);
                                continue;
                            }
                            break;

                        case SaveLayoutStyle.FixedSet:
                            break;

                        case SaveLayoutStyle.WholeFolder:
                            break;

                        case SaveLayoutStyle.ManualPerGame:
                            break;

                        default:
                            break;
                    }

                }


                if(extentions != null && extentions.Contains(Path.GetExtension(file)))
                {



                }
                else
                {

                }
            }

            return new List<SaveRow>();
        }

    }
}
