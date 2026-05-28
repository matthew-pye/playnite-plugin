using Graviton.Models.Notifications;

using Playnite;

using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace Graviton.Settings
{
    /// <summary>
    /// Interaction logic for GravitonSettingsView.xaml
    /// </summary>
    public partial class NotificationsView : UserControl
    {

        public NotificationsView()
        {
            InitializeComponent();
            GravitonNotify.OnNotificationAdded += HandleNewNotification;

            Unloaded += (_, _) => GravitonNotify.OnNotificationAdded -= HandleNewNotification;
        }

        private void HandleNewNotification(GravitonNotification notification)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
            {
                var toast = new SingleNotificationView(notification);

                toast.DismissCompleted += (_, _) =>
                {
                    ToastStack.Children.Remove(toast);
                    GravitonNotify.Notifications.Remove(notification);
                };

                // Insert at index 0 so newest toast appears at the top
                ToastStack.Children.Insert(0, toast);
            });
        }

    }
}
