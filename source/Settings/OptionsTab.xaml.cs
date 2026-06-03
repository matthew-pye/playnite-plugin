using Playnite;

using System.Windows;
using System.Windows.Controls;

namespace Graviton.Settings
{
    /// <summary>
    /// Interaction logic for GravitonSettingsView.xaml
    /// </summary>
    public partial class OptionsTab : UserControl
    {
        private GravitonPlugin _plugin { get => GravitonPlugin.Instance; }

        Dictionary<string, string[]> PathTo7zFileType = new()
        {
            { "7Zip Executable", ["7z.exe"]}
        };

        public OptionsTab()
        {
            InitializeComponent();

            LibraryScanningText.Text = Loc.GetString("LibraryScanning");
            MergeROMText.Text = Loc.GetString("MergeROMs");
            KeepDeleteText.Text = Loc.GetString("KeepDeleted");
            SkipDeletedText.Text = Loc.GetString("SkipDeleted");
            ExcludeGenresText.Text = Loc.GetString("ExcludeGenres");
            InstallationText.Text = Loc.GetString("Installation");
            Use7zText.Text = Loc.GetString("Use7z");
            Browse7zText.Text = Loc.GetString("Browse");
            StatusTitle.Text = Loc.GetString("StatusSync");
            KeepStatusSyncedText.Text = Loc.GetString("KeepStatusSynced");

        }

        private async void Browse7zPath_Click(object sender, RoutedEventArgs e)
        {
            var path = await GravitonPlugin.PlayniteApi.Dialogs.SelectFileAsync(PathTo7zFileType, false);

            if (path?[0] == null) return;

            _plugin.Settings.PathTo7z = path[0];
            e.Handled = true;
        }
    }
}
