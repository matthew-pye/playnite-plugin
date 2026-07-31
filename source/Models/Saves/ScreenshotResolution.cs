using System.ComponentModel;

namespace Graviton.Models.Saves
{
    public enum ScreenshotResolution
    {
        [Description("720p")] P720 = 720,
        [Description("1080p")] P1080 = 1080,
        [Description("1440p")] P1440 = 1440,
        [Description("4k")] UHD4K = 2160
    }
}