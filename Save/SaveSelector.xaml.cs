using Playnite.SDK.Controls;

using RomM.Models.RomM.Rom;

using System.Collections.ObjectModel;
using System.Windows;

namespace RomM.Save
{
    public partial class RomMSaveSelector : PluginUserControl
    {
        public ObservableCollection<PossibleSave> Saves { get; set; }

        public bool Cancelled { get; set; } = true;
        public bool NeverSave { get; set; } = false;

        public RomMSaveSelector(ObservableCollection<PossibleSave> saves)
        {
            Saves = saves;
            InitializeComponent();  
        }

        private void Click_Never(object sender, RoutedEventArgs e)
        {
            NeverSave = true;
            ((Window)Parent).Close();
        }

        private void Click_DontSave(object sender, RoutedEventArgs e)
        {
            ((Window)Parent).Close();
        }

        private void Click_Save(object sender, RoutedEventArgs e)
        {
            Cancelled = false;
            ((Window)Parent).Close();
        }
    }
}