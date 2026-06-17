using Playnite;

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

            AuthItem.Header = $"\U000f0004 {Loc.GetString("Authentication")}";
            OptionsItem.Header = $"\uf013 {Loc.GetString("Options")}";
            MappingsItem.Header = $"\U000f0eb6 {Loc.GetString("Mappings")}";
            SavesItem.Header = $"\U000f0193 {Loc.GetString("Saves")}";
            SaveStatesItem.Header = $"\ueea8 {Loc.GetString("States")}";

            SettingsTabs.FontFamily = Playnite.Fonts.NerdFont;

#if DEBUG
            DEVTAB.Visibility = System.Windows.Visibility.Visible;
#else
            DEVTAB.Visibility = System.Windows.Visibility.Collapsed;
#endif
        }

    }
}
