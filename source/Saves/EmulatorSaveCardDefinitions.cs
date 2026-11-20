namespace Graviton.Saves
{
    public sealed record SaveCardDefinition
    (
        string EmulatorID,
        List<string>? CoreIDs,
        List<string>? PlatformIDs,
        List<string> FilePatterns
    );

    internal static class SaveCardDefinitions
    {
        private static readonly List<SaveCardDefinition> Definitions = new();

        internal static IReadOnlyList<SaveCardDefinition> All => Definitions;

        internal static void Initialize()
        {
            if (Definitions.Count > 0)
                return;

            Definitions.AddRange(
            [
                new
                (
                    "pcsx2",
                    null,
                    ["sony_playstation2"],
                    ["*.ps2"]
                ),
                new
                (
                    "dolphin",
                    null,
                    ["nintendo_gamecube"],
                    ["MemoryCard?.*.raw"]
                ),
                new
                (
                    "retroarch",
                    ["pcsx_rearmed"],
                    null,
                    ["pcsx-card?.mcd"]
                ),
            ]);
        }

        internal static IReadOnlyList<SaveCardDefinition>? Find(string? EmulatorID = null, string? CoreID = null, List<string>? PlatformIDs = null)
        {
            if (EmulatorID == null)
                return null;

            if(EmulatorID != null && CoreID != null)
            {
                return Definitions.Where(x => (!string.IsNullOrEmpty(EmulatorID) && x.EmulatorID.Equals(EmulatorID, StringComparison.OrdinalIgnoreCase)
                                           && x.CoreIDs != null && x.CoreIDs.Contains(CoreID))
                                           || (PlatformIDs != null && x.PlatformIDs != null && PlatformIDs.Any(y => x.PlatformIDs.Contains(y)))).ToList();
            }
            else
            {
                return Definitions.Where(x => (!string.IsNullOrEmpty(EmulatorID) && x.EmulatorID.Equals(EmulatorID, StringComparison.OrdinalIgnoreCase))
                                           || (PlatformIDs != null && x.PlatformIDs != null && PlatformIDs.Any(y => x.PlatformIDs.Contains(y)))).ToList();
            }
        }
    }
}