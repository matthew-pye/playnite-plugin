using Graviton.Models.Notifications;

using Playnite;

namespace Graviton
{
    public static class GravitonNotify
    {
        private static readonly ILogger _logger = GravitonPlugin.Logger;

        public static readonly object NotificationsLock = new();
        public static readonly List<GravitonNotification> Notifications = new();
        public static event Action<GravitonNotification>? OnNotificationAdded;

        public static void Add(GravitonNotification Notification)
        {
            NotificationSeverity severity = Notification.severity == GravitonSeverity.Error ? NotificationSeverity.Error : NotificationSeverity.Info;

            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                GravitonPlugin.PlayniteApi?.Notifications.Add(new NotificationMessage(Notification.id, Notification.message, severity));
            });

            string loggerMessage = $"[{Notification.file} @ line {Notification.lineNumber}] {Notification.message}";
            if(Notification.exeption != null)
            {
                loggerMessage += $"\n{Notification.exeption}";
            }

            switch (Notification.severity)
            {
                case GravitonSeverity.Info:
                    _logger.Info(loggerMessage);
                    break;

                case GravitonSeverity.Success:
                    _logger.Info(loggerMessage);
                    break;

                case GravitonSeverity.Warn:
                    _logger.Warn(loggerMessage);
                    break;

                case GravitonSeverity.Error:
                    _logger.Error(loggerMessage);
                    break;

            }

            lock (NotificationsLock) { Notifications.Add(Notification); }
            OnNotificationAdded?.Invoke(Notification);
        }
    }
}
