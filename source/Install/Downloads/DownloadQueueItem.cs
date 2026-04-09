using CommunityToolkit.Mvvm.ComponentModel;

namespace RomMLibrary.Install.Downloads
{
    public enum DownloadStatus
    {
        Queued,
        Downloading,
        Extracting,
        Completed,
        Failed,
        Canceled
    }

    public class DownloadQueueItem : ObservableObject
    {
        public string GameId { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
        public DownloadStatus Status { get; set; }

        public bool IsIndeterminate { get; set; }
        public double ProgressValue { get; set; }     // bytes or 0..100 depending on stage
        public double ProgressMaximum { get; set; }   // bytes or 100

        public DateTime QueuedOn { get; set; }
        public CancellationTokenSource Cts { get; set; } = new CancellationTokenSource();

        public void SetStatus(DownloadStatus st, string text)
        {
            Status = st;
            StatusText = text;
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(StatusText));
        }

        public void SetProgress(double value, double max, bool indeterminate)
        {
            ProgressValue = value;
            ProgressMaximum = max < 1 ? 1 : max;
            IsIndeterminate = indeterminate;
            OnPropertyChanged(nameof(ProgressValue));
            OnPropertyChanged(nameof(ProgressMaximum));
            OnPropertyChanged(nameof(IsIndeterminate));
        }
    }
}