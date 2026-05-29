using Graviton.Models.Notifications;

using Playnite;

using System.Windows.Controls;

namespace Graviton.Settings
{
    /// <summary>
    /// Interaction logic for GravitonSettingsView.xaml
    /// </summary>
    public partial class SettingsTabControl : UserControl
    {
        public SettingsTabControl()
        {
            InitializeComponent();
            SettingsTabs.FontFamily = Playnite.Fonts.NerdFont;

#if DEBUG
            DEVTAB.Visibility = System.Windows.Visibility.Visible;
#else
            DEVTAB.Visibility = System.Windows.Visibility.Collapsed;
#endif
        }

        private async void MappingTabItem_Selected(object sender, System.Windows.RoutedEventArgs e)
        {
            //foreach (var mapping in GravitonSettingsHandler.Instance?.Settings.Mappings!)
            //{
            //    mapping.AvailablePlatforms = GravitonSettingsHandler.Instance.Settings.RomMPlatforms;
            //}


                //var importcontroller = GravitonPlugin.Instance?.ImportController;
                //
                //if(importcontroller == null)
                //{
                //    e.Handled = true;
                //    return;
                //}
                //
                //var platforms = await importcontroller.FetchPlatforms();
                //if (platforms == null)
                //{
                //    e.Handled = true;
                //    return;
                //}
                //else if(platforms.Count <= 0)
                //{
                //    GravitonNotify.Add(new GravitonNotification("graviton.GET.no.platforms", $"No platforms pulled from server!", GravitonSeverity.Warn));
                //    e.Handled = true;
                //    return;
                //}
                //
                //GravitonNotify.Add(new GravitonNotification("graviton.GET.platforms", $"Pulled {platforms.Count} platforms from server", GravitonSeverity.Success));
                //
                //GravitonSettingsHandler.Instance?.Settings.RomMPlatforms = platforms.ToObservableCollection();
                //foreach (var mapping in GravitonSettingsHandler.Instance?.Settings.Mappings!)
                //{
                //    mapping.AvailablePlatforms = platforms;
                //}
                //
                //e.Handled = true;
        }
    }
}
