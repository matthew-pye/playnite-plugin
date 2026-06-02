using Playnite;

namespace Graviton.Install.Downloads
{
    public class RomMDownloadsAppViewItem : AppViewItem
    {
        private GravitonPlugin _plugin { get => GravitonPlugin.Instance; }
        private IPlayniteApi _playniteAPI { get => GravitonPlugin.PlayniteApi; }
        private ILogger _logger { get => GravitonPlugin.Logger; }

        public RomMDownloadsAppViewItem()
        {
            if(_plugin.DownloadQueueController == null)
                throw new ArgumentNullException("Download controller is null, cannot continue!");
            View = new RomMDownloadQueueControl(_plugin.DownloadQueueController);
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