using System.Windows.Media;

namespace Graviton.Models.Notifications
{
    public class NotificationViewModel
    {
        public string Message { get; }
        public string Icon { get; }
        public Brush TextColour { get; }
        public Brush ProgressBarColour { get; }
        public Brush AccentColour { get; }

        public NotificationViewModel(GravitonNotification n)
        {
            Message = n.message;

            switch (n.severity)
            {
                case GravitonSeverity.Success:
                    Icon = "✓";
                    TextColour = new SolidColorBrush(Color.FromArgb(255, 79, 138, 16));
                    ProgressBarColour = new SolidColorBrush(Color.FromArgb(255, 79, 138, 16));
                    AccentColour = new SolidColorBrush(Color.FromArgb(235, 213, 250, 151));
                    break;

                case GravitonSeverity.Warn:
                    Icon = "⚠";
                    TextColour = new SolidColorBrush(Color.FromArgb(255, 159, 96, 0));
                    ProgressBarColour = new SolidColorBrush(Color.FromArgb(255, 159, 96, 0));
                    AccentColour = new SolidColorBrush(Color.FromArgb(235, 254, 239, 179));
                    break;

                case GravitonSeverity.Error:
                    Icon = "✕";
                    TextColour = new SolidColorBrush(Color.FromArgb(255, 216, 0, 12));
                    ProgressBarColour = new SolidColorBrush(Color.FromArgb(255, 216, 0, 12));
                    AccentColour = new SolidColorBrush(Color.FromArgb(235, 255, 186, 186));
                    break;

                default:
                    Icon = "ℹ";
                    TextColour = new SolidColorBrush(Color.FromArgb(255, 49, 112, 143));
                    ProgressBarColour = new SolidColorBrush(Color.FromArgb(255, 49, 112, 143));
                    AccentColour = new SolidColorBrush(Color.FromArgb(235, 217, 237, 247));
                    break;
            }
        }
    }
}
