using Playnite.SDK.Controls;

using RomM.Models.RomM.Rom;
using RomM.Settings;

using System.Collections.ObjectModel;
using System.Windows;

namespace RomM.Save
{
    public partial class RomMDeleteSaveView : PluginUserControl
    {
        public bool Local = false;
        public bool Remote = false;

        public RomMDeleteSaveView()
        {
            InitializeComponent();  
        }

        private void Click_Cancel(object sender, RoutedEventArgs e)
        {
            ((Window)Parent).Close();
        }

        private void Click_LocalOnly(object sender, RoutedEventArgs e)
        {
            var res = SettingsViewModel.Instance.PlayniteAPI.Dialogs.ShowMessage("Delete save locally, Are you sure?", "Confirm delete", MessageBoxButton.YesNo);
            if (res == MessageBoxResult.Yes)
            {
                Local = true;
                ((Window)Parent).Close();
            }

            
        }

        private void Click_RemoteOnly(object sender, RoutedEventArgs e)
        {
           
            var res = SettingsViewModel.Instance.PlayniteAPI.Dialogs.ShowMessage("Delete save from server, Are you sure?", "Confirm delete", MessageBoxButton.YesNo);
            if (res == MessageBoxResult.Yes)
            {
                Remote = true;
                ((Window)Parent).Close();
            }
        }

        private void Click_Delete(object sender, RoutedEventArgs e)
        {
           
            var res = SettingsViewModel.Instance.PlayniteAPI.Dialogs.ShowMessage("Delete save completely, Are you sure?", "Confirm delete", MessageBoxButton.YesNo);
            if (res == MessageBoxResult.Yes)
            {
                Local = true;
                Remote = true;
                ((Window)Parent).Close();
            }

        }
    }
}