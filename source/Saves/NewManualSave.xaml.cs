using Graviton.Models;
using Graviton.Models.RomM.Rom;

using Playnite;

using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Graviton.Saves
{
    public partial class NewManualSaveView : UserControl
    {
        public ObservableCollection<DirectorySaveFile> RootItems { get; } = new();
        public bool WasConfirmed { get; private set; }
        public List<string> SelectedSourcePaths { get; private set; } = new();

        public List<RomMRomLocal> ROMs { get; set; } = new();
        public RomMRomLocal? ROM { get; set; } = null;

        public NewManualSaveView()
        {
            InitializeComponent();
            DataContext = this;

            SelectGameText.Text = Loc.GetString("SelectGame");
            CancelText.Text = Loc.GetString("Cancel");
            SyncNewSaveText.Text = Loc.GetString("SyncNewSave");
        }

        public void LoadForPath(string savePath)
        {
            RootItems.Clear();
            TitleText.Text = Loc.GetString("SelectSaveFilesInPath", ("SavePath", savePath));

            if (!Directory.Exists(savePath))
                return;

            try
            {
                foreach (var dir in Directory.GetDirectories(savePath).OrderBy(d => d))
                    RootItems.Add(DirectorySaveFile.Build(dir, parent: null));

                foreach (var file in Directory.GetFiles(savePath).OrderBy(f => f))
                {
                    RootItems.Add(new DirectorySaveFile
                    {
                        Name = Path.GetFileName(file),
                        FullPath = file,
                        IsDirectory = false
                    });
                }
            }
            catch { }
        }

        private void SyncNewSave_Click(object sender, RoutedEventArgs e)
        {
            var sourcePaths = new List<string>();
            foreach (var root in RootItems)
                root.CollectSelectedTopLevelPaths(sourcePaths);

            SelectedSourcePaths = sourcePaths;
            WasConfirmed = true;
            Window.GetWindow(this)?.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            WasConfirmed = false;
            Window.GetWindow(this)?.Close();
        }

        private void SaveFileCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox { IsChecked: null } checkBox)
            {
                checkBox.IsChecked = false;
            }
        }
    }
}