using CommunityToolkit.Mvvm.ComponentModel;

using Graviton.Models;
using Graviton.Models.Saves;
using Graviton.Models.Saves.Discovery;

using System.Collections.ObjectModel;
using System.IO;

namespace Graviton.Saves
{
    public enum DiscoverOptions
    {
        Accept,
        ChooseGame,
        ChooseGamesCard

    }

    public partial class DiscoveredSaveContext : ObservableObject
    {
        [ObservableProperty] private string? _name;
        [ObservableProperty] private string? _description;

        [ObservableProperty] private DiscoverOptions _type = DiscoverOptions.Accept;

        [ObservableProperty] private int _candidateCount = 0;

        [ObservableProperty] private DiscoveredSave _discoveredSave = new();

        [ObservableProperty] private string? _generalPath;
        public string? ConflictGamesString => DiscoveredSave.Candidates.Count() > 1 ? $" · claimed by {DiscoveredSave.Candidates.Count()} games" : "";


        public DiscoveredSaveContext(EmulatorMapping mapping, DiscoveredSave save)
        {
            DiscoveredSave = save;

            if (save.Candidates.Count() == 1 && save.Type != SaveLayoutTypes.Card)
            {
                Name = save.Candidates[0].Rom.Name;
                Description = $"{save.Type} · {save.FileCount} file(s) · {BytesToUI(save.SizeBytes)}";
                Type = DiscoverOptions.Accept;

                var topPath = save.Paths.OrderBy(x => x.AsSpan().Count('\\')).FirstOrDefault();
                if(topPath?.StartsWith(mapping.SaveLocation!, StringComparison.OrdinalIgnoreCase) ?? false)
                {
                    topPath = topPath.Substring(mapping.SaveLocation!.Count());
                }

                if (save.Paths.Count() > 1 && save.Type != SaveLayoutTypes.Folder)
                {
                    topPath = Path.GetDirectoryName(topPath);
                }

                GeneralPath = topPath;
            }
            else if(save.Type == SaveLayoutTypes.Card)
            {
                Name = Path.GetFileNameWithoutExtension(save.Paths[0]);
                Description = $"{save.Type} · {save.FileCount} file(s) · {BytesToUI(save.SizeBytes)}";
                Type = DiscoverOptions.ChooseGamesCard;
                CandidateCount = save.Candidates.Count();

                var topPath = save.Paths[0];
                if (topPath?.StartsWith(mapping.SaveLocation!, StringComparison.OrdinalIgnoreCase) ?? false)
                {
                    topPath = topPath.Substring(mapping.SaveLocation!.Count());
                }

                GeneralPath = topPath;
            }
            else if(save.Candidates.Count() > 1)
            {
                Name = "";
                Description = $"{save.Type} · {save.FileCount} file(s) · {BytesToUI(save.SizeBytes)}";
                Type = DiscoverOptions.ChooseGame;
                CandidateCount = save.Candidates.Count();

                var topPath = save.Paths.OrderBy(x => x.AsSpan().Count('\\')).FirstOrDefault();
                if (topPath?.StartsWith(mapping.SaveLocation!, StringComparison.OrdinalIgnoreCase) ?? false)
                {
                    topPath = topPath.Substring(mapping.SaveLocation!.Count());
                }

                if (save.Paths.Count() > 1 && save.Type != SaveLayoutTypes.Folder)
                {
                    topPath = Path.GetDirectoryName(topPath);
                }

                GeneralPath = topPath;
            }
        }

        private string BytesToUI(long bytes)
        {
            if (bytes <= 0)
                return Playnite.Loc.GetString("Unknown");

            if (bytes < 1000)
            {
                return $"{bytes}B";
            }
            else if (bytes < 1000000)
            {
                return $"{((float)bytes / 1000).ToString("F1")}KB";
            }
            else
            {
                return $"{((float)bytes / 1000000).ToString("F1")}MB";
            }
        }

    }

    public partial class DiscoveredSavesContext : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<DiscoveredSaveContext> _discoveredSaves = new();

        [ObservableProperty] private EmulatorMapping _mapping;

        public DiscoveredSavesContext(EmulatorMapping mapping, List<DiscoveredSave> saves)
        {
            Mapping = mapping;

            foreach (var save in saves)
            {
                DiscoveredSaves.Add(new DiscoveredSaveContext(mapping, save));
            }

        }

        public void DiscoveredSaveUpdated(DiscoveredSaveContext saveContext)
        {
            // TODO update other contexts when the save context is updated like a rom has accepted a save so should be removed as a candidate from other contexts (?)
        }
    }

}