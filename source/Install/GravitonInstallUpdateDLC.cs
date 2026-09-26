using Graviton.Models;
using Graviton.Models.Install;
using Graviton.Models.RomM.Rom;

using System.Text.Json;

namespace Graviton.Install
{
    public sealed record UpdateDLCCandidate(string Name, IReadOnlyCollection<int> FileIDs);

    public static class InstallUpdateDLC
    { 

        public static async Task<List<UpdateDLCCandidate>?> FindCategoryCandidates(EmulatorMapping mapping, int RomMID, string category)
        {
            var response = await GravitonPlugin.RomMServer.GETAsync($"/api/roms/{RomMID}");
            if (response == null)
                return null;

            try
            {
                var rom = JsonSerializer.Deserialize<RomMRom>(response);
                if (rom == null)
                    throw new Exception("Rom is null");

                if(!rom.Files.Any(x => x.Category == category))
                {
                    GravitonPlugin.Logger.Info($"{rom.Name} ({rom.Id}) doesn't have any files within the {category} folder, skipping {category} candidates check");
                    return null;
                }

                List<UpdateDLCCandidate> candidates = new();

                InstallShapes installshape = InstallShapes.None;
                CLIInstallDefinition? CLIInstaller = null;
                TitleIDInstallDefinition? titleIDInstaller = null;

                if (category == "update")
                {
                    installshape = mapping.UpdateInstallStyle;
                    CLIInstaller = mapping.UpdateCLIDefinition;
                    titleIDInstaller = mapping.UpdateTitleIDDefinition;
                }
                    

                if(category == "dlc")   
                {
                    installshape = mapping.DLCInstallStyle;
                    CLIInstaller = mapping.DLCCLIDefinition;
                    titleIDInstaller = mapping.DLCTitleIDDefinition;
                }

                if(installshape == InstallShapes.ExternalFolder)
                {
                    if (category == "update")
                    {

                    }
                    else if (category == "dlc")
                    {
                        return [new("All", [.. rom.Files.Where(x => x.Category == "dlc").Select(y => y.Id)])];
                    }
                }

                if (installshape == InstallShapes.CLI)
                {
                    if (CLIInstaller == null || CLIInstaller.ID == null)
                        throw new Exception("Mapping has no CLI installer selected");

                    var supportedfiles = rom.Files.Where(x => x.Category == category && CLIInstaller.SupportedExtensions.Any(y => x.FullPath.EndsWith(y, StringComparison.OrdinalIgnoreCase))).ToList();

                    if(supportedfiles.Count == 0)
                    {
                        GravitonPlugin.Logger.Info($"{rom.Name} ({rom.Id}) doesn't have any supported files within the {category} folder, skipping update candidates check");
                        return null;
                    }

                    foreach (var file in supportedfiles)
                    {
                        candidates.Add(
                            new(
                                file.FileName,
                                [file.Id]
                                ));
                    }

                    return candidates;
                }

                if (installshape == InstallShapes.TitleIDFolder)
                {
                    if (string.IsNullOrEmpty(rom.FullPath))
                        throw new Exception("ROM has no full path set");

                    if(titleIDInstaller == null)
                        throw new Exception("Mapping has no TitleID installer selected");

                    if (titleIDInstaller.InstallShapes == null || titleIDInstaller.InstallShapes.Count <= 0)
                        throw new Exception("TitleID installer selected has no shapes to match against");

                    var updatefiles = rom.Files.Where(x => x.Category == category).ToList();
                    var filetree = RomMFileTree.Build(rom.FullPath, updatefiles);

                    var matches = RomMFileTree.FindMatchingRoots(filetree, titleIDInstaller.InstallShapes);

                    foreach (var match in matches)
                    {
                        candidates.Add(new(
                            match.Name,
                            match.CollectFileIDs()
                        ));
                    }

                    return candidates;
                }

                return null;
            }
            catch (Exception ex)
            {
                GravitonNotify.Notify("graviton.findcadidate.failed", $"Failed to find {category} candidates: {ex.Message}", Models.Notifications.GravitonSeverity.Error, ex);
                return null;
            }
        }
    }
}