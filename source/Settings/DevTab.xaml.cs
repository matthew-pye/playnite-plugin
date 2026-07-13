using Graviton.Models.Notifications;

using System.Windows;
using System.Windows.Controls;

namespace Graviton.Settings
{
    /// <summary>
    /// Interaction logic for GravitonSettingsView.xaml
    /// </summary>
    public partial class DevTab : UserControl
    {
        bool testInProgress = false;
        public DevTab()
        {
            InitializeComponent();
        }

        private void Success_Click(object sender, RoutedEventArgs e)
        {
            GravitonNotify.Add(new GravitonNotification("graviton.dev.success.test", "Success Notification", GravitonSeverity.Success));
        }

        private void Info_Click(object sender, RoutedEventArgs e)
        {
            GravitonNotify.Add(new GravitonNotification("graviton.dev.info.test", "Info Notification", GravitonSeverity.Info));
        }

        private void Warn_Click(object sender, RoutedEventArgs e)
        {
            GravitonNotify.Add(new GravitonNotification("graviton.dev.warn.test", "Warn Notification", GravitonSeverity.Warn));
        }

        private void Error_Click(object sender, RoutedEventArgs e)
        {
            GravitonNotify.Add(new GravitonNotification("graviton.dev.error.test", "Error Notification", GravitonSeverity.Error));
        }

        private async void LoggedIn_Click(object sender, RoutedEventArgs e)
        {
            if (testInProgress)
                return;

            testInProgress = true;
            var AuthenticateFailed = GravitonPlugin.Instance.Settings.AccountState.AuthenticateFailed;
            var UserID = GravitonPlugin.Instance.Settings.AccountState.UserID;
            var LastAuthenticated = GravitonPlugin.Instance.Settings.AccountState.LastAuthenticated;

            GravitonPlugin.Instance.Settings.AccountState.AuthenticateFailed = System.Net.HttpStatusCode.OK;
            GravitonPlugin.Instance.Settings.AccountState.UserID = 1000;
            GravitonPlugin.Instance.Settings.AccountState.LastAuthenticated = DateTime.UtcNow;

            GravitonNotify.Add(new GravitonNotification("graviton.dev.login.test", "Temporarily set login status to Logged In", GravitonSeverity.Info));
            await Task.Delay(10000);

            GravitonPlugin.Instance.Settings.AccountState.AuthenticateFailed = AuthenticateFailed;
            GravitonPlugin.Instance.Settings.AccountState.UserID = UserID;
            GravitonPlugin.Instance.Settings.AccountState.LastAuthenticated = LastAuthenticated;

            GravitonNotify.Add(new GravitonNotification("graviton.dev.login.test", "Reverted login status", GravitonSeverity.Info));
            testInProgress = false;
        }

        private async void Forbidden_Click(object sender, RoutedEventArgs e)
        {
            if (testInProgress)
                return;

            testInProgress = true;
            var AuthenticateFailed = GravitonPlugin.Instance.Settings.AccountState.AuthenticateFailed;
            var UserID = GravitonPlugin.Instance.Settings.AccountState.UserID;
            var LastAuthenticated = GravitonPlugin.Instance.Settings.AccountState.LastAuthenticated;

            GravitonPlugin.Instance.Settings.AccountState.AuthenticateFailed = System.Net.HttpStatusCode.Forbidden;
            GravitonPlugin.Instance.Settings.AccountState.UserID = 1000;
            GravitonPlugin.Instance.Settings.AccountState.LastAuthenticated = null;

            GravitonNotify.Add(new GravitonNotification("graviton.dev.login.test", "Temporarily set login status to forbidden", GravitonSeverity.Info));
            await Task.Delay(10000);

            GravitonPlugin.Instance.Settings.AccountState.AuthenticateFailed = AuthenticateFailed;
            GravitonPlugin.Instance.Settings.AccountState.UserID = UserID;
            GravitonPlugin.Instance.Settings.AccountState.LastAuthenticated = LastAuthenticated;

            GravitonNotify.Add(new GravitonNotification("graviton.dev.login.test", "Reverted login status", GravitonSeverity.Info));
            testInProgress = false;
        }

