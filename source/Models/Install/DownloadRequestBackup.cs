namespace Graviton.Models.Install
{
    public enum InstallType
    {
        BaseGame,
        Remote,
        UpdateDLC
    }

    public class DownloadRequestBackup
    {
        public InstallType InstallType { get; set; }

        public string GameID { get; set; } = "";
        public string ID { get; set; } = "";
        public string DownloadPath { get; set; } = "";
        public string InstallDir { get; set; } = "";

        // Only used by Update/DLC
        public UpdateDLCCandidate? Candidate { get; set; }
        public string? Category { get; set; }
        public InstallStyles? InstallStyle { get; set; }
        public string? PreviousRequestId { get; set; }

        // Only used by remote installs
        public string DownloadURL { get; set; } = "";
    }

}
