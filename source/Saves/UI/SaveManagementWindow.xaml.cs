using Graviton.Models.ROM;

using System.Windows.Controls;

namespace Graviton.Saves
{
    public partial class SaveManagementWindow : UserControl
    {
        public SaveManagementWindow(List<RomMRomLocal> ROMs)
        {
            InitializeComponent();

            _ = SaveManagementContent.Load(ROMs);
        }
        public SaveManagementWindow()
        {
            InitializeComponent();

            _ = SaveManagementContent.Load();
        }
    }
}
