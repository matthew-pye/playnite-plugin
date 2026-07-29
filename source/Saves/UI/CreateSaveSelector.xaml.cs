using Graviton.Models;
using Graviton.Models.Notifications;
using Graviton.Models.RomM.Rom;
using Graviton.Models.Saves;

using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Graviton.Saves
{
    public partial class CreateSaveSelector : UserControl
    {
        public ObservableCollection<SaveDirectoryTree> RootItems = new();
        
        public List<RomMRomLocal> ROMs { get; set; }
        public List<EmulatorMapping> Mappings { get; set; }
        public EmulatorMapping? SelectedMapping { get; set; }

        public RomMRomLocal? SelectedROM { get; set; }
        public List<string>? SelectedSourcePaths; 

        public bool WasConfirmed = false;

        public CreateSaveSelector()
        {
            InitializeComponent();

            ROMs = GravitonPlugin.Instance.ImportedGames!.Select(x => x.Value).ToList();
            Mappings = GravitonPlugin.Instance.Settings.Mappings.ToList();

            MainGrid.DataContext = this;
        }

        public CreateSaveSelector(List<RomMRomLocal> roms)
        {
            InitializeComponent();

            ROMs = roms;
            List<Guid> mappingIDs = new();
            foreach (var rom in ROMs)
            {
                if(!mappingIDs.Any(x => x == rom.MappingID))
                {
                    mappingIDs.Add(rom.MappingID);
                }
            }

            Mappings = new();

            foreach (var mappingID in mappingIDs)
            {
                var mapping = GravitonPlugin.Instance.Settings.Mappings.FirstOrDefault(x => x.MappingId == mappingID);
                if (mapping != null)
                    Mappings.Add(mapping);
            }

            if (Mappings.Count == 1)
                SelectedMapping = Mappings[0];

            MainGrid.DataContext = this;
        }

        private async Task SwitchedMapping()
        {
            NoMappingText.Visibility = Visibility.Collapsed;
            NoFilesText.Visibility = Visibility.Collapsed;

            if (SelectedMapping != null)
            {
                ROMsComboBox.DataContext = ROMs.Where(x => x.MappingID == SelectedMapping.MappingId);

                try
                {
                    if (!Directory.Exists(SelectedMapping.SavePath))
                        throw new Exception();

                    RootItems = SaveDirectoryTree.BuildFromDisk(SelectedMapping.SavePath);
                    FileTree.ItemsSource = RootItems;
                }
                catch 
                {
                    FileTree.DataContext = null;
                    SelectedROM = null;
                    ROMsComboBox.DataContext = null;
                    NoFilesText.Visibility = Visibility.Visible;
                }

            }
            else
            {
                FileTree.DataContext = null;
                SelectedROM = null;
                ROMsComboBox.DataContext = null;
                
                NoMappingText.Visibility = Visibility.Visible;
            }
        }

        private async void SyncNewSave_Click(object sender, RoutedEventArgs e)
        {
            if(SelectedROM == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.ROM.null", "No game has been selected, cannot create new save", GravitonSeverity.Error));
                e.Handled = true;
                return;
            }

            var sourcePaths = new List<string>();
            foreach (var root in RootItems)
                root.CollectSelectedTopLevelPaths(sourcePaths);

            string sourcePathsList = "\t";
            foreach (var path in sourcePaths)
            {
                sourcePathsList += path + "\n\t";
            }

            Playnite.MessageBoxResult result;
            if(SelectedROM.LocalSave.SaveID != -1)
            {
                result = await GravitonPlugin.PlayniteApi.Dialogs.ShowMessageAsync($"{SelectedROM.Name} already has a tracked save, do you want to overwrite it?", "File Paths", Playnite.MessageBoxButtons.YesNo);
                if(result == Playnite.MessageBoxResult.No)
                {
                    e.Handled = true;
                    return;
                }
            }

            result = await GravitonPlugin.PlayniteApi.Dialogs.ShowMessageAsync($"Creating a new save for {SelectedROM.Name}?\nPaths:\n{sourcePathsList}", "File Paths", Playnite.MessageBoxButtons.OKCancel);
            if(result == Playnite.MessageBoxResult.OK)
            {
                SelectedSourcePaths = sourcePaths;
                WasConfirmed = true;
                Window.GetWindow(this)?.Close();
            }

            e.Handled = true;

        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this)?.Close();
        }

        private void SaveFileCheckBox_Click(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox != null && checkBox.IsChecked == null)
            {
                checkBox.IsChecked = false;
            }
        }

        private void MappingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _ = SwitchedMapping();
            e.Handled = true;
        }
    }
}