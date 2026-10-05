using Graviton.Models;
using Graviton.Models.Install;
using Graviton.Models.ROM;
using Graviton.Models.RomM.Rom;

using Playnite;

using System.Collections.ObjectModel;
using System.IO;

namespace Graviton.Install
{
    public static class InstallUpdateDLC
    {
        public static async Task<RomMRomLocal> RefreshCandidates(EmulatorMapping mapping, RomMRom ROM, RomMRomLocal LocalROM)
        {
            var updates = await FindCategoryCandidates(mapping, ROM, RomMCategory.Update) ?? [];
            MergeCandidates(LocalROM.UpdateCandidates, updates, RomMCategory.Update);

            var dlc = await FindCategoryCandidates(mapping, ROM, RomMCategory.DLC) ?? [];
            MergeCandidates(LocalROM.DLCCandidates, dlc, RomMCategory.DLC);

            LocalROM.Save();

            return LocalROM;
        }

        private static async Task<List<UpdateDLCCandidate>?> FindCategoryCandidates(EmulatorMapping mapping, RomMRom ROM, string category)
        {
            try
            {

                if(!ROM.Files.Any(x => x.Category == category))
                {
                    GravitonPlugin.Logger.Info($"{ROM.Name} ({ROM.Id}) doesn't have any files within the {category} folder, skipping {category} candidates check");
                    return null;
                }

                List<UpdateDLCCandidate> candidates = new();

                InstallStyles installshape = InstallStyles.None;
                CLIInstallDefinition? CLIInstaller = null;
                TitleIDInstallDefinition? titleIDInstaller = null;

                if (category == RomMCategory.Update)
                {
                    installshape = mapping.UpdateInstallStyle;
                    CLIInstaller = mapping.UpdateCLIDefinition;
                    titleIDInstaller = mapping.UpdateTitleIDDefinition;
                }
                    
                if(category == RomMCategory.DLC)   
                {
                    installshape = mapping.DLCInstallStyle;
                    CLIInstaller = mapping.DLCCLIDefinition;
                    titleIDInstaller = mapping.DLCTitleIDDefinition;
                }

                if(installshape == InstallStyles.Folder)
                {
                    if (category == RomMCategory.Update)
                    {
                        var files = ROM.Files.Where(x => x.Category == RomMCategory.Update).ToList();
                        var size = files.Sum(x => x.FileSize ?? 0);

                        if (files.Count == 1)
                            return [new(Path.GetFileName(files[0].FileName), files[0].FileName, category, [.. files.Select(y => y.Id)], null, null, size)];
                        else
                            return [new("Update Folder", $"{ROM.Name}-Update.zip", category, [.. files.Select(y => y.Id)], null, null, size)];
                    }
                    else if (category == RomMCategory.DLC)
                    {
                        var files = ROM.Files.Where(x => x.Category == RomMCategory.DLC).ToList();
                        var size = files.Sum(x => x.FileSize ?? 0);

                        if (files.Count == 1)
                            return [new(Path.GetFileName(files[0].FileName), files[0].FileName, category, [.. files.Select(y => y.Id)], null, null, size)];
                        else
                            return [new("DLC Folder", $"{ROM.Name}-DLC.zip", category, [.. files.Select(y => y.Id)], null, null, size)];


                    }
                }

                if (installshape == InstallStyles.CLI)
                {
                    if (CLIInstaller == null || CLIInstaller.ID == null)
                        throw new Exception("Mapping has no CLI installer selected");

                    var supportedfiles = ROM.Files.Where(x => x.Category == category && CLIInstaller.SupportedExtensions.Any(y => x.FullPath.EndsWith(y, StringComparison.OrdinalIgnoreCase))).ToList();

                    if(supportedfiles.Count == 0)
                    {
                        GravitonPlugin.Logger.Info($"{ROM.Name} ({ROM.Id}) doesn't have any supported files within the {category} folder, skipping update candidates check");
                        return null;
                    }

                    foreach (var file in supportedfiles)
                    {
                        candidates.Add(
                            new(
                                Path.GetFileNameWithoutExtension(file.FileName),
                                file.FileName,
                                category,
                                [file.Id],
                                null,
                                null,
                                file.FileSize ?? 0
                                ));
                    }

                    return candidates;
                }

                if (installshape == InstallStyles.MappedFolder)
                {
                    if (string.IsNullOrEmpty(ROM.FullPath))
                        throw new Exception("ROM has no full path set");

                    if(titleIDInstaller == null)
                        throw new Exception("Mapping has no TitleID installer selected");

                    if (titleIDInstaller.InstallShapes == null || titleIDInstaller.InstallShapes.Count <= 0)
                        throw new Exception("TitleID installer selected has no shapes to match against");

                    var updatefiles = ROM.Files.Where(x => x.Category == category).ToList();

                    // Check if files have the category shape we are looking for
                    var filetree = RomMFileTree.Build(ROM.FullPath, updatefiles);
                    var matches = RomMFileTree.FindMatchingRoots(filetree, titleIDInstaller.InstallShapes);
                    foreach (var match in matches)
                    {
                        var fileIDs = match.CollectFileIDs();
                        long candidateSize = 0;
                        foreach (var id in fileIDs)
                        {
                            candidateSize += updatefiles.First(x => x.Id == id).FileSize ?? 0;
                        }
                        
                        if(fileIDs.Count == 1)
                        {
                            var file = updatefiles.First(x => x.Id == fileIDs[0]);

                            var relativePath = Path.GetRelativePath(Path.Combine(ROM.FullPath, match.FullPath), file.FullPath);

                            candidates.Add(new(
                                                match.Name,
                                                file.FileName,
                                                category,
                                                fileIDs,
                                                null,
                                                relativePath,
                                                candidateSize
                            ));
                        }     
                        else
                        {
                            candidates.Add(new(
                                               match.Name,
                                               $"{match.Name}.zip",
                                               category,
                                               fileIDs,
                                               match.FullPath,
                                               null,
                                               candidateSize
                           ));
                        }    
                    }

                    // Check if archive files have the shape we are looking for
                    var archiveFiles = updatefiles.Where(x => x.ArchiveMembers != null).ToList();
                    if (archiveFiles.Count > 0)
                    {
                        foreach (var file in archiveFiles)
                        {
                            var arcFileTree = ArchiveMemberTree.Build(file.ArchiveMembers!);
                            var arcMatches = ArchiveMemberTree.FindMatchingRoots(arcFileTree, titleIDInstaller.InstallShapes);

                            foreach (var match in arcMatches)
                            {
                                var isArchiveRoot = string.IsNullOrEmpty(match.FullPath);

                                candidates.Add(new(
                                                   isArchiveRoot ? Path.GetFileNameWithoutExtension(file.FileName) : match.Name,
                                                   file.FileName,
                                                   category,
                                                   [file.Id],
                                                   isArchiveRoot ? null : match.FullPath,
                                                   null,
                                                   file.FileSize ?? 0
                                ));
                            }
                        }
                    }

                    return candidates;
                }

                return null;
            }
            catch (Exception ex)
            { 
                GravitonNotify.Notify("graviton.findcadidate.failed", Loc.GetString("CandidateDiscoveryFailed", ("Category", category), ("Error", ex.Message)), Models.Notifications.GravitonSeverity.Error, ex);
                return null;
            }
        }

