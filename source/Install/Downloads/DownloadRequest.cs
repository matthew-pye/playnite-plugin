
using Playnite;

using static Playnite.InstallController;

namespace RomMLibrary.Install.Downloads
{
    public class DownloadRequest
    {
        public string GameId { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string InstallDir { get; set; } = string.Empty;     // folder where to extract/install
        public string GamePath { get; set; } = string.Empty;       // full path to the downloaded file on disk
        public bool HasMultipleFiles { get; set; }  // whether archive contains multiple top-level files
        public bool AutoExtract { get; set; } = true;
        public bool Use7z { get; set; } = false;
        public string PathTo7Z { get; set; } = "";


        /// Optional function used after extraction to build rom list for Playnite
        public Func<List<Game>>? BuildRoms { get; set; }

        // Callbacks
        public Action<GameInstalledArgs>? OnInstalled { get; set; }
        public Action? OnCancelled { get; set; }
        public Action<Exception>? OnFailed { get; set; }
    }
}