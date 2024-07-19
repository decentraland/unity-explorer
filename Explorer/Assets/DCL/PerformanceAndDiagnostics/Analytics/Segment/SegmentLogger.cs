using DCL.Diagnostics;
using Segment.Analytics.Utilities;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class SegmentLogger : ISegmentLogger
    {
        private const string EMPTY_MESSAGE = "Log message was empty.";

        public void Log(LogLevel logLevel, Exception exception = null, string message = null)
        {
            switch (logLevel)
            {
                case LogLevel.Critical:
                case LogLevel.Trace:
                case LogLevel.Error:
                    if (exception != null)
                        ReportHub.LogException(exception, ReportCategory.ANALYTICS);
                    else
                        ReportHub.LogError(ReportCategory.ANALYTICS, message ?? EMPTY_MESSAGE);

                    break;
                case LogLevel.Warning:
                    ReportHub.LogWarning(ReportCategory.ANALYTICS, message ?? EMPTY_MESSAGE);
                    break;
                case LogLevel.Information:
                case LogLevel.Debug:
                    ReportHub.Log(ReportCategory.ANALYTICS, message ?? EMPTY_MESSAGE);
                    break;
                case LogLevel.None:
                default:
                    break;
            }
        }
    }
}
