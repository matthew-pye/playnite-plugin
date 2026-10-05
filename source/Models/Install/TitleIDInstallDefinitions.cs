using Playnite;

using System.IO;

namespace Graviton.Models.Install
{
    internal static class TitleIDInstallDefinitions
    {
        public static string RegexPrefix = "{!RGX!}";

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
                    Loc.GetString("CemuTitleIDPathNote"),
                    [
                        new (["code/", "content/", "meta/"])
                    ]
                ),
                new(
                    "cemu.dlc",
                    "Cemu - DLC",
                    ["cemu"],
                    ["cemu.exe"],
                    "{FolderPath}\\0005000c\\{TitleID}\\",
                    Loc.GetString("CemuTitleIDPathNote"),
                    [
                        new (["code/", "content/", "meta/"])
                    ]
                ),
                
                // Xenia
                new(
                    "xenia.tuupdate",
                    "Xenia - TU Update",
                    ["xenia", "xenia_edge"],
                    [
                        "xenia.exe",
                        "xenia_canary.exe",
                        "xenia_canary_netplay.exe",
                        "xenia_edge.exe"
                    ],
                    "{FolderPath}\\{TitleID}\\000B0000\\",
                    Loc.GetString("XeniaTitleIDPathNote"),
                    [
                        new ([@"{!RGX!}^TU_[A-Z0-9]{7}_[0-9]{13}\."])
                    ]
                ),
                
                //new( // This will require looking into STFS extraction and building of package headers or have the xenia team expose `Install Content` through the CLI
                //    "xenia.dlc",
                //    "Xenia - DLC",
                //    ["xenia", "xenia_edge"],
                //    [
                //        "xenia.exe",
                //        "xenia_canary.exe",
                //        "xenia_canary_netplay.exe",
                //        "xenia_edge.exe"
                //    ],
                //    "{FolderPath}\\{TitleID}\\",
                //    Loc.GetString("XeniaTitleIDPathNote"),
                //    [
                //        new (["00000002/"]),
                //    ]
                //),
                
                // shadPS4
                new(
                    "shadps4.update",
                    "shadPS4 - Update",
                    ["shadps4"],
                    ["shadPS4.exe", "shadPS4QtLauncher.exe"],
                    "{FolderPath}\\{TitleID}-UPDATE\\",
                    Loc.GetString("ShadPS4UpdatePathNote"),
                    [
                        new (["sce_sys/param.sfo"])
                    ]
                ),
                new(
                    "shadps4.dlc",
                    "shadPS4 - DLC",
                    ["shadps4"],
                    ["shadPS4.exe", "shadPS4QtLauncher.exe"],
                    "{FolderPath}\\{TitleID}-{CandidateName}\\",
                    Loc.GetString("ShadPS4DLCPathNote"),
                    [
                        new (["sce_sys/param.sfo"])
                    ]
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
