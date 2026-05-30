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

            if (GravitonSettingsHandler.Instance?.Settings.Mappings == null)
                return;

            foreach (var map in GravitonSettingsHandler.Instance.Settings.Mappings)
            {
                map.IsSelected = false;
            }

            var mapping = ((FrameworkElement)sender).DataContext as EmulatorMapping;

            if (mapping != null && GravitonSettingsHandler.Instance?.Settings.RomMPlatforms != null)
            {
                mapping.AvailablePlatforms = GravitonSettingsHandler.Instance.Settings.RomMPlatforms;
                MappingOptions.DataContext = mapping;
                MappingOptions.Visibility = Visibility.Visible;
                mapping.IsSelected = true;
            }
            else
            {
                MappingOptions.Visibility = Visibility.Hidden;
            }

           
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

        private async void DeleteMapping_Click(object sender, RoutedEventArgs e)
        {
            var mapping = ((FrameworkElement)sender).DataContext as EmulatorMapping;

            var response = await GravitonPlugin.PlayniteApi.Dialogs.ShowMessageAsync($"{mapping!.GetDescriptionLines()}", "Are you sure you want to delete this mapping?", Playnite.MessageBoxButtons.YesNoCancel);
            
            if(response == Playnite.MessageBoxResult.Yes)
            {
                GravitonSettingsHandler.Instance?.Settings.Mappings.Remove(mapping!);
                MappingOptions.DataContext = null;
                MappingOptions.Visibility = Visibility.Hidden;
            }
            
            e.Handled = true;
        }
    }
}
