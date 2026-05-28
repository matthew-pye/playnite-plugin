using Playnite;

namespace Graviton.Install.Downloads
{
    public class RomMDownloadsAppViewItem : AppViewItem
    {
        private readonly GravitonPlugin Plugin;
    
        public RomMDownloadsAppViewItem(GravitonPlugin plugin)
        {
            Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            if(Plugin.DownloadQueueController == null)
                throw new ArgumentNullException("Download controller is null, cannot continue!");
            View = new RomMDownloadQueueControl(Plugin.DownloadQueueController);
        }
    
        public override async Task ActivateViewAsync(ActivateViewAsyncArgs args)
        {
            await Task.CompletedTask;
        }
    
        public override async Task DeactivateViewAsync(DeactivateViewAsyncArgs args)
        {
            await Task.CompletedTask;
        }
    }
}