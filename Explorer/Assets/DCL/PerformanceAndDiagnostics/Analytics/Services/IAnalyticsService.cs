using Segment.Analytics;
using Segment.Serialization;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    /// <summary>
    /// For the events we use the convention of all lower cases and "_" instead of space
    /// </summary>
    public interface IAnalyticsService
    {
        void Identify(string userId, JsonObject? traits = null);

        /// <summary>
        ///     To track an event you have to call identify first
        /// </summary>
        void Track(string eventName, JsonObject? properties = null);

        void AddPlugin(EventPlugin plugin);

        void Flush();
    }
}
