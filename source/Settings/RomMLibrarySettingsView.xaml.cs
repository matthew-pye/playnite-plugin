using Playnite;

using RomMLibrary.Models;
using RomMLibrary.Models.RomM.Platform;

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace RomMLibrary.Settings
{
    /// <summary>
    /// Interaction logic for RomMLibrarySettingsView.xaml
    /// </summary>
    public partial class RomMLibrarySettingsView : UserControl
    {
        Dictionary<string, string[]> PathTo7zFileType = new Dictionary<string, string[]>();

        public RomMLibrarySettingsView()
        {
            PathTo7zFileType.Add("7Zip Executable", ["7z.exe"]);
            InitializeComponent();
        }

        private void Click_Authenticate(object sender, RoutedEventArgs e)
        { 
            RomMLibrarySettingsHandler.Instance?.Authenticate();
            e.Handled = true;
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                if (e.Uri.Scheme == Uri.UriSchemeHttp || e.Uri.Scheme == Uri.UriSchemeHttps)
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = e.Uri.AbsoluteUri,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open URL: {ex.Message}");
            }
            e.Handled = true;
        }

        private async void Click_PullPlatforms(object sender, RoutedEventArgs e)
        {
            RomMLibrarySettingsHandler.Instance?.Settings.PlatformSynced = false;
            RomMLibrarySettingsHandler.Instance?.Settings.PlatformSyncFailed = false;

            try
            {
                HttpResponseMessage response = await HttpClientSingleton.Instance.GetAsync($"{RomMLibrarySettingsHandler.Instance?.Settings.Host}/api/platforms");
                response.EnsureSuccessStatusCode();

                string body = await response.Content.ReadAsStringAsync();
                 RomMLibrarySettingsHandler.Instance?.Settings.RomMPlatforms = JsonSerializer.Deserialize<ObservableCollection<RomMPlatform>>(body) ?? throw new Exception("Failed to deserialize RomM platforms!");
                 RomMLibrarySettingsHandler.Instance?.Settings.PlatformSynced = true;
            }
            catch (Exception ex)
            {
                LogManager.GetLogger().Error($"RomM - failed to get platforms: {ex}");
                 RomMLibrarySettingsHandler.Instance?.Settings.PlatformSyncFailed = true;
            }
        }

        private void Click_AddMapping(object sender, RoutedEventArgs e)
        {
            RomMLibrarySettingsHandler.Instance?.Settings.Mappings.Add(new EmulatorMapping(RomMLibrarySettingsHandler.Instance?.Settings.RomMPlatforms));
        }

        private void Click_Delete(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is EmulatorMapping mapping)
            {
                var res =  RomMLibraryPlugin.PlayniteApi?.Dialogs.ShowMessageAsync(string.Format("Delete this mapping?\r\n\r\n{0}", mapping.GetDescriptionLines().Aggregate((a, b) => $"{a}{Environment.NewLine}{b}")), "Confirm delete", MessageBoxButtons.YesNo);
                if (res?.Result == Playnite.MessageBoxResult.Yes)
                {
                     RomMLibrarySettingsHandler.Instance?.Settings.Mappings.Remove(mapping);
                }
            }
        }

        private void Click_BrowseDestination(object sender, RoutedEventArgs e)
        {
            var mapping = ((FrameworkElement)sender).DataContext as EmulatorMapping;
            string path;
            if ((path = GetSelectedFolderPath()) == null) return;
            //var playnite =  RomMLibrarySettingsHandler.Instance?.Settings.PlayniteAPI;
            //if (playnite.Paths.IsPortable)
            //{
            //    path = path.Replace(playnite.Paths.ApplicationPath, Playnite.SDK.ExpandableVariables.PlayniteDirectory);
            //}

            mapping?.DestinationPath = path;
        }

        private static string GetSelectedFolderPath()
        {
            var FolderPath = RomMLibraryPlugin.PlayniteApi?.Dialogs.SelectFolderAsync().GetAwaiter().GetResult();
            if(FolderPath?[0] != null)
            {
                return FolderPath[0];
            }
            return string.Empty;
        }

        private void Click_Browse7zDestination(object sender, RoutedEventArgs e)
        {

            var path = RomMLibraryPlugin.PlayniteApi?.Dialogs.SelectFileAsync(PathTo7zFileType, false).GetAwaiter().GetResult();

            if (path?[0] == null) return;

             RomMLibrarySettingsHandler.Instance?.Settings.PathTo7z = path[0];
            e.Handled = true;
        }
    }
}
