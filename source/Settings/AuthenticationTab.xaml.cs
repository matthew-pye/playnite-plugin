using Graviton.Models;
using Graviton.Models.Notifications;

using Playnite;

//using QRCoder;
//using QRCoder.Xaml;

//using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
//using System.Windows.Media;

namespace Graviton.Settings
{
    /// <summary>
    /// Interaction logic for GravitonSettingsView.xaml
    /// </summary>
    public partial class AuthenticationTab : UserControl
    {
        private GravitonPlugin _plugin { get => GravitonPlugin.Instance; }
        private IPlayniteApi _playniteAPI { get => GravitonPlugin.PlayniteApi; }
        private ILogger _logger { get => GravitonPlugin.Logger; }

        Dictionary<string, string[]> ImageFileChoices = new Dictionary<string, string[]>()
        {
            {"Image File", ["*.png","*.jpg", "*.jpeg","*.webp"]}
        };

        public AuthenticationTab()
        {
            InitializeComponent();

            //AuthButtonText.Text = Loc.GetString("AuthButton");
            //ServerHostText.Text = Loc.GetString("ServerText");
            //TokenText.Text = Loc.GetString("ClientToken");
            //UseBasicAuthText.Text = Loc.GetString("UseBasicAuth");
            //UserPassWarning.Text = Loc.GetString("UserPassWarning");
            //UsernameText.Text = Loc.GetString("Username");
            //PasswordText.Text = Loc.GetString("Password");
            //CustomHeadersText.Text = Loc.GetString("CustomHeaders");
            //
            //ProfileEditButton.FontFamily = Playnite.Fonts.NerdFont;
            //ShowPassword.FontFamily = Playnite.Fonts.NerdFont;
            //AddHeaderText.FontFamily = Playnite.Fonts.NerdFont;
            //
            //RomMPassword.Password = _plugin.Settings.PasswordNP;
            //RomMPassword.PasswordChanged += (_, _) =>
            //{
            //    _plugin.Settings.PasswordNP = RomMPassword.Password;
            //};
            //
            //RomMPassword.Background = RomMUsername.Background;
            //RomMPassword.BorderBrush = RomMUsername.BorderBrush;

        }

        private async void Click_UpdateProfileIcon(object sender, RoutedEventArgs e)
        {

            var image = await _playniteAPI.Dialogs.SelectFileAsync(ImageFileChoices);
            if (image != null && image.Count == 1)
            {
                var fileBytes = File.ReadAllBytes(image[0]);
                var fileName = Path.GetFileName(image[0]);

                var ext = Path.GetExtension(fileName).ToLowerInvariant();
                string filetype;

                switch (ext)
                {
                    case ".jpg":
                        filetype = "image/jpeg";
                        break;

                    case ".jpeg":
                        filetype = "image/jpeg";
                        break;

                    case ".png":
                        filetype = "image/png";
                        break;

                    case ".webp":
                        filetype = "image/webp";
                        break;

                    default:
                        filetype = "application/octet-stream";
                        break;
                }

                var content = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(filetype);

                content.Add(fileContent, "avatar", fileName);
                var result = await HttpClientSingleton.RomMPutContentAsync($"/api/users/{_plugin.Settings.AccountState.UserID}", content);

                if (result != null)
                {
                    File.WriteAllBytes($"{_playniteAPI.UserDataDir}\\avatar.png", fileBytes);
                    _plugin.Settings.ProfilePath = $"{_playniteAPI.UserDataDir}\\avatar.png";
                }
                else
                {
                    _plugin.Settings.ProfilePath = $"{_plugin.PluginDLLPath}\\profile.png";
                }
            }

            e.Handled = true;
        }

