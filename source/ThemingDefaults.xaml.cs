using Playnite;

using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Graviton
{

    // Used to load profile image into cache so it can be changed while the application is running
    public class ImageCacheConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
            object parameter, System.Globalization.CultureInfo culture)
        {

            var path = value as string;
            if (string.IsNullOrEmpty(path))
                return new BitmapImage();

            path = path.Contains('?') ? path.Substring(0, path.IndexOf('?')) : path;

            if (string.IsNullOrEmpty(path))
                return new BitmapImage();

            if (!File.Exists(path))
                return new BitmapImage();

            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.EndInit();

            return image;

        }

        public object ConvertBack(object value, Type targetType,
            object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException("Not implemented.");
        }
    }

    public class EnumDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            var locKey = $"{value.GetType().Name}_{value}";
            if (Loc.IsStringId(locKey))
                return Loc.GetString(locKey);

            var field = value.GetType().GetField(value.ToString()!);
            if (field == null)
                return value.ToString()!;

            var attribute = (System.ComponentModel.DescriptionAttribute?)Attribute.GetCustomAttribute(field, typeof(System.ComponentModel.DescriptionAttribute));
            return attribute?.Description ?? value.ToString()!;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException("Not implemented.");
    }
    public static class Placeholder
    {
        public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached("Text", typeof(string), typeof(Placeholder), new PropertyMetadata(string.Empty, OnTextChanged));

        public static void SetText(DependencyObject element, string value) => element.SetValue(TextProperty, value);
        public static string GetText(DependencyObject element) => (string)element.GetValue(TextProperty);

        private static readonly DependencyPropertyKey HasTextPropertyKey = DependencyProperty.RegisterAttachedReadOnly("HasText", typeof(bool), typeof(Placeholder), new PropertyMetadata(false));
        public static readonly DependencyProperty HasTextProperty = HasTextPropertyKey.DependencyProperty;

        public static bool GetHasText(DependencyObject element) => (bool)element.GetValue(HasTextProperty);

        private static readonly ConditionalWeakTable<PasswordBox, object> hookedPasswordBoxes = new();

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not PasswordBox passwordBox)
                return;

            if (hookedPasswordBoxes.TryGetValue(passwordBox, out _))
                return;

            hookedPasswordBoxes.Add(passwordBox, null!);
            passwordBox.SetValue(HasTextPropertyKey, passwordBox.Password.Length > 0);
            passwordBox.PasswordChanged += (sender, args) =>
            {
                var box = (PasswordBox)sender;
                box.SetValue(HasTextPropertyKey, box.Password.Length > 0);
            };
        }
    }
}