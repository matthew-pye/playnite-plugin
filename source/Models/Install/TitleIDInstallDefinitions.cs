using Playnite;

using System.IO;

namespace Graviton.Models.Install
{ 
    internal static class TitleIDInstallDefinitions
    {
        private static readonly List<TitleIDInstallDefinition> Definitions = new();

        internal static IReadOnlyList<TitleIDInstallDefinition> All => Definitions;

        internal static void Initialize()
        {
            if (Definitions.Count > 0)
                return;

            Definitions.AddRange(
            [
                // Null
                new(
                    null,
                    Loc.GetString("Default"),
                    [],
                    [],
                    "{FolderPath}\\{TitleID}\\",
                    "",
                    null
                    ),

                // CEMU
                new(
                    "cemu.update",
                    "Cemu - Update",
                    ["cemu"],
                    ["cemu.exe"],
                    "{FolderPath}\\0005000e\\{TitleID}\\",
                    "Select Cemu's mlc01\\usr\\title folder.",
                    []
                ),
                new(
                    "cemu.dlc",
                    "Cemu - DLC",
                    ["cemu"],
                    ["cemu.exe"],
                    "{FolderPath}\\0005000c\\{TitleID}\\",
                    "Select Cemu's mlc01\\usr\\title folder.",
                    []
                ),
                
                // Xenia
                new(
                    "xenia.update",
                    "Xenia - Update",
                    ["xenia", "xenia_edge"],
                    [
                        "xenia.exe",
                        "xenia_canary.exe",
                        "xenia_canary_netplay.exe",
                        "xenia_edge.exe"
                    ],
                    "{FolderPath}\\{TitleID}\\000B0000\\",
                    "Select Xenia's content root folder, usually Documents\\Xenia.",
                    []
                ),
                new(
                    "xenia.dlc",
                    "Xenia - DLC",
                    ["xenia", "xenia_edge"],
                    [
                        "xenia.exe",
                        "xenia_canary.exe",
                        "xenia_canary_netplay.exe",
                        "xenia_edge.exe"
                    ],
                    "{FolderPath}\\{TitleID}\\00000002\\",
                    "Select Xenia's content root folder, usually Documents\\Xenia.",
                    []
                ),
                
                // shadPS4
                new(
                    "shadps4.update",
                    "shadPS4 - Update",
                    ["shadps4"],
                    ["shadPS4.exe", "shadPS4QtLauncher.exe"],
                    "{FolderPath}\\{TitleID}-UPDATE\\",
                    "Select the folder containing your installed shadPS4 games.",
                    []
                ),
                new(
                    "shadps4.dlc",
                    "shadPS4 - DLC",
                    ["shadps4"],
                    ["shadPS4.exe", "shadPS4QtLauncher.exe"],
                    "{FolderPath}\\{TitleID}\\",
                    "Select shadPS4's add-on content folder.",
                    []
                ),

            ]);
        }

        internal static TitleIDInstallDefinition? Get(string? ID)
        {
            if (ID == null)
                return Definitions.First(x => string.IsNullOrEmpty(x.ID));

            return Definitions.FirstOrDefault(x => ID.Equals(x.ID, StringComparison.OrdinalIgnoreCase));
        }

        // Will find the TitleID Definition thats matches based on either the Emulator ID or the Executable name
        internal static IReadOnlyList<TitleIDInstallDefinition>? Find(string? EmulatorID = null, string? ExecutableName = null)
        {
            if (EmulatorID == null && ExecutableName == null)
                return null;

            var executableName = Path.GetFileName(ExecutableName);

            return Definitions.Where(x => (!string.IsNullOrEmpty(EmulatorID) && x.KnownEmulatorIDs.Any(y => y.Equals(EmulatorID, StringComparison.OrdinalIgnoreCase)))
                                       || (!string.IsNullOrEmpty(executableName) && x.ExecutableNames.Any(y => y.Equals(executableName, StringComparison.OrdinalIgnoreCase)))).ToList();

        }
    }

}
