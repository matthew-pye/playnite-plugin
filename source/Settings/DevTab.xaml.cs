using Graviton.Models.Notifications;

using System.Windows;
using System.Windows.Controls;

namespace Graviton.Settings
{
    /// <summary>
    /// Interaction logic for GravitonSettingsView.xaml
    /// </summary>
    public partial class DevTab : UserControl
    {

        public DevTab()
        {
            InitializeComponent();
        }

        private void Success_Click(object sender, RoutedEventArgs e)
        {
            GravitonNotify.Add(new GravitonNotification("graviton.dev.success.test", "Test notification for successful notification!", GravitonSeverity.Success));
        }

        private void Info_Click(object sender, RoutedEventArgs e)
        {
            GravitonNotify.Add(new GravitonNotification("graviton.dev.info.test", "Test notification for info notification!", GravitonSeverity.Info));
        }

        private void Warn_Click(object sender, RoutedEventArgs e)
        {
            GravitonNotify.Add(new GravitonNotification("graviton.dev.warn.test", "Test notification for warn notification!", GravitonSeverity.Warn));
        }

        private void Error_Click(object sender, RoutedEventArgs e)
        {
            GravitonNotify.Add(new GravitonNotification("graviton.dev.error.test", "Test notification for error notification!", GravitonSeverity.Error));
        }
    }
}
