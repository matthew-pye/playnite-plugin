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

            OptionsPanel.IsEnabled = !GameSessionHandler.IsAGameRunning;
        }

        private async void Browse7zPath_Click(object sender, RoutedEventArgs e)
        {
            var path = await GravitonPlugin.PlayniteApi.Dialogs.SelectFileAsync(PathTo7zFileType, false);

            if (path == null || path.Count == 0) 
                return;

            _plugin.Settings.PathTo7z = path[0];
            e.Handled = true;
        }
    }
}
