using Graviton.Models;
using Graviton.Models.Notifications;

using Playnite;

using QRCoder;

using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;

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

        string QRCodeURl = "";

        public AuthenticationTab()
        {
            InitializeComponent();

            AuthButtonText.Text = Loc.GetString("AuthButton");

            UseBasicAuthText.Text = Loc.GetString("UseBasicAuth");
            UserPassWarning.Text = Loc.GetString("UserPassWarning");
            BasicLoginText.Text = Loc.GetString("Login");

            AdvanceOptionsText.Text = Loc.GetString("AdvanceOptions");
            CustomHeadersText.Text = Loc.GetString("CustomHeaders");
            AddHeaderText.Text = $"\uea60 {Loc.GetString("NewHeader")}";

            ProfileEditButton.FontFamily = Playnite.Fonts.NerdFont;
            ShowPassword.FontFamily = Playnite.Fonts.NerdFont;
            AddHeaderText.FontFamily = Playnite.Fonts.NerdFont;
            
            RomMPassword.Password = _plugin.Settings.PasswordNP;
            RomMPassword.PasswordChanged += (_, _) =>
            {
                _plugin.Settings.PasswordNP = RomMPassword.Password;
            };

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

        private async void Click_Authenticate(object sender, System.Windows.RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_plugin.Settings.ClientTokenNP))
            {
                GravitonNotify.Add(new GravitonNotification("graviton.login.notoken", Loc.GetString("LoginNoToken"), GravitonSeverity.Warn));
                e.Handled = true;
                return;
            }

            ConnectButton.IsEnabled = false;
            await _plugin.Account!.Login();
            ConnectButton.IsEnabled = true;

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

            QRCodeURl = $"{_plugin.Settings.Host}{initDevice.VerificationPathComplete}";

            UIDispatcher.Invoke(() => QRAuth.IsEnabled = false);
            UIDispatcher.Invoke(() => OpenInBrowser.Visibility = Visibility.Visible);
            UIDispatcher.Invoke(() => LoginQRBorder.Background = new System.Windows.Media.ImageBrush(BuildQRCode(QRCodeURl)));
            UIDispatcher.Invoke(() => LoginQRBorder.Visibility = Visibility.Visible);
            UIDispatcher.Invoke(() => TempQRText.Text = "");
            UIDispatcher.Invoke(() => LoginQRTimer.Visibility = Visibility.Visible);


            await _plugin.Account.StartDevicePair(initDevice, LoginQRTimer);

            UIDispatcher.Invoke(() => LoginQRBorder.Background = null);
            UIDispatcher.Invoke(() => LoginQRTimer.Text = "");
            UIDispatcher.Invoke(() => QRAuth.IsEnabled = true);
            UIDispatcher.Invoke(() => OpenInBrowser.Visibility = Visibility.Collapsed);
            UIDispatcher.Invoke(() => LoginQRTimer.Visibility = Visibility.Collapsed);
            UIDispatcher.Invoke(() => LoginQRBorder.Visibility = Visibility.Hidden);
            UIDispatcher.Invoke(() => TempQRText.Text = "\uf029");

            e.Handled = true;
        }

        private void Click_OnInBrowser(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo(QRCodeURl) { UseShellExecute = true })?.Dispose();
            e.Handled = true;
        }

        private void RomMPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _plugin.Settings.PasswordNP = RomMPassword.Password;
        }

        private async void Click_BasicLogin(object sender, RoutedEventArgs e)
        {
            if(!_plugin.Settings.UseBasicAuth)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.basiclogin.notenabled", Loc.GetString("EnableBasicAuth"), GravitonSeverity.Warn));
                e.Handled = true;
                return;
            }

            BasicLoginButton.IsEnabled = false;
            ConnectButton.IsEnabled = false;

            await _plugin.Account!.Login();

            BasicLoginButton.IsEnabled = true;
            ConnectButton.IsEnabled = true;

            e.Handled = true;
        }

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



        #region QR Code
        private GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();

            float d = radius * 2;

            if (radius <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }

            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;

            radius = d / 2;

            path.StartFigure();
            path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();

            return path;
        }

        private void DrawFinder(Graphics g, int x, int y, int pixelsPerModule, Color light)
        {
            int size = pixelsPerModule * 7;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var lightBrush = new SolidBrush(light))
            {
                using (var outer = new GraphicsPath())
                using (var middleHole = new GraphicsPath())
                {
                    using (var outerShape = RoundedRect(new RectangleF(x, y, size, size), pixelsPerModule * 1.6f))
                    {
                        outer.AddPath(outerShape, false);
                    }

                    int border = (int)(pixelsPerModule * 0.6f);
                    using (var middleHoleShape = RoundedRect(new RectangleF(x + border, y + border, size - border * 2, size - border * 2), pixelsPerModule * 1.2f))
                    {
                        middleHole.AddPath(middleHoleShape, false);
                    }

                    using (var region = new System.Drawing.Region(outer))
                    {
                        region.Exclude(middleHole);
                        g.FillRegion(lightBrush, region);
                    }
                }

                int centre = pixelsPerModule * 2;
                using (var centerPath = RoundedRect(new RectangleF(x + centre, y + centre, size - centre * 2, size - centre * 2), pixelsPerModule * 0.8f))
                {
                    g.FillPath(lightBrush, centerPath);
                }
            }

        }

        private BitmapImage BuildQRCode(string data)
        {
            using (Bitmap logo = new Bitmap($"{_plugin.PluginDLLPath}/libraryicon.png"))
            {
                using (var qrGenerator = new QRCodeGenerator())
                using (var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.H))
                using (var qrCode = new ArtQRCode(qrCodeData))
                {
                    var lightcolour = System.Drawing.Color.FromArgb(255, 250, 250, 245);
                    var darkcolour = System.Drawing.Color.FromArgb(255, 20, 20, 30);

                    Bitmap qrBitmap = qrCode.GetGraphic(
                                                    pixelsPerModule: 20,
                                                    darkColor: lightcolour,
                                                    lightColor: darkcolour,
                                                    backgroundColor: System.Drawing.Color.Transparent,
                                                    pixelSizeFactor: 0.7,
                                                    drawQuietZones: false
                                                    );

                    const int padding = 20;
                    Bitmap canvas = new Bitmap(qrBitmap.Width + padding * 2, qrBitmap.Height + padding * 2, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                    using (Graphics g = Graphics.FromImage(canvas))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.InterpolationMode = InterpolationMode.NearestNeighbor;
                        g.PixelOffsetMode = PixelOffsetMode.Half;

                        g.DrawImage(qrBitmap, padding, padding);

                        int finderSize = 7 * 20;
                        using (var coverPath = new GraphicsPath())
                        {
                            coverPath.AddRectangle(new RectangleF(padding, padding, finderSize, finderSize));
                            coverPath.AddRectangle(new RectangleF(canvas.Width - padding - finderSize, padding, finderSize, finderSize));
                            coverPath.AddRectangle(new RectangleF(padding, canvas.Height - padding - finderSize, finderSize, finderSize));

                            GraphicsState clearState = g.Save();
                            g.SetClip(coverPath);
                            g.Clear(System.Drawing.Color.Transparent);
                            g.Restore(clearState);
                        }

                        DrawFinder(g, 20, 20, 20, lightcolour);
                        DrawFinder(g, canvas.Width - 20 - finderSize, 20, 20, lightcolour);
                        DrawFinder(g, 20, canvas.Height - 20 - finderSize, 20, lightcolour);

                        int iconSize = (int)(canvas.Width * 0.20);
                        int iconX = (canvas.Width - iconSize) / 2;
                        int iconY = (canvas.Height - iconSize) / 2;

                        const int badgePadding = 20;
                        const int badgeRadius = 20;

                        RectangleF badgeRect = new RectangleF(iconX - badgePadding, iconY - badgePadding, iconSize + badgePadding * 2, iconSize + badgePadding * 2);
                        using (var badgePath = RoundedRect(badgeRect, badgeRadius))
                        {
                            GraphicsState state = g.Save();
                            g.SetClip(badgePath);
                            g.Clear(System.Drawing.Color.Transparent);
                            g.Restore(state);

                            using (GraphicsPath logoPath = RoundedRect(new RectangleF(iconX, iconY, iconSize, iconSize), 16))
                            {
                                state = g.Save();
                                g.SetClip(logoPath);
                                g.DrawImage(logo, iconX, iconY, iconSize, iconSize);
                                g.Restore(state);
                            }
                        }
                    }

                    qrBitmap.Dispose();
                    qrBitmap = canvas;

                    using (MemoryStream memory = new MemoryStream())
                    {
                        qrBitmap.Save(memory, ImageFormat.Png);
                        memory.Position = 0;

                        BitmapImage bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.StreamSource = memory;
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.EndInit();
                        bitmapImage.Freeze();

                        qrBitmap.Dispose();
                        return bitmapImage;
                    }
                }
            }

        }
        #endregion
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