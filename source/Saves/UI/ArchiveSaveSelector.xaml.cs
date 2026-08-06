using Graviton.Models;
using Graviton.Models.Notifications;
using Graviton.Models.RomM.Rom;
using Graviton.Models.RomM.Saves;
using Graviton.Models.Saves;

using Playnite;

using System.Windows;
using System.Windows.Controls;

namespace Graviton.Saves
{ 

    public partial class ArchiveSaveSelector : UserControl
    {
        public GravitonSave? NewSave;

        List<RomMRomLocal>? ROMs;
        EmulatorMapping? Mapping;

        private SaveController SaveController => GravitonPlugin.Instance.SaveController!;

        public ArchiveSaveSelector()
        {
            InitializeComponent();
            _ = Load();
        }
        public ArchiveSaveSelector(EmulatorMapping mapping)
        {
            InitializeComponent();

            Mapping = mapping;
            _ = Load();
        }
        public ArchiveSaveSelector(List<RomMRomLocal> roms)
        {
            InitializeComponent();

            ROMs = roms;
            _ = Load();
        }

        public async Task Load()
        {
            LoadingBar.Visibility = Visibility.Visible;

            List<RomMSave>? saves;

            if (ROMs != null)
            {
                saves = await SaveController.Discover.GetArchivedSaves(ROMs);
            }
            else if (Mapping != null)
            {
                saves = await SaveController.Discover.GetArchivedSaves(Mapping);
            }
            else
            {
                saves = await SaveController.Discover.GetArchivedSaves();
            }

            if (saves == null || saves.Count < 1)
            {
                NoSavesText.Visibility = Visibility.Visible;
            }
            else
            {
                SavesItemControl.ItemsSource = saves;
            }

            LoadingBar.Visibility = Visibility.Collapsed;
        }

        private async void DownloadArchiveSave_Click(object sender, RoutedEventArgs e)
        {
            var save = ((FrameworkElement)sender).DataContext as RomMSave;
            if (save == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.save.null", Loc.GetString("SaveIsNull"), GravitonSeverity.Error));
                e.Handled = true;
                return;
            }

            var result = await GravitonPlugin.PlayniteApi.Dialogs.ShowMessageAsync(Loc.GetString("TrackArchivedSaveConfirm"), Loc.GetString("TrackArchivedSave"), MessageBoxButtons.YesNo);
            if (result == Playnite.MessageBoxResult.Yes)
            {
                NewSave = await SaveController.Manager.DownloadArchivedSave(save);

                if(NewSave != null)
                    Window.GetWindow(this)?.Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this)?.Close();
        }
    }
}