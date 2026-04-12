namespace Playnite;

public static partial class Loc
{

    /// <summary>
    /// "Fluent test string"
    /// </summary>
    public static string DownloadViewName()
    {
        return GetString("DownloadViewName");
    }
    public static string DownloadViewTitle()
    {
        return GetString("DownloadViewTitle");
    }
}

public static partial class LocId
{
    public static readonly HashSet<string> StringIds = new()
    {
        "DownloadViewName",
        "DownloadViewTitle"
    };

    /// <summary>
    /// "Fluent test string"
    /// </summary>
    public const string DownloadViewName = "DownloadViewName";
    public const string DownloadViewTitle = "DownloadViewTitle";
}
