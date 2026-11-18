using CommunityToolkit.Mvvm.ComponentModel;

using Graviton.Models;
using Graviton.Models.ROM;

using Playnite;

using System.Collections.ObjectModel;

namespace Graviton.Saves
{
    internal class DiscoveredSave : ObservableObject
    {
        internal RomMRomLocal? ROM { get; set; }
        internal string? Path { get; set; }
        internal List<string>? FilePaths { get; set; }
        internal int? Size { get; set; }
    }

    internal class DiscoveredSaves : ObservableObject
    {
        internal EmulatorMapping? EmulatorMapping { get; set; }
        internal ObservableCollection<DiscoveredSave> Saves { get; set; } = new();
    }

    public class SaveManagementContext : ObservableObject
    {
        internal ObservableCollection<RomMRomLocal> ROMs { get; set; }
        internal ObservableCollection<DiscoveredSaves>? DiscoveredSaves { get; set; }

        internal SaveManagementContext(List<RomMRomLocal> roms)
        {
            ROMs = roms.ToObservableCollection();
        }

    }
}
