using DCL.Diagnostics;
using Microsoft.Extensions.Logging;
using System;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace DCL.WebRequests.ChromeDevtool
{
    public class DCLLogger : ILogger
    {
        private readonly ReportData reportData;

        public DCLLogger(ReportData reportData)
        {
            this.reportData = reportData;
        }

        public IDisposable BeginScope<TState>(TState state) where TState: notnull
        {
            // Scopes are used for structured logging; not needed in Unity
            return NullScope.INSTANCE;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            string message = formatter(state, exception);

            if (exception != null)
                message += $"\nException: {exception}";

            switch (logLevel)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                case LogLevel.Information:
                    ReportHub.Log(reportData, message);
                    break;
                case LogLevel.Warning:
                    ReportHub.LogWarning(reportData, message);
                    break;
                case LogLevel.Error:
                case LogLevel.Critical:
                    ReportHub.LogError(reportData, message);
                    break;
                case LogLevel.None:
                    break;
                default:
                    ReportHub.Log(reportData, message);
                    break;
            }
        }

        private class NullScope : IDisposable
        {
            public static readonly NullScope INSTANCE = new ();

            public void Dispose() { }
        }
    }
}
