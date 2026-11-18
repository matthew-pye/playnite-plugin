using System.Windows.Controls;

namespace Graviton.Saves
{
    public partial class SingleGameSaveManagement : UserControl
    {
        private GravitonPlugin _plugin { get => GravitonPlugin.Instance; }

        public SingleGameSaveManagement()
        {
            InitializeComponent();
        }

    }
}
