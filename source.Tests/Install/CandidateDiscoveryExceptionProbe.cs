using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

namespace Graviton.Tests.Install;

internal sealed class CandidateDiscoveryExceptionProbe : IDisposable
{
    private readonly ConcurrentQueue<string> _exceptions = new();
    private readonly EventHandler<FirstChanceExceptionEventArgs> _handler;

    public CandidateDiscoveryExceptionProbe()
    {
        _handler = (_, args) =>
        {
            var trace = args.Exception.StackTrace;
            if (trace?.Contains("InstallUpdateDLC.FindCategoryCandidates", StringComparison.Ordinal) == true)
            {
                _exceptions.Enqueue(args.Exception.ToString());
            }
        };

        AppDomain.CurrentDomain.FirstChanceException += _handler;
    }

    public string Details => _exceptions.IsEmpty
        ? "No exception was observed inside FindCategoryCandidates."
        : string.Join(Environment.NewLine + "---" + Environment.NewLine, _exceptions.Take(5));

    public void Dispose()
    {
        AppDomain.CurrentDomain.FirstChanceException -= _handler;
    }
}
