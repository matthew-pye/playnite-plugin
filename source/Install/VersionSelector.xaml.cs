using RomMLibrary.Models.RomM.Rom;

using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace RomMLibrary.Install
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
    }
}