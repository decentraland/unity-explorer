using DCL.PerformanceAndDiagnostics.Analytics;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;

namespace DCL.Profiles
{
    public class ProfilesAnalytics
    {
        private readonly ConcurrentDictionary<string, DateTime> startedRequests = new (StringComparer.CurrentCultureIgnoreCase);

        private readonly ProfilesDebug profilesDebug;
        private readonly IAnalyticsController analyticsController;

        public ProfilesAnalytics(ProfilesDebug profilesDebug, IAnalyticsController analyticsController)
        {
            this.profilesDebug = profilesDebug;
            this.analyticsController = analyticsController;
        }

        internal void OnProfileRetrievalStarted(string id) =>
            startedRequests[id] = DateTime.Now;

        internal void OnProfileResolved(string id, bool asAggregated)
        {
            if (asAggregated)
                profilesDebug.AddAggregated();
            else
                profilesDebug.AddNonAggregated();

            if (startedRequests.TryRemove(id, out DateTime startTime))
            {
                analyticsController.Track(AnalyticsEvents.Endpoints.PROFILE_RETRIEVED,
                    new JObject { { "duration", (ulong)(DateTime.Now - startTime).TotalMilliseconds } });
            }
        }

        internal void OnProfileResolutionFailed(string id, int version)
        {
            profilesDebug.AddMissing(id);

            if (startedRequests.TryRemove(id, out DateTime startTime))
            {
                analyticsController.Track(AnalyticsEvents.Endpoints.PROFILE_RETRIEVED,
                    new JObject
                    {
                        { "duration", (ulong)(DateTime.Now - startTime).TotalMilliseconds },
                        { "user_id", id },
                        { "version", version },
                    });
            }
        }
    }
}
