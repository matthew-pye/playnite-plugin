using Playnite;

using System.IO;

namespace Graviton.Models.Install
{
    internal static class CLIInstallDefinitions
    {
        private static readonly List<CLIInstallDefinition> Definitions = new();

        internal static IReadOnlyList<CLIInstallDefinition> All => Definitions;

        internal static void Initialize()
        {
            if (Definitions.Count > 0)
                return;

            Definitions.AddRange(
            [
                // Null
                new(
                    null,
                    Loc.GetString("None"),
                    [],
                    [],
                    [],
                    "",
                    null
                    ),

                // RPCS3
                new(
                    "rpcs3.install-pkg",
                    Loc.GetString("CLIInstallRPCS3PKG"),
                    ["rpcs3"],
                    ["rpcs3.exe"],
                    [".pkg"],
                    "--installpkg \"{FilePath}\"",
                    null
                    ),

                // Vita3k
                new(
                    "vita3k.install-pkg",
                    Loc.GetString("CLIInstallVita3KPKG"),
                    ["vita3k"],
                    ["Vita3K.exe"],
                    [".pkg"],
                    "--pkg \"{FilePath}\"",
                    [new("zrif.key", Loc.GetString("CLIArgumentZRIF"), " --zrif \"{arg}\"")]
                    ),
                new( // Experimental
                    "vita3k.install-arc",
                    Loc.GetString("CLIInstallVita3KArchive"),
                    ["vita3k"],
                    ["Vita3K.exe"],
                    [".vpk", ".zip"],
                    "\"{FilePath}\"",
                    null
                    ),

                // Azahar
                new(
                    "azahar.install-cia",
                    Loc.GetString("CLIInstallAzaharCIA"),
                    ["azahar"],
                    ["azahar.exe"],
                    [".cia"],
                    "--install \"{FilePath}\"",
                    null
                ),


            ]);
        }

        internal static CLIInstallDefinition? Get(string? ID)
        {
            if (ID == null)
                return Definitions.First(x => string.IsNullOrEmpty(x.ID));

            return Definitions.FirstOrDefault(x => ID.Equals(x.ID, StringComparison.OrdinalIgnoreCase));
        }

        // Will find the CLI Definition thats matches based on either the Emulator ID or the Executable name
        internal static IReadOnlyList<CLIInstallDefinition>? Find(string? EmulatorID = null, string? ExecutableName = null)
        {
            if (EmulatorID == null && ExecutableName == null)
                return null;

            var executableName = Path.GetFileName(ExecutableName);

            return Definitions.Where(x => (!string.IsNullOrEmpty(EmulatorID) && x.KnownEmulatorIDs.Any(y => y.Equals(EmulatorID, StringComparison.OrdinalIgnoreCase)))
                                       || (!string.IsNullOrEmpty(executableName) && x.ExecutableNames.Any(y => y.Equals(executableName, StringComparison.OrdinalIgnoreCase)))).ToList();

        }
    }
}