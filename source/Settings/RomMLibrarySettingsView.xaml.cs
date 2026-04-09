using RomMLibrary.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace RomMLibrary
{
    /// <summary>
    /// Interaction logic for RomMLibrarySettingsView.xaml
    /// </summary>
    public partial class RomMLibrarySettingsView : UserControl
    {
        public RomMLibrarySettingsView()
        {
            InitializeComponent();
        }

        private void Click_TestConnection(object sender, RoutedEventArgs e)
        {
            RomMLibrarySettingsHandler.Instance?.TestConnection();
            e.Handled = true;
        }
    }
}