        private async void Unauthorized_Click(object sender, RoutedEventArgs e)
        {
            if (testInProgress)
                return;

            testInProgress = true;
            var AuthenticateFailed = GravitonPlugin.Instance.Settings.AccountState.AuthenticateFailed;
            var UserID = GravitonPlugin.Instance.Settings.AccountState.UserID;
            var LastAuthenticated = GravitonPlugin.Instance.Settings.AccountState.LastAuthenticated;

            GravitonPlugin.Instance.Settings.AccountState.AuthenticateFailed = System.Net.HttpStatusCode.Unauthorized;
            GravitonPlugin.Instance.Settings.AccountState.UserID = 1000;
            GravitonPlugin.Instance.Settings.AccountState.LastAuthenticated = null;

            GravitonNotify.Add(new GravitonNotification("graviton.dev.login.test", "Temporarily set login status to Unauthorised", GravitonSeverity.Info));
            await Task.Delay(10000);

            GravitonPlugin.Instance.Settings.AccountState.AuthenticateFailed = AuthenticateFailed;
            GravitonPlugin.Instance.Settings.AccountState.UserID = UserID;
            GravitonPlugin.Instance.Settings.AccountState.LastAuthenticated = LastAuthenticated;

            GravitonNotify.Add(new GravitonNotification("graviton.dev.login.test", "Reverted login status", GravitonSeverity.Info));
            testInProgress = false;
        }

        private async void Reconnecting_Click(object sender, RoutedEventArgs e)
        {
            if (testInProgress)
                return;

            testInProgress = true;
            var AuthenticateFailed = GravitonPlugin.Instance.Settings.AccountState.AuthenticateFailed;
            var UserID = GravitonPlugin.Instance.Settings.AccountState.UserID;
            var LastAuthenticated = GravitonPlugin.Instance.Settings.AccountState.LastAuthenticated;


            GravitonPlugin.Instance.Settings.AccountState.AuthenticateFailed = null;
            GravitonPlugin.Instance.Settings.AccountState.UserID = 1000;
            GravitonPlugin.Instance.Settings.AccountState.LastAuthenticated = DateTime.UtcNow;

            GravitonNotify.Add(new GravitonNotification("graviton.dev.login.test", "Temporarily set login status to reconnecting", GravitonSeverity.Info));
            await Task.Delay(10000);

            GravitonPlugin.Instance.Settings.AccountState.AuthenticateFailed = AuthenticateFailed;
            GravitonPlugin.Instance.Settings.AccountState.UserID = UserID;
            GravitonPlugin.Instance.Settings.AccountState.LastAuthenticated = LastAuthenticated;

            GravitonNotify.Add(new GravitonNotification("graviton.dev.login.test", "Reverted login status", GravitonSeverity.Info));
            testInProgress = false;
        }

        private async void NotLoggedIn_Click(object sender, RoutedEventArgs e)
        {
            if (testInProgress)
                return;

            testInProgress = true;
            var AuthenticateFailed = GravitonPlugin.Instance.Settings.AccountState.AuthenticateFailed;
            var UserID = GravitonPlugin.Instance.Settings.AccountState.UserID;
            var LastAuthenticated = GravitonPlugin.Instance.Settings.AccountState.LastAuthenticated;

            GravitonPlugin.Instance.Settings.AccountState.AuthenticateFailed = null;
            GravitonPlugin.Instance.Settings.AccountState.UserID = -1;
            GravitonPlugin.Instance.Settings.AccountState.LastAuthenticated = null;

            GravitonNotify.Add(new GravitonNotification("graviton.dev.login.test", "Temporarily set login status to Not logged in", GravitonSeverity.Info));
            await Task.Delay(10000);

            GravitonPlugin.Instance.Settings.AccountState.AuthenticateFailed = AuthenticateFailed;
            GravitonPlugin.Instance.Settings.AccountState.UserID = UserID;
            GravitonPlugin.Instance.Settings.AccountState.LastAuthenticated = LastAuthenticated;

            GravitonNotify.Add(new GravitonNotification("graviton.dev.login.test", "Reverted login status", GravitonSeverity.Info));
            testInProgress = false;
        }
    }
}
