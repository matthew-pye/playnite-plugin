using System.Windows.Controls;

namespace Graviton.Saves
{
    public partial class SaveManagement : UserControl
    {
        private GravitonPlugin _plugin { get => GravitonPlugin.Instance; }

        private SaveManagementContext _context;
        private bool _filteredForNeedsAttention = false;

        public SaveManagement(SaveManagementContext context)
        {
            _context = context;

            InitializeComponent();

            SaveManagementUI.DataContext = _context;
        }

        private void ScanForSaves_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            //_context.DiscoveredSaves = _plugin.SaveController.DiscoverSaves();
        }

        private void NeedAttentionTab_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            //TODO Update when playthrough data is added to the FS rom cache for proper filtering
        }

        private void DiscoveredTab_Click(object sender, System.Windows.RoutedEventArgs e)
        {

        }

        private void AllGamesTab_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            GamesList.DataContext = _context.ROMs;
        }

        private void SaveFilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            //TODO Update when playthrough data is added to the FS rom cache for proper filtering

            if(!string.IsNullOrEmpty(GameFilterBox.Text))
            {
                GamesList.DataContext = _context.ROMs.Where(x => x.Name.Contains(GameFilterBox.Text)).OrderBy(x => x.Name).ToList();
            }
            else
            {
                GamesList.DataContext = _context.ROMs;
            }
        }

        private void ViewGame_Click(object sender, System.Windows.RoutedEventArgs e)
        {

        }
    }
}
