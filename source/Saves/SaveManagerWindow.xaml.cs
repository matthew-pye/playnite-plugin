using Playnite;

using System.Windows;

namespace Graviton.Saves
{
    public partial class SaveManagerWindow
    {
        public static void Show(string title, FrameworkElement content)
        {
            var window = GravitonPlugin.PlayniteApi.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = false,
                ShowMaximizeButton = true,
                ShowCloseButton = true,
                DefaultWidth = 850,
                DefaultHeight = 650
            });

            window.Title = title;
            window.Content = content;
            window.Owner = GravitonPlugin.PlayniteApi.GetLastActiveWindow();
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();
        }

    }
}