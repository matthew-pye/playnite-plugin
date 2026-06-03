using System.IO;
using System.Runtime.CompilerServices;

namespace Graviton.Models.Notifications
{
    public struct GravitonNotification
    {
        public GravitonNotification(string ID, string Message, GravitonSeverity Severity, [CallerLineNumber] int LineNumber = 0, [CallerFilePath] string File = "")
        {
            id = ID;
            message = Message;
            severity = Severity;
            lineNumber = LineNumber;
            file = Path.GetFileName(File);
        }

        public string id;
        public string message;
        public GravitonSeverity severity;

        public int lineNumber;
        public string file;
    }
}
