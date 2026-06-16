using Graviton.Models.Notifications;

using Playnite;

using System.Collections.Concurrent;

namespace Graviton
{
    public static class GravitonNotify
    {
        private static readonly ILogger _logger = LogManager.GetLogger();

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

            switch (Notification.severity)
            {
                case GravitonSeverity.Info:
                    _logger.Info($"[{Notification.file} @ line {Notification.lineNumber}] {Notification.message}");
                    break;

                case GravitonSeverity.Success:
                    _logger.Info($"[{Notification.file} @ line {Notification.lineNumber}] {Notification.message}");
                    break;

                case GravitonSeverity.Warn:
                    _logger.Warn($"[{Notification.file} @ line {Notification.lineNumber}] {Notification.message}");
                    break;

                case GravitonSeverity.Error:
                    _logger.Error($"[{Notification.file} @ line {Notification.lineNumber}] {Notification.message}");
                    break;

            }

            lock (NotificationsLock) { Notifications.Add(Notification); }
            OnNotificationAdded?.Invoke(Notification);
        }
    }
}
