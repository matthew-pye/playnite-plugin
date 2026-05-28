using System.Windows.Controls;

namespace Graviton.Settings
{
    /// <summary>
    /// Interaction logic for GravitonSettingsView.xaml
    /// </summary>
    public partial class SettingsTabControl : UserControl
    {
        public SettingsTabControl()
        {
            InitializeComponent();
            SettingsTabs.FontFamily = Playnite.Fonts.NerdFont;

#if DEBUG
            DEVTAB.Visibility = System.Windows.Visibility.Visible;
#else
            DEVTAB.Visibility = System.Windows.Visibility.Collapsed;
#endif
        }

    }
}
