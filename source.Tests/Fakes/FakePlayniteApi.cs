using NSubstitute;

using Playnite;

using System.IO;

namespace Graviton.Tests.Fakes
{
    public class FakePlayniteApi
    {
        public IPlayniteApi Api { get; }

        public FakeLibraryApi Library { get; } = new();
        public IDialogs Dialogs { get; } = Substitute.For<IDialogs>();
        public IMainViewApi MainView { get; } = Substitute.For<IMainViewApi>();
        public INotificationsAPI Notifications { get; } = Substitute.For<INotificationsAPI>();
        public IApplicationInfoApi AppInfo { get; } = Substitute.For<IApplicationInfoApi>();
        public IApplicationSettingsApi Settings { get; } = Substitute.For<IApplicationSettingsApi>();
        public IAddonsApi Addons { get; } = Substitute.For<IAddonsApi>();
        public IWebViewApi WebView { get; } = Substitute.For<IWebViewApi>();
        public IUriHandlerAPI UriHandler { get; } = Substitute.For<IUriHandlerAPI>();

        public string UserDataDir { get; set; } = Path.Combine(Path.GetTempPath(), "GravitonTests", Guid.NewGuid().ToString("N"));

        public FakePlayniteApi()
        {
            var api = Substitute.For<IPlayniteApi>();

            api.Library.Returns(Library);
            api.Dialogs.Returns(Dialogs);
            api.MainView.Returns(MainView);
            api.Notifications.Returns(Notifications);
            api.AppInfo.Returns(AppInfo);
            api.Settings.Returns(Settings);
            api.Addons.Returns(Addons);
            api.WebView.Returns(WebView);
            api.UriHandler.Returns(UriHandler);
            api.UserDataDir.Returns(_ => UserDataDir);

            Api = api;
        }
    }
}


