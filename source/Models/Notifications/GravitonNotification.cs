using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Graviton.Models.Notifications
{
    public struct GravitonNotification
    {
        public GravitonNotification(string ID, string Message, GravitonSeverity Severity, [CallerLineNumber] int LineNumber = 0, [CallerMemberName] string Method = "")
        {
            id = ID;
            message = Message;
            severity = Severity;
            lineNumber = LineNumber;
            method = Method;
        }

        public string id;
        public string message;
        public GravitonSeverity severity;

        public int lineNumber;
        public string method;
    }
}
