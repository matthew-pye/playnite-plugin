using Playnite;

using System.Windows;
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

#if DEBUG
            DEVTAB.Visibility = System.Windows.Visibility.Visible;
#else
            DEVTAB.Visibility = System.Windows.Visibility.Collapsed;
#endif
        }

        private void MappingsTab_ManageSavesRequested(object sender, RoutedEventArgs e)
        {
            var tab = ((ManageSavesRequestedEventArgs)e).Tab;
            tab.BackRequested += (_, _) => CloseFullView();
            ShowInFullView(tab);
        }

        private void ShowInFullView(FrameworkElement content)
        {
            SettingsFullView.Content = content;
            SettingsFullView.Visibility = Visibility.Visible;
            SettingsTabs.Visibility = Visibility.Collapsed;
        }

        private void CloseFullView()
        {
            SettingsFullView.Visibility = Visibility.Collapsed;
            SettingsFullView.Content = null;
            SettingsTabs.Visibility = Visibility.Visible;
        }

    }
}
