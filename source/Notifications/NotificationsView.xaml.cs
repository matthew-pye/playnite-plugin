using Graviton.Models.Notifications;

using System.Windows.Controls;
using System.Windows.Threading;

namespace Graviton.Settings
{
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
                    lock (GravitonNotify.NotificationsLock) { GravitonNotify.Notifications.Remove(notification); }
                };

                ToastStack.Children.Insert(0, toast);
            });
        }

    }
}
