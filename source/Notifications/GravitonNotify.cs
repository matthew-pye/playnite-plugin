using Graviton.Models.Notifications;

using Playnite;

using System.Diagnostics;

namespace Graviton
{
    public static class GravitonNotify
    {
        public static readonly object NotificationsLock = new();
        public static readonly List<GravitonNotification> Notifications = new();
        public static event Action<GravitonNotification>? OnNotificationAdded;

        private static GravitonPlugin? _plugin;
        private static IPlayniteApi? _playniteAPI;
        private static ILogger? _logger;

        private static bool IsInitialized = false;

        public static void Initialize(GravitonPlugin plugin, IPlayniteApi playniteAPI, ILogger logger)
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;

            IsInitialized = true;
        }

        public static void Add(GravitonNotification Notification)
        {
            if (!IsInitialized)
            {
                Debug.WriteLine("GravitonNotify hasn't been initialized cannot perform notifications!!");
                return;
            }
               
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
                    _logger?.Info(loggerMessage);
                    break;

                case GravitonSeverity.Success:
                    _logger?.Info(loggerMessage);
                    break;

                case GravitonSeverity.Warn:
                    _logger?.Warn(loggerMessage);
                    break;

                case GravitonSeverity.Error:
                    _logger?.Error(loggerMessage);
                    break;

            }

            lock (NotificationsLock) { Notifications.Add(Notification); }
            OnNotificationAdded?.Invoke(Notification);
        }
    }
}
