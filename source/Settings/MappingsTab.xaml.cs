using Graviton.Models;
using Graviton.Models.Notifications;
using Graviton.Saves;

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
            _plugin.Settings.Mappings.Add(new EmulatorMapping(_plugin.Settings.AccountState.RomMPlatforms));
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

            if (mapping != null && _plugin.Settings.AccountState.RomMPlatforms != null)
            {
                mapping.AvailablePlatforms = _plugin.Settings.AccountState.RomMPlatforms;
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
            if(await _plugin.Account!.SyncPlatforms())
                GravitonNotify.Add(new GravitonNotification("graviton.GET.platforms", Loc.GetString("PlatformsSynced", [("PlaformCount", _plugin.Settings.AccountState.RomMPlatforms.Count)]), GravitonSeverity.Success));
            
            e.Handled = true;
        }

        private async void Click_BrowseSaveDestination(object sender, RoutedEventArgs e)
        {
            var mapping = ((FrameworkElement)sender).DataContext as EmulatorMapping;
            var path = await GravitonPlugin.PlayniteApi.Dialogs.SelectFolderAsync();

            if (mapping != null && path != null)
                mapping.SavePath = path[0];

            e.Handled = true;
        }

        private async void Click_BrowseSaveStateDestination(object sender, RoutedEventArgs e)
        {
            var mapping = ((FrameworkElement)sender).DataContext as EmulatorMapping;
            var path = await GravitonPlugin.PlayniteApi.Dialogs.SelectFolderAsync();

            if (mapping != null && path != null)
                mapping.SaveStatePath = path[0];

            e.Handled = true;
        }

        private void OpenSaveManager_Click(object sender, RoutedEventArgs e)
        {
            var mapping = ((FrameworkElement)sender).DataContext as EmulatorMapping;
            if (mapping == null) return;

            var tab = new MappingSaveTab();
            tab.Load(mapping);

            var window = new SaveManagerWindow(Loc.GetString("SaveManagerTitle"), tab);
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.ShowDialog();
        }
    }
}
