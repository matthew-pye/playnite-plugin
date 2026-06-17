using Graviton.Models.Notifications;

using Playnite;

using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows;
using System.Windows.Controls;

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

            AuthButtonText.Text = Loc.GetString("AuthButton");
            ServerHostText.Text = Loc.GetString("ServerText");
            TokenText.Text = Loc.GetString("ClientToken");
            UseBasicAuthText.Text = Loc.GetString("UseBasicAuth");
            UserPassWarning.Text = Loc.GetString("UserPassWarning");
            UsernameText.Text = Loc.GetString("Username");
            PasswordText.Text = Loc.GetString("Password");

            ProfileEditButton.FontFamily = Playnite.Fonts.NerdFont;
            ShowPassword.FontFamily = Playnite.Fonts.NerdFont;

            RomMPassword.Password = _plugin.Settings.PasswordNP;
            RomMPassword.PasswordChanged += (_, _) =>
            {
                _plugin.Settings.PasswordNP = RomMPassword.Password;
            };

            RomMPassword.Background = RomMUsername.Background;
            RomMPassword.BorderBrush = RomMUsername.BorderBrush;

        }

        private async void Click_Authenticate(object sender, System.Windows.RoutedEventArgs e)
        {
            TestServer.IsEnabled = false;
            await _plugin.Account!.Login();
            TestServer.IsEnabled = true;

            e.Handled = true;
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
                var result = await HttpClientSingleton.RomMPutContentAsync($"/api/users/{_plugin.Settings.UserID}", content);

                if(result != null)
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
            e.Handled = true;
        }

        private void Checked_UseBasicAuth(object sender, RoutedEventArgs e)
        {
            RomMPassword.Visibility = Visibility.Visible;
            RomMPasswordUnmasked.Visibility = Visibility.Collapsed;
            ShowPassword.Content = "\uea70";
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

    }
}
