using Graviton.Install;
using Graviton.Models.Install;
using Graviton.Models.Notifications;
using Graviton.Models.RomM.Rom;

using Playnite;

using System.Collections.ObjectModel;
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
            GravitonNotify.Notify("graviton.dev.success.test", "Success Notification", GravitonSeverity.Success);
        }

        private void Info_Click(object sender, RoutedEventArgs e)
        {
            GravitonNotify.Notify("graviton.dev.info.test", "Info Notification", GravitonSeverity.Info);
        }

        private void Warn_Click(object sender, RoutedEventArgs e)
        {
            GravitonNotify.Notify("graviton.dev.warn.test", "Warn Notification", GravitonSeverity.Warn);
        }

        private void Error_Click(object sender, RoutedEventArgs e)
        {
            GravitonNotify.Notify("graviton.dev.error.test", "Error Notification", GravitonSeverity.Error);
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

            GravitonNotify.Notify("graviton.dev.login.test", "Temporarily set login status to Logged In", GravitonSeverity.Info);
            await Task.Delay(10000);

            GravitonPlugin.Instance.Settings.AccountState.AuthenticateFailed = AuthenticateFailed;
            GravitonPlugin.Instance.Settings.AccountState.UserID = UserID;
            GravitonPlugin.Instance.Settings.AccountState.LastAuthenticated = LastAuthenticated;

            GravitonNotify.Notify("graviton.dev.login.test", "Reverted login status", GravitonSeverity.Info);
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

            GravitonNotify.Notify("graviton.dev.login.test", "Temporarily set login status to forbidden", GravitonSeverity.Info);
            await Task.Delay(10000);

            GravitonPlugin.Instance.Settings.AccountState.AuthenticateFailed = AuthenticateFailed;
            GravitonPlugin.Instance.Settings.AccountState.UserID = UserID;
            GravitonPlugin.Instance.Settings.AccountState.LastAuthenticated = LastAuthenticated;

            GravitonNotify.Notify("graviton.dev.login.test", "Reverted login status", GravitonSeverity.Info);
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

            GravitonNotify.Notify("graviton.dev.login.test", "Temporarily set login status to Unauthorised", GravitonSeverity.Info);
            await Task.Delay(10000);

            GravitonPlugin.Instance.Settings.AccountState.AuthenticateFailed = AuthenticateFailed;
            GravitonPlugin.Instance.Settings.AccountState.UserID = UserID;
            GravitonPlugin.Instance.Settings.AccountState.LastAuthenticated = LastAuthenticated;

            GravitonNotify.Notify("graviton.dev.login.test", "Reverted login status", GravitonSeverity.Info);
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

            GravitonNotify.Notify("graviton.dev.login.test", "Temporarily set login status to reconnecting", GravitonSeverity.Info);
            await Task.Delay(10000);

            GravitonPlugin.Instance.Settings.AccountState.AuthenticateFailed = AuthenticateFailed;
            GravitonPlugin.Instance.Settings.AccountState.UserID = UserID;
            GravitonPlugin.Instance.Settings.AccountState.LastAuthenticated = LastAuthenticated;

            GravitonNotify.Notify("graviton.dev.login.test", "Reverted login status", GravitonSeverity.Info);
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

            GravitonNotify.Notify("graviton.dev.login.test", "Temporarily set login status to Not logged in", GravitonSeverity.Info);
            await Task.Delay(10000);

            GravitonPlugin.Instance.Settings.AccountState.AuthenticateFailed = AuthenticateFailed;
            GravitonPlugin.Instance.Settings.AccountState.UserID = UserID;
            GravitonPlugin.Instance.Settings.AccountState.LastAuthenticated = LastAuthenticated;

            GravitonNotify.Notify("graviton.dev.login.test", "Reverted login status", GravitonSeverity.Info);
            testInProgress = false;
        }

        private void SequentialCandidateSelector_Click(object sender, RoutedEventArgs e)
        {
            var window = GravitonPlugin.PlayniteApi.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = false,
                ShowMaximizeButton = true,
                ShowCloseButton = true,
                DefaultWidth = 800,
                DefaultHeight = 450
            });

            ObservableCollection<UpdateDLCCandidate> candidates = new()
            {
                new("v1.1", "v1.1", RomMCategory.Update, [1,2,3], null, null, 1245363),
                new("v1.2", "v1.2", RomMCategory.Update, [1,2,3], null, null, 43634),
                new("v1.3", "v1.3", RomMCategory.Update, [1,2,3], null, null, 3463463463),
                new("v1.4", "v1.4", RomMCategory.Update, [1,2,3], null, null, 344),
                new("v2.0", "v2.0", RomMCategory.Update, [1,2,3], null, null, 347433476),
                new("v2.3", "v2.3", RomMCategory.Update, [1,2,3], null, null, 34634),
                new("v3.1", "v3.1", RomMCategory.Update, [1,2,3], null, null, 4585346389342),
                new("v3.3", "v3.3", RomMCategory.Update, [1,2,3], null, null, 5685),
            };

            var selector = new CandidateSelector(candidates, Models.Install.InstallMode.Sequential, "Updates");

            window.Title = $"Install Updates";
            window.Content = selector;
            window.Owner = GravitonPlugin.PlayniteApi.GetLastActiveWindow();
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();
        }

        private void SelectManyCandidateSelector_Click(object sender, RoutedEventArgs e)
        {
            var window = GravitonPlugin.PlayniteApi.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = false,
                ShowMaximizeButton = true,
                ShowCloseButton = true,
                DefaultWidth = 800,
                DefaultHeight = 450
            });

            ObservableCollection<UpdateDLCCandidate> candidates = new()
            {
                new("v1.1", "v1.1", RomMCategory.Update, [1,2,3], null, null, 1245363),
                new("v1.2", "v1.2", RomMCategory.Update, [1,2,3], null, null, 43634),
                new("v1.3", "v1.3", RomMCategory.Update, [1,2,3], null, null, 3463463463),
                new("v1.4", "v1.4", RomMCategory.Update, [1,2,3], null, null, 344),
                new("v2.0", "v2.0", RomMCategory.Update, [1,2,3], null, null, 347433476),
                new("v2.3", "v2.3", RomMCategory.Update, [1,2,3], null, null, 34634),
                new("v3.1", "v3.1", RomMCategory.Update, [1,2,3], null, null, 4585346389342),
                new("v3.3", "v3.3", RomMCategory.Update, [1,2,3], null, null, 5685),
            };

            var selector = new CandidateSelector(candidates, Models.Install.InstallMode.SelectMany, "Updates");

            window.Title = $"Install Updates";
            window.Content = selector;
            window.Owner = GravitonPlugin.PlayniteApi.GetLastActiveWindow();
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();
        }

        private void SelectOneCandidateSelector_Click(object sender, RoutedEventArgs e)
        {
            var window = GravitonPlugin.PlayniteApi.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = false,
                ShowMaximizeButton = true,
                ShowCloseButton = true,
                DefaultWidth = 800,
                DefaultHeight = 450
            });

            ObservableCollection<UpdateDLCCandidate> candidates = new()
            {
                new("v1.1", "v1.1", RomMCategory.Update, [1,2,3], null, null, 1245363),
                new("v1.2", "v1.2", RomMCategory.Update, [1,2,3], null, null, 43634),
                new("v1.3", "v1.3", RomMCategory.Update, [1,2,3], null, null, 3463463463),
                new("v1.4", "v1.4", RomMCategory.Update, [1,2,3], null, null, 344),
                new("v2.0", "v2.0", RomMCategory.Update, [1,2,3], null, null, 347433476),
                new("v2.3", "v2.3", RomMCategory.Update, [1,2,3], null, null, 34634),
                new("v3.1", "v3.1", RomMCategory.Update, [1,2,3], null, null, 4585346389342),
                new("v3.3", "v3.3", RomMCategory.Update, [1,2,3], null, null, 5685),
            };

            var selector = new CandidateSelector(candidates, Models.Install.InstallMode.SelectOne, "Updates");

            window.Title = $"Install Updates";
            window.Content = selector;
            window.Owner = GravitonPlugin.PlayniteApi.GetLastActiveWindow();
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();
        }
    }
}
