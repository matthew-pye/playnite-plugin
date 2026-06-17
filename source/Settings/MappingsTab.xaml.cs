using Graviton.Models;
using Graviton.Models.Notifications;

using Playnite;

using System.Windows;
using System.Windows.Controls;

namespace Graviton.Settings
{
    /// <summary>
    /// Interaction logic for GravitonSettingsView.xaml
    /// </summary>
    public partial class MappingsTab : UserControl
    {
        private GravitonPlugin _plugin { get => GravitonPlugin.Instance; }

        public MappingsTab()
        {
            InitializeComponent();

            SyncPlatformsIcon.Text = $"\uf46a {Loc.GetString("SyncPlatforms")}";
            AddMappingIcon.Text = $"\uea60 {Loc.GetString("NewMapping")}";
            EmulatorText.Text = Loc.GetString("Emulator");
            ProfileText.Text = Loc.GetString("Profile");
            PlatformText.Text = Loc.GetString("Platform");
            ROMLocText.Text = Loc.GetString("ROMLoc");
            ROMLocButtonText.Text = Loc.GetString("Browse");
            OptionText.Text = Loc.GetString("Options");
            AutoExtractROMText.Text = Loc.GetString("AutoExtractROMs");
            Preferm3uText.Text = Loc.GetString("PreferM3U");

            AddMappingIcon.FontFamily = Playnite.Fonts.NerdFont;
        }

        private void AddMapping_Click(object sender, RoutedEventArgs e)
        {
            _plugin.Settings.Mappings.Add(new EmulatorMapping(_plugin.Settings.RomMPlatforms));
        }

        private void Mapping_Click(object sender, RoutedEventArgs e)
        {

            if (_plugin.Settings.Mappings == null)
                return;

            foreach (var map in _plugin.Settings.Mappings)
            {
                map.IsSelected = false;
            }

            var mapping = ((FrameworkElement)sender).DataContext as EmulatorMapping;

            if (mapping != null && _plugin.Settings.RomMPlatforms != null)
            {
                mapping.AvailablePlatforms = _plugin.Settings.RomMPlatforms;
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
            if(mapping != null)
            {
                var response = await GravitonPlugin.PlayniteApi.Dialogs.ShowMessageAsync($"{mapping.GetDescriptionLines()}", "Are you sure you want to delete this mapping?", Playnite.MessageBoxButtons.YesNoCancel);

                if (response == Playnite.MessageBoxResult.Yes)
                {
                    _plugin.Settings.Mappings.Remove(mapping!);
                    MappingOptions.DataContext = null;
                    MappingOptions.Visibility = Visibility.Hidden;
                }
            }
            
            e.Handled = true;
        }

        private async void SyncPlatforms_Click(object sender, RoutedEventArgs e)
        {
            await _plugin.Account!.SyncPlatforms();
            e.Handled = true;
        }
    }
}
