using System.IO;
using System.Runtime.CompilerServices;

namespace Graviton.Models.Notifications
{
    public struct GravitonNotification
    {
        public GravitonNotification(string ID, string Message, GravitonSeverity Severity, Exception? ex = null, [CallerLineNumber] int LineNumber = 0, [CallerFilePath] string File = "")
        {
            id = ID;
            message = Message;
            severity = Severity;
            exeption = ex;
            lineNumber = LineNumber;
            file = Path.GetFileName(File);
        }

        public string id;
        public string message;
        public GravitonSeverity severity;

        public Exception? exeption;
        public int lineNumber;
        public string file;
    }
}
