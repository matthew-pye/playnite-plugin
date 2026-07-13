using Playnite;

using System.IO;
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
}