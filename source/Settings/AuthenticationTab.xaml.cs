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
        static GravitonPlugin _plugin {get => GravitonPlugin.Instance ?? throw new Exception("Plugin is null, cannot continue"); }
        static IPlayniteApi _playniteAPI { get => GravitonPlugin.PlayniteApi ?? throw new Exception("PlayniteAPI is null, cannot continue"); }
        static GravitonSettingsHandler SettingsHandler { get => GravitonSettingsHandler.Instance ?? throw new Exception("Settings is null, cannot continue"); }

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
            PasswordText.Text = Loc.GetString("Username");

            ProfileEditButton.FontFamily = Playnite.Fonts.NerdFont;
        }

        private async void Click_Authenticate(object sender, System.Windows.RoutedEventArgs e)
        {
            TestServer.IsEnabled = false;
            await _plugin.Account.Login(SettingsHandler.Settings);
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

                try
                {
                    HttpResponseMessage response = await HttpClientSingleton.Instance.PutAsync($"{SettingsHandler.Settings.Host}/api/users/{SettingsHandler.Settings.UserID}", content);
                    response.EnsureSuccessStatusCode();
                    File.WriteAllBytes($"{_playniteAPI.UserDataDir}\\avatar.png", fileBytes);
                    SettingsHandler.Settings.ProfilePath = $"{_playniteAPI.UserDataDir}\\avatar.png";
                }
                catch (Exception ex)
                {
                    Path.Combine(_plugin.PluginDLLPath, @"profile.png");
                    GravitonNotify.Add(new GravitonNotification("graviton.PUT.profileimage.failed", $"{Loc.GetString("NewProfileIconFailed")} - {ex.Message}", GravitonSeverity.Error));
                }
                e.Handled = true;
            }
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
    }
}
