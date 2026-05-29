using Graviton.Models;

using System.Windows;
using System.Windows.Controls;

namespace Graviton.Settings
{
    /// <summary>
    /// Interaction logic for GravitonSettingsView.xaml
    /// </summary>
    public partial class MappingsTab : UserControl
    {

        GravitonPluginSettings Settings { get => GravitonSettingsHandler.Instance?.Settings ?? throw new Exception("Seetings is null, cannot continue!"); }

        public MappingsTab()
        {
            InitializeComponent();
            AddMappingIcon.FontFamily = Playnite.Fonts.NerdFont;
        }

        private void AddMapping_Click(object sender, RoutedEventArgs e)
        {
            Settings.Mappings.Add(new EmulatorMapping(Settings.RomMPlatforms));
        }

        private void Mapping_Click(object sender, RoutedEventArgs e)
        {
            var mapping = ((FrameworkElement)sender).DataContext as EmulatorMapping;
            MappingOptions.DataContext = mapping;
            MappingOptions.Visibility = Visibility.Visible;
            e.Handled = true;
        }

        private async void Click_BrowseDestination(object sender, RoutedEventArgs e)
        {
            var mapping = ((FrameworkElement)sender).DataContext as EmulatorMapping;
            var path = await GravitonPlugin.PlayniteApi.Dialogs.SelectFolderAsync();

            if (mapping != null && path != null)
                mapping.DestinationPath = path[0];

            e.Handled = true;
        }
    }
}
