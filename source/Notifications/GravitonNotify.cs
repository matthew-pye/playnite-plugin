using Graviton.Models.Notifications;

using Playnite;

namespace Graviton
{
    public static class GravitonNotify
    {
        public static List<GravitonNotification> Notifications = new();
        public static event Action<GravitonNotification>? OnNotificationAdded;

        public static void Add(GravitonNotification Notification)
        {
            NotificationSeverity severity = Notification.severity == GravitonSeverity.Error ? NotificationSeverity.Error : NotificationSeverity.Info;
            GravitonPlugin.PlayniteApi?.Notifications.Add(new NotificationMessage(Notification.id, Notification.message, severity));
            
            switch (Notification.severity)
            {
                case GravitonSeverity.Info:
                    LogManager.GetLogger().Info($"[{Notification.file} @ line {Notification.lineNumber}] {Notification.message}");
                    break;

                case GravitonSeverity.Success:
                    LogManager.GetLogger().Info($"[{Notification.file} @ line {Notification.lineNumber}] {Notification.message}");
                    break;

                case GravitonSeverity.Warn:
                    LogManager.GetLogger().Warn($"[{Notification.file} @ line {Notification.lineNumber}] {Notification.message}");
                    break;

                case GravitonSeverity.Error:
                    LogManager.GetLogger().Error($"[{Notification.file} @ line {Notification.lineNumber}] {Notification.message}");
                    break;

            }

            Notifications.Add(Notification);
            OnNotificationAdded?.Invoke(Notification);
        }
    }
}
