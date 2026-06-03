using Graviton.Models.Notifications;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Graviton.Settings
{
    /// <summary>
    /// Interaction logic for GravitonSettingsView.xaml
    /// </summary>
    public partial class SingleNotificationView : UserControl
    {
        public event EventHandler? DismissCompleted;

        private bool _dismissing;

        public SingleNotificationView(GravitonNotification notification)
        {
            InitializeComponent();
            DataContext = new NotificationViewModel(notification);

            Loaded += (_, _) =>
            {
                var animation = (DoubleAnimation)((Storyboard)Resources["ProgressBarAnimation"]).Children[0];
                animation.Duration = new Duration(TimeSpan.FromSeconds(notification.message.Length * 0.2));
                animation.From = RootBorder.ActualWidth;
                ((Storyboard)Resources["SlideInAnimation"]).Begin();
                ((Storyboard)Resources["ProgressBarAnimation"]).Begin();
            };
        }

        private void Progress_Completed(object sender, EventArgs e) => BeginDismiss();

        private void CloseButton_Click(object sender, RoutedEventArgs e) => BeginDismiss();

        public void BeginDismiss()
        {
            if (_dismissing) 
                return;
                
            _dismissing = true;
            ((Storyboard)Resources["ProgressBarAnimation"]).Stop();
            ((Storyboard)Resources["SlideOutAnimation"]).Begin();
        }

        private void SlideOut_Completed(object sender, EventArgs e)
        {
            DismissCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

}