        private static void MergeCandidates(ObservableCollection<UpdateDLCCandidate> existing, List<UpdateDLCCandidate> discovered, string category)
        {
            foreach (var candidate in discovered)
            {
                candidate.Category = category;

                var old = existing.FirstOrDefault(x => x.Category == category && 
                                                       x.Name.Equals(candidate.Name, StringComparison.OrdinalIgnoreCase) && 
                                                       string.Equals(x.CandidateRoot, candidate.CandidateRoot, StringComparison.OrdinalIgnoreCase));

                old ??= existing.FirstOrDefault(x => x.FileIDs.Intersect(candidate.FileIDs).Any());

                if (old == null)
                    continue;

                candidate.InstalledFileIDs = old.InstalledFileIDs.ToList();
                candidate.InstalledTopPaths = old.InstalledTopPaths.ToList();
                candidate.InstalledSize = old.InstalledSize;
                candidate.PreviousInstallStyle = old.PreviousInstallStyle;

                var current = candidate.FileIDs.ToHashSet();
                var installed = candidate.InstalledFileIDs.ToHashSet();

                if (candidate.InstalledFileIDs.Count == 0)
                    candidate.Status = InstallStatus.NotInstalled;
                else if (current.IsSubsetOf(installed))
                    candidate.Status = InstallStatus.Installed;
                else if (current.Overlaps(installed))
                    candidate.Status = InstallStatus.PartialInstalled;
                else
                    candidate.Status = InstallStatus.NotInstalled;  
            }

            existing.Clear();

            foreach (var candidate in discovered)
                existing.Add(candidate);
        }

    }
}