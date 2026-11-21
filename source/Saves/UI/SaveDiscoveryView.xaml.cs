using Graviton.Models;

using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace Graviton.Saves
{
    public partial class SaveDiscoveryView : UserControl
    {
        private GravitonPlugin _plugin { get => GravitonPlugin.Instance; }

        // Save contexts grouped into per mapping contexts
        public ObservableCollection<DiscoveredSavesContext> SaveContexts = new();

        public SaveDiscoveryView()
        {
            InitializeComponent();
            DiscoveryList.DataContext = this;
        }

        public void RefreshSaveContexts()
        {

        }

        public void RefreshSaveContexts(EmulatorMapping mapping)
        {

        }

        private void Refresh_Click(object sender, System.Windows.RoutedEventArgs e)
        {

        }

        private void Accept_Click(object sender, System.Windows.RoutedEventArgs e)
        {

        }

        private void ChooseGame_Click(object sender, System.Windows.RoutedEventArgs e)
        {

        }

        private void ChooseGamesCard_Click(object sender, System.Windows.RoutedEventArgs e)
        {

        }
    }
}
