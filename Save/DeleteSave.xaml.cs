using Playnite.SDK.Controls;

using RomM.Models.RomM.Rom;

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
            Local = true;
            ((Window)Parent).Close();
        }

        private void Click_RemoteOnly(object sender, RoutedEventArgs e)
        {
            Remote = true;
            ((Window)Parent).Close();
        }

        private void Click_Delete(object sender, RoutedEventArgs e)
        {
            Local = true;
            Remote = true;
            ((Window)Parent).Close();
        }
    }
}