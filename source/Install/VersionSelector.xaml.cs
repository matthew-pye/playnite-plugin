using Graviton.Models.RomM.Rom;

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Graviton.Install
{
    public partial class RomMVersionSelector : UserControl
    {

        public ObservableCollection<GameInstallInfo> RomVersions { get; set; }
        public bool Cancelled { get; set; } = true;

        public RomMVersionSelector(List<GameInstallInfo> romVersions)
        {
            RomVersions = new ObservableCollection<GameInstallInfo>(romVersions);
            InitializeComponent();  
        }

        private void Click_Cancel(object sender, RoutedEventArgs e)
        {
            ((Window)Parent).Close();
        }

        private void Click_Install(object sender, RoutedEventArgs e)
        {
            Cancelled = false;
            ((Window)Parent).Close();
        }
    }
}