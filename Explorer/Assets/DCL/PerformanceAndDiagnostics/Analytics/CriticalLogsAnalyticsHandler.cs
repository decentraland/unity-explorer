using Newtonsoft.Json.Linq;
using System;
using System.Runtime.CompilerServices;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class CriticalLogsAnalyticsHandler
    {
        private const int PAYLOAD_LIMIT = 28 * 1024; // Segment == 32 KB, leaving some room for headers
        private const int SAFE_CHAR_LIMIT = PAYLOAD_LIMIT / 4; // 7,680 characters

        private readonly IAnalyticsController analytics;

        public CriticalLogsAnalyticsHandler(IAnalyticsController analytics)
        {
            this.analytics = analytics;
            AppDomain.CurrentDomain.UnhandledException += TrackUnhandledException;
        }

        ~CriticalLogsAnalyticsHandler()
        {
            AppDomain.CurrentDomain.UnhandledException -= TrackUnhandledException;
        }

        private void TrackUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            var e = (Exception)args.ExceptionObject;

            analytics.Track(AnalyticsEvents.General.ERROR, new JObject
            {
                { "type", e.GetType().ToString() },
                { "message", TrimToPayloadLimit(e.Message) },
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string TrimToPayloadLimit(string message)
        {
            if (string.IsNullOrEmpty(message))
                return "Reported message was null or empty";

            return message.Length <= SAFE_CHAR_LIMIT
                ? message
                : message[..SAFE_CHAR_LIMIT];
        }
    }
}
