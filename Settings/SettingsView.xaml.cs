using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playnite.SDK;
using RomM.Models.RomM;
using RomM.Models.RomM.Platform;
using RomM.Models.RomM.Rom;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;

namespace RomM.Settings
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        private void Click_TestConnection(object sender, RoutedEventArgs e)
        {
            SettingsViewModel.Instance.TestConnection(true);
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
            SettingsViewModel.Instance.Notify = false;

            try
            {
                HttpResponseMessage response = await HttpClientSingleton.Instance.GetAsync($"{SettingsViewModel.Instance.RomMHost}/api/platforms");
                response.EnsureSuccessStatusCode();

                string body = await response.Content.ReadAsStringAsync();
                SettingsViewModel.Instance.RomMPlatforms = JsonConvert.DeserializeObject<List<RomMPlatform>>(body);
                SettingsViewModel.Instance.UpdateNotifcationBar("Platforms successfully retrieved!");
            }
            catch (Exception ex)
            {
                LogManager.GetLogger().Error($"RomM - failed to get platforms: {ex}");
                SettingsViewModel.Instance.UpdateNotifcationBar($"Failed to get platforms: {ex.Message}!", true);
            }
        }

        private void Click_AddMapping(object sender, RoutedEventArgs e)
        {
            SettingsViewModel.Instance.Notify = false;
            SettingsViewModel.Instance.Mappings.Add(new EmulatorMapping(SettingsViewModel.Instance.RomMPlatforms));
        }

        private void Click_Delete(object sender, RoutedEventArgs e)
        {
            SettingsViewModel.Instance.Notify = false;
            if (((FrameworkElement)sender).DataContext is EmulatorMapping mapping)
            {
                var res = SettingsViewModel.Instance.PlayniteAPI.Dialogs.ShowMessage(string.Format("Delete this mapping?\r\n\r\n{0}", mapping.GetDescriptionLines().Aggregate((a, b) => $"{a}{Environment.NewLine}{b}")), "Confirm delete", MessageBoxButton.YesNo);
                if (res == MessageBoxResult.Yes)
                {
                    SettingsViewModel.Instance.Mappings.Remove(mapping);
                }
            }
        }

        private void Click_BrowseDestination(object sender, RoutedEventArgs e)
        {
            SettingsViewModel.Instance.Notify = false;
            var mapping = ((FrameworkElement)sender).DataContext as EmulatorMapping;
            string path;
            if ((path = GetSelectedFolderPath()) == null) return;
            var playnite = SettingsViewModel.Instance.PlayniteAPI;
            if (playnite.Paths.IsPortable)
            {
                path = path.Replace(playnite.Paths.ApplicationPath, Playnite.SDK.ExpandableVariables.PlayniteDirectory);
            }

            mapping.DestinationPath = path;
        }

        private static string GetSelectedFolderPath()
        {
            SettingsViewModel.Instance.Notify = false;
            return SettingsViewModel.Instance.PlayniteAPI.Dialogs.SelectFolder();
        }

        private void Click_Browse7zDestination(object sender, RoutedEventArgs e)
        {
            SettingsViewModel.Instance.Notify = false;
            string path;
            if ((path = SettingsViewModel.Instance.PlayniteAPI.Dialogs.SelectFile("7Zip Executable|7z.exe")) == null) return;

            SettingsViewModel.Instance.PathTo7z = path;
            e.Handled = true;
        }

        private void Click_SyncSaves(object sender, RoutedEventArgs e)
        {
            SettingsViewModel.Instance.Notify = false;
            SettingsViewModel.Instance.SaveController.SyncRemoteSaves(true);
        }

        private void Click_SaveDirectory(object sender, RoutedEventArgs e)
        {
            SettingsViewModel.Instance.Notify = false;
            var mapping = ((FrameworkElement)sender).DataContext as EmulatorMapping;

            if (mapping != null)
            {
                string path = SettingsViewModel.Instance.PlayniteAPI.Dialogs.SelectFolder();
                if (string.IsNullOrEmpty(path)) 
                    return;

                mapping.GeneralSavePath = path;
                SettingsViewModel.Instance.SaveController.SyncPotentialSaves();
            }

            e.Handled = true;
        }

        private void LostKeyboard_SaveExtensionsBox(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            SettingsViewModel.Instance.Notify = false;
            SettingsViewModel.Instance.SaveController.CurrentMapping.SaveFileExtensions = SaveExtentionTextBox.Text;
            SettingsViewModel.Instance.SaveController.SyncPotentialSaves();
            e.Handled = true;
        }

        private void LostKeyboard_GeneralSavePath(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            SettingsViewModel.Instance.Notify = false;
            var mapping = ((FrameworkElement)sender).DataContext as EmulatorMapping;

            if (mapping != null)
            {
                if (string.IsNullOrEmpty(mapping.GeneralSavePath))
                    return;

                SettingsViewModel.Instance.SaveController.SyncPotentialSaves();
            }

            e.Handled = true;
        }

        private void Click_BrowseSavefile(object sender, RoutedEventArgs e)
        {
            SettingsViewModel.Instance.Notify = false;
            var save = ((FrameworkElement)sender).DataContext as RomMSave;

            string path = SettingsViewModel.Instance.PlayniteAPI.Dialogs.SelectFolder(SettingsViewModel.Instance.SaveController.CurrentMapping.GeneralSavePath);
            if (string.IsNullOrEmpty(path))
                return;

            if(!path.StartsWith(SettingsViewModel.Instance.SaveController.CurrentMapping.GeneralSavePath))
            {
                SettingsViewModel.Instance.UpdateNotifcationBar("Selected folder is not in the general save directory!", true);
                return;
            }

            save.SaveFolder = path;
            e.Handled = true;
        }

        private void Click_SyncNow(object sender, RoutedEventArgs e)
        {
            SettingsViewModel.Instance.Notify = false;
            var save = ((FrameworkElement)sender).DataContext as RomMSave;
            SettingsViewModel.Instance.SaveController.SyncSave(save, SettingsViewModel.Instance.SaveController.CurrentMapping.MappingId);
            e.Handled = true;
        }

        private void Checked_RemoteEnableSync(object sender, RoutedEventArgs e)
        {
            SettingsViewModel.Instance.Notify = false;
            var save = ((FrameworkElement)sender).DataContext as RomMSave;
            SettingsViewModel.Instance.SaveController.RemoteSaveEnabled(save);
            e.Handled = true;
        }

        private void Checked_LocalEnableSync(object sender, RoutedEventArgs e)
        {
            SettingsViewModel.Instance.Notify = false;
            var save = ((FrameworkElement)sender).DataContext as RomMSave;
            SettingsViewModel.Instance.SaveController.LocalSaveEnabled(save);
            e.Handled = true;
        }

        private void Click_RemoveSave(object sender, RoutedEventArgs e)
        {
            SettingsViewModel.Instance.Notify = false;
            var save = ((FrameworkElement)sender).DataContext as RomMSave;
            SettingsViewModel.Instance.SaveController.RemoveSaveEntry(save);
            e.Handled = true;
        }

        private void Click_DeleteSave(object sender, RoutedEventArgs e)
        {
            SettingsViewModel.Instance.Notify = false;
            var save = ((FrameworkElement)sender).DataContext as RomMSave;
            SettingsViewModel.Instance.SaveController.DeleteSave(save);
            e.Handled = true;
        } 

        private void Click_UploadSave(object sender, RoutedEventArgs e)
        {
            SettingsViewModel.Instance.Notify = false;
            var possiblesave = ((FrameworkElement)sender).DataContext as PossibleSave;
            SettingsViewModel.Instance.SaveController.UploadNewSave(possiblesave);
            e.Handled = true;
        }
    }
}
