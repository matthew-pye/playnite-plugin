using Playnite;

namespace RomMLibrary.Install.Downloads
{
    public class RomMDownloadsSidebarItem : AppViewItem
    {
        private readonly IPlayniteApi PlayniteApi;
        private readonly RomMLibraryPlugin Plugin;
        private SidebarItemControl? sidebarRoot;

        public RomMDownloadsSidebarItem(RomMLibraryPlugin plugin)
        {
            Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            PlayniteApi = RomMLibraryPlugin.PlayniteApi ?? throw new ArgumentNullException("PlayniteAPI was null, cannot continue!");

            View = new RomMDownloadQueueControl(Plugin.DownloadQueueController);
        }

        public override async Task ActivateViewAsync(ActivateViewAsyncArgs args)
        {
            await Task.CompletedTask;
            //throw new NotImplementedException();
        }

        public override async Task DeactivateViewAsync(DeactivateViewAsyncArgs args)
        {
            await Task.CompletedTask;
            //throw new NotImplementedException();
        }
    }
}
