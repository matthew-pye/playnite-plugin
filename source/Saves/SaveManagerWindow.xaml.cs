using System.Windows;

namespace Graviton.Saves
{
    public partial class SaveManagerWindow : Window
    {
        public string WindowTitle { get; }

        public SaveManagerWindow(string title, FrameworkElement content)
        {
            WindowTitle = title;
            InitializeComponent();
            DataContext = this;
            Host.Children.Add(content);
        }
    }
}