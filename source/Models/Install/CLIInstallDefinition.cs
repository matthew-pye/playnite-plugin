
using CommunityToolkit.Mvvm.ComponentModel;

using Playnite;

using System.IO;

namespace Graviton.Models.Install
{
    public partial class DynamicArgument : ObservableObject
    {
        public string ID { get; }
        public string Name { get; }
        public string Arg { get; }

        [ObservableProperty] private string? _value;

        public DynamicArgument(string id, string name, string arg)
        {
            ID = id;
            Name = name;
            Arg = arg;
        }

        public DynamicArgument Clone() => new(ID, Name, Arg);
    }

    public sealed record CLIInstallDefinition
    (
        string ID,
        string Name,
        IReadOnlyCollection<string> KnownEmulatorIDs,
        IReadOnlyCollection<string> ExecutableNames,
        IReadOnlyCollection<string> SupportedExtensions,
        string RuntimeArgs,
        IReadOnlyCollection<DynamicArgument>? DynamicArgsTemplate  
    );

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
                new( // TODO: NEEDS TESTING TO CHECK THIS WORKS
                    "vita3k.install-arc",
                    Loc.GetString("CLIInstallVita3KArchive"),
                    ["vita3k"],
                    ["Vita3K.exe"],
                    [".vpk", ".zip"],
                    "\"{FilePath}\"",
                    null
                    ),
            ]);
        }

        internal static CLIInstallDefinition? Get(string ID) => Definitions.FirstOrDefault(x => x.ID.Equals(ID, StringComparison.OrdinalIgnoreCase));
        
        // Will find the CLI Definition thats matches based on either the Emulator ID or the Executable name
        internal static IReadOnlyList<CLIInstallDefinition> Find(string? EmulatorID = null, string? ExecutableName = null) => Definitions.Where(x => x.KnownEmulatorIDs.Any(y => y.Equals(EmulatorID, StringComparison.OrdinalIgnoreCase)) || 
                                                                                                                                                     x.ExecutableNames.Any(y => y.Equals(Path.GetFileName(ExecutableName), StringComparison.OrdinalIgnoreCase))).ToList();
    }


}