        /*private async void Click_Authenticate(object sender, System.Windows.RoutedEventArgs e)
        {
            TestServer.IsEnabled = false;
            await _plugin.Account!.Login();
            TestServer.IsEnabled = true;

            e.Handled = true;
        }

        

        private void Hyperlink_ClientToken(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {

            if(string.IsNullOrEmpty(_plugin.Settings.Host))
            {
                GravitonNotify.Add(new GravitonNotification("graviton.openuri.clienttoken.failed", Loc.GetString("ClientTokenAddressFailed"), GravitonSeverity.Error));
                e.Handled= true;
                return;
            }

            string clienttokenaddress = _plugin.Settings.Host + "/client-api-tokens";
            Process.Start(new ProcessStartInfo(clienttokenaddress) { UseShellExecute = true })?.Dispose();
            e.Handled = true;
        }

        private void Unchecked_UseBasicAuth(object sender, RoutedEventArgs e)
        {
            RomMPasswordUnmasked.Visibility = Visibility.Collapsed;
            RomMPassword.Visibility = Visibility.Collapsed;
            BasicAuthRow1.Height = new GridLength(0);
            BasicAuthRow2.Height = new GridLength(0);
            BasicAuthRow3.Height = new GridLength(0);
            e.Handled = true;
        }

        private void Checked_UseBasicAuth(object sender, RoutedEventArgs e)
        {
            RomMPassword.Visibility = Visibility.Visible;
            RomMPasswordUnmasked.Visibility = Visibility.Collapsed;
            ShowPassword.Content = "\uea70";
            BasicAuthRow1.Height = new GridLength(50);
            BasicAuthRow2.Height = new GridLength(45);
            BasicAuthRow3.Height = new GridLength(45);
            e.Handled = true;
        }

        private void Click_ShowPassword(object sender, RoutedEventArgs e)
        {
            if(RomMPassword.Visibility == Visibility.Visible)
            {
                RomMPassword.Visibility = Visibility.Collapsed;
                RomMPasswordUnmasked.Visibility = Visibility.Visible;
                ShowPassword.Content = "\ueae7";
            }
            else
            {
                RomMPassword.Visibility = Visibility.Visible;
                RomMPasswordUnmasked.Visibility = Visibility.Collapsed;
                ShowPassword.Content = "\uea70";
                RomMPassword.Password = _plugin.Settings.PasswordNP;
            }

            e.Handled = true;
        }

        private async void Click_LoginViaQR(object sender, RoutedEventArgs e)
        {
            var initDevice = await _plugin.Account!.InitDevicePair();
            if (initDevice == null)
            {
                e.Handled = true;
                return;
            }

            QRAuth.IsEnabled = false;

            using (var qrGenerator = new QRCodeGenerator())
            using (var qrCodeData = qrGenerator.CreateQrCode($"{_plugin.Settings.Host}{initDevice.VerificationPathComplete}", QRCodeGenerator.ECCLevel.Q))
            using (var qrCode = new XamlQRCode(qrCodeData))
            {
                DrawingImage qrImage = qrCode.GetGraphic(20);
                qrImage.Freeze();
                UIDispatcher.Invoke(() => LoginQR.Source = qrImage);
            }

            await _plugin.Account.StartDevicePair(initDevice, LoginQRTimer);

            UIDispatcher.Invoke(() => LoginQR.Source = null);
            UIDispatcher.Invoke(() => LoginQRTimer.Text = "");
            UIDispatcher.Invoke(() => QRAuth.IsEnabled = true);

            e.Handled = true;
        }*/

        private void Click_AddHeader(object sender, RoutedEventArgs e)
        {
            _plugin.Settings.CustomHeaders.Add(new CustomHTTPHeader());
            e.Handled = true;
        }
        private void Click_RemoveHeader(object sender, RoutedEventArgs e)
        {
            var header = ((FrameworkElement)sender).DataContext as CustomHTTPHeader;
            if (header != null)
            {
                if (!string.IsNullOrEmpty(header.Name))
                    HttpClientSingleton.Instance.DefaultRequestHeaders.Remove(header.Name);

                _plugin.Settings.CustomHeaders.Remove(header);
            }

            e.Handled = true;
        }

        private void Header_Checked(object sender, RoutedEventArgs e)
        {
            var header = ((FrameworkElement)sender).DataContext as CustomHTTPHeader;
            if (header != null && !string.IsNullOrEmpty(header.Name) && !string.IsNullOrEmpty(header.Value))
            {
                if (!HttpClientSingleton.Instance.DefaultRequestHeaders.Contains(header.Name))
                    HttpClientSingleton.Instance.DefaultRequestHeaders.Add(header.Name, header.Value);
            }
            else if (header != null)
            {
                header.Enabled = false;
                GravitonNotify.Add(new GravitonNotification("graviton.header.ismalformed", Loc.GetString("CustomHeaderMalformed"), GravitonSeverity.Error));
            }

            e.Handled = true;
        }

        private void Header_Unchecked(object sender, RoutedEventArgs e)
        {
            var header = ((FrameworkElement)sender).DataContext as CustomHTTPHeader;
            if (header != null && !string.IsNullOrEmpty(header.Name))
            {
                HttpClientSingleton.Instance.DefaultRequestHeaders.Remove(header.Name);
            }

            e.Handled = true;
        }

    }

    public class InvertBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool)
                return !(bool)value;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool)
                return !(bool)value;
            return false;
        }
    }

}
