using Graviton.Settings;

using Playnite;

using System.Runtime.CompilerServices;
using System.Text;

namespace Graviton.Notifications
{
    [InterpolatedStringHandler]
    public struct DebugLogInterpolatedStringHandler
    {
        private StringBuilder? _builder;

        public DebugLogInterpolatedStringHandler(int literalLength, int formattedCount, GravitonLogger logger, out bool handlerIsValid)
        {
            // Return if debug is not enabled by the user
            if (!logger.DebugEnabled)
            {
                _builder = null;
                handlerIsValid = false;
                return;
            }

            // String builder capacity is a guess at an average formatted variable length
            _builder = new StringBuilder(literalLength + formattedCount * 8);
            handlerIsValid = true;
        }

        public void AppendLiteral(string s) => _builder!.Append(s);

        public void AppendFormatted<T>(T value) => _builder!.Append(value);
        public void AppendFormatted<T>(T value, string? format) =>_builder!.Append(value is IFormattable f ? f.ToString(format, null) : value);

        public string GetFormattedTextOrEmpty() => _builder?.ToString() ?? string.Empty;
    }

    public class GravitonLogger
    {
        GravitonPluginSettings Settings => GravitonPlugin.Instance.Settings;

        internal ILogger Logger { get; private set; } = null!;
        internal ILogger DebugLogger { get; private set; } = null!;

        public void Initialize()
        {
            Logger = LogManager.GetLogger<GravitonPlugin>();
            DebugLogger = LogManager.GetLogger<GravitonLogger>("Debug");
        }

        #region Debug Logging
        public bool DebugEnabled => Settings.DebuggingEnabled;

        public void Debug(object? message)
        {
            if (DebugEnabled)
                DebugLogger?.Info(message);
        }

        // Using InterpolatedStringHandlerArgument so when the user doesn't have debug logging enabled the compiler skips over 
        // debug logs which skips the string formatting and any "work" on variables
        // So minimal performance impact when these log calls are spammed in the codebase
        // e.g. GravitonLogger.Debug($"Bob's profile: {JsonSerializer.Serialize(bob)}")
        // So when debugging is disabled this won't incur the cost of serializing bob
        public void Debug([InterpolatedStringHandlerArgument("")] ref DebugLogInterpolatedStringHandler message)
        {
            if (!DebugEnabled)
                return;

            DebugLogger?.Info(message.GetFormattedTextOrEmpty());
        }

        public void Debug(Exception exception, string message)
        {
            if (DebugEnabled)
                DebugLogger?.Error(exception, message);
        }

        public void Debug(Exception exception, object message)
        {
            if (DebugEnabled)
                DebugLogger?.Error(exception, message);
        }

        public void Trace([InterpolatedStringHandlerArgument("")] ref DebugLogInterpolatedStringHandler message)
        {
            if (!DebugEnabled)
                return;

            DebugLogger?.Info(message.GetFormattedTextOrEmpty());
        }

        public void Trace(object? message)
        {
            if (DebugEnabled)
                DebugLogger?.Info(message);
        }

        public void Trace(Exception exception, string message)
        {
            if (DebugEnabled)
                DebugLogger?.Warn(exception, message);
        }

        public void Trace(Exception exception, object message)
        {
            if (DebugEnabled)
                DebugLogger?.Warn(exception, message);
        }
        #endregion

        #region Normal Logging
        public void Info(string message)
        {
            Logger?.Info(message);
        }

        public void Info(object? message)
        {
            Logger?.Info(message);
        }

        public void Warn(string message)
        {
            Logger?.Warn(message);
        }

        public void Warn(object? message)
        {
            Logger?.Warn(message);
        }

        public void Warn(Exception exception, string message)
        {
            Logger?.Warn(exception, message);
        }

        public void Warn(Exception exception, object message)
        {
            Logger?.Warn(exception, message);
        }

        public void Error(string message)
        {
            Logger?.Error(message);
        }

        public void Error(object? message)
        {
            Logger?.Error(message);
        }

        public void Error(Exception exception, string message)
        {
            Logger?.Error(exception, message);
        }

        public void Error(Exception exception, object message)
        {
            Logger?.Error(exception, message);
        }
        #endregion
    }
}