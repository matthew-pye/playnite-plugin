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

            ROMs = GravitonPlugin.Instance.ImportedGames.Select(x => x.Value).ToList();
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
                ROMsComboBox.ItemsSource = ROMs.Where(x => x.MappingID == SelectedMapping.MappingId);

                try
                {
                    if (!Directory.Exists(SelectedMapping.SavePath))
                        throw new Exception();

                    RootItems = SaveDirectoryTree.BuildFromDisk(SelectedMapping.SavePath);
                    FileTree.ItemsSource = RootItems;
                }
                catch 
                {
                    FileTree.ItemsSource = null;
                    SelectedROM = null;
                    ROMsComboBox.ItemsSource = null;
                    NoFilesText.Visibility = Visibility.Visible;
                }

            }
            else
            {
                FileTree.ItemsSource = null;
                SelectedROM = null;
                ROMsComboBox.ItemsSource = null;
                
                NoMappingText.Visibility = Visibility.Visible;
            }
        }

        private async void SyncNewSave_Click(object sender, RoutedEventArgs e)
        {
            if(SelectedROM == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.ROM.null", Playnite.Loc.GetString("SaveNoGameSelected"), GravitonSeverity.Error));
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
            if(SelectedROM.LocalSave != null)
            {
                result = await GravitonPlugin.PlayniteApi.Dialogs.ShowMessageAsync(Playnite.Loc.GetString("OverwriteSaveConfirm", ("GameName", SelectedROM.Name!)), Playnite.Loc.GetString("OverwriteSaveTitle"), Playnite.MessageBoxButtons.YesNo);
                if(result == Playnite.MessageBoxResult.No)
                {
                    e.Handled = true;
                    return;
                }
            }

            result = await GravitonPlugin.PlayniteApi.Dialogs.ShowMessageAsync(Playnite.Loc.GetString("CreateSaveConfirm", ("GameName", SelectedROM.Name!), ("Paths", sourcePathsList)), Playnite.Loc.GetString("FilePaths"), Playnite.MessageBoxButtons.OKCancel);
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