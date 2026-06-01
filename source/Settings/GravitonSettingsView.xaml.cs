using Playnite;

using Graviton.Models;
using Graviton.Models.RomM.Platform;

using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace Graviton.Settings
{
    /// <summary>
    /// Interaction logic for GravitonSettingsView.xaml
    /// </summary>
    public partial class GravitonSettingsView : UserControl
    {
        private GravitonPlugin _plugin { get => GravitonPlugin.Instance; }

        public GravitonSettingsView()
        {
            InitializeComponent();
        }

        private async void Click_PullPlatforms(object sender, RoutedEventArgs e)
        {

            try
            {
                HttpResponseMessage response = await HttpClientSingleton.Instance.GetAsync($"{_plugin.Settings.Host}/api/platforms");
                response.EnsureSuccessStatusCode();

                string body = await response.Content.ReadAsStringAsync();
                 _plugin.Settings.RomMPlatforms = JsonSerializer.Deserialize<ObservableCollection<RomMPlatform>>(body) ?? throw new Exception("Failed to deserialize RomM platforms!");
            }
            catch (Exception ex)
            {
                LogManager.GetLogger().Error($"RomM - failed to get platforms: {ex}");
            }
        }

        private void Click_AddMapping(object sender, RoutedEventArgs e)
        {
            _plugin.Settings.Mappings.Add(new EmulatorMapping(_plugin.Settings.RomMPlatforms));
        }

        private void Click_BrowseDestination(object sender, RoutedEventArgs e)
        {
            var mapping = ((FrameworkElement)sender).DataContext as EmulatorMapping;
            string path;
            if ((path = GetSelectedFolderPath()) == null) return;
            //var playnite =  _plugin.Settings.PlayniteAPI;
            //if (playnite.Paths.IsPortable)
            //{
            //    path = path.Replace(playnite.Paths.ApplicationPath, Playnite.SDK.ExpandableVariables.PlayniteDirectory);
            //}

            mapping?.DestinationPath = path;
        }

        private static string GetSelectedFolderPath()
        {
            var FolderPath = GravitonPlugin.PlayniteApi?.Dialogs.SelectFolderAsync().GetAwaiter().GetResult();
            if(FolderPath?[0] != null)
            {
                return FolderPath[0];
            }
            return string.Empty;
        }

        private void Click_Browse7zDestination(object sender, RoutedEventArgs e)
        {

            
        }
    }
}
