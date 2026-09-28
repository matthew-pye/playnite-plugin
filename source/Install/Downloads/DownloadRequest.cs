namespace Graviton.Install.Downloads
{
    public class DownloadRequest
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        public string DownloadUrl { get; set; } = string.Empty;
        public string DownloadPath { get; set; } = string.Empty;

        public Func<DownloadQueueItem, DownloadRequest, Task> OnDownloadComplete { get; set; } = async (item, req) => { };
        public Func<Task> OnCancelled { get; set; } = async () => { };
        public Func<Exception, Task> OnFailed { get; set; } = async (ex) => { };
    }
}