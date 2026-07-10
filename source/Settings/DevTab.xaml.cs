using Graviton.Models.Notifications;

using Playnite;

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

            NotificationTestsText.Text = Loc.GetString("NotificationTests");
            SuccessNotificationText.Text = Loc.GetString("SuccessNotificationTest");
            InfoNotificationText.Text = Loc.GetString("InfoNotificationTest");
            WarnNotificationText.Text = Loc.GetString("WarnNotificationTest");
            ErrorNotificationText.Text = Loc.GetString("ErrorNotificationTest");

            LoginTestsText.Text = Loc.GetString("LoginTests");
            TestLoginText.Text = Loc.GetString("TestLogin");
            TestForbiddenText.Text = Loc.GetString("TestForbidden");
            TestUnauthorizedText.Text = Loc.GetString("TestUnauthorized");
            TestReconnectingText.Text = Loc.GetString("TestReconnecting");
            TestNotLoggedInText.Text = Loc.GetString("TestNotLoggedIn");
        }

        private void Success_Click(object sender, RoutedEventArgs e)
        {
            GravitonNotify.Add(new GravitonNotification("graviton.dev.success.test", Loc.GetString("SuccessNotificationTest"), GravitonSeverity.Success));
        }

        private void Info_Click(object sender, RoutedEventArgs e)
        {
            GravitonNotify.Add(new GravitonNotification("graviton.dev.info.test", Loc.GetString("InfoNotificationTest"), GravitonSeverity.Info));
        }

        private void Warn_Click(object sender, RoutedEventArgs e)
        {
            GravitonNotify.Add(new GravitonNotification("graviton.dev.warn.test", Loc.GetString("WarnNotificationTest"), GravitonSeverity.Warn));
        }

        private void Error_Click(object sender, RoutedEventArgs e)
        {
            GravitonNotify.Add(new GravitonNotification("graviton.dev.error.test", Loc.GetString("ErrorNotificationTest"), GravitonSeverity.Error));
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

            GravitonNotify.Add(new GravitonNotification("graviton.dev.login.test", Loc.GetString("LoginTestStarted"), GravitonSeverity.Info));
            await Task.Delay(10000);

            GravitonPlugin.Instance.Settings.AccountState.AuthenticateFailed = AuthenticateFailed;
            GravitonPlugin.Instance.Settings.AccountState.UserID = UserID;
            GravitonPlugin.Instance.Settings.AccountState.LastAuthenticated = LastAuthenticated;

            GravitonNotify.Add(new GravitonNotification("graviton.dev.login.test", Loc.GetString("LoginTestFinished"), GravitonSeverity.Info));
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

            GravitonNotify.Add(new GravitonNotification("graviton.dev.login.test", Loc.GetString("ForbiddenTestStarted"), GravitonSeverity.Info));
            await Task.Delay(10000);

            GravitonPlugin.Instance.Settings.AccountState.AuthenticateFailed = AuthenticateFailed;
            GravitonPlugin.Instance.Settings.AccountState.UserID = UserID;
            GravitonPlugin.Instance.Settings.AccountState.LastAuthenticated = LastAuthenticated;

            GravitonNotify.Add(new GravitonNotification("graviton.dev.login.test", Loc.GetString("ForbiddenTestFinished"), GravitonSeverity.Info));
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

            GravitonNotify.Add(new GravitonNotification("graviton.dev.login.test", Loc.GetString("UnauthorizedTestStarted"), GravitonSeverity.Info));
            await Task.Delay(10000);

            GravitonPlugin.Instance.Settings.AccountState.AuthenticateFailed = AuthenticateFailed;
            GravitonPlugin.Instance.Settings.AccountState.UserID = UserID;
            GravitonPlugin.Instance.Settings.AccountState.LastAuthenticated = LastAuthenticated;

            GravitonNotify.Add(new GravitonNotification("graviton.dev.login.test", Loc.GetString("UnauthorizedTestFinished"), GravitonSeverity.Info));
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

            GravitonNotify.Add(new GravitonNotification("graviton.dev.login.test", Loc.GetString("ReconnectingTestStarted"), GravitonSeverity.Info));
            await Task.Delay(10000);

            GravitonPlugin.Instance.Settings.AccountState.AuthenticateFailed = AuthenticateFailed;
            GravitonPlugin.Instance.Settings.AccountState.UserID = UserID;
            GravitonPlugin.Instance.Settings.AccountState.LastAuthenticated = LastAuthenticated;

            GravitonNotify.Add(new GravitonNotification("graviton.dev.login.test", Loc.GetString("ReconnectingTestFinished"), GravitonSeverity.Info));
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

            GravitonNotify.Add(new GravitonNotification("graviton.dev.login.test", Loc.GetString("NotLoggedInTestStarted"), GravitonSeverity.Info));
            await Task.Delay(10000);

            GravitonPlugin.Instance.Settings.AccountState.AuthenticateFailed = AuthenticateFailed;
            GravitonPlugin.Instance.Settings.AccountState.UserID = UserID;
            GravitonPlugin.Instance.Settings.AccountState.LastAuthenticated = LastAuthenticated;

            GravitonNotify.Add(new GravitonNotification("graviton.dev.login.test", Loc.GetString("NotLoggedInTestFinished"), GravitonSeverity.Info));
            testInProgress = false;
        }
    }
}
