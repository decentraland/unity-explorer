using DCL.Passport;
using Segment.Serialization;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class OpenPassportAnalytics : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly PassportController passportController;

        public OpenPassportAnalytics(IAnalyticsController analytics, PassportController passportController)
        {
            this.analytics = analytics;
            this.passportController = passportController;

            this.passportController.PassportOpened += OnPassportOpened;
        }

        public void Dispose()
        {
            passportController.PassportOpened -= OnPassportOpened;
        }

        private void OnPassportOpened(string userId)
        {
            analytics.Track(AnalyticsEvents.UI.PASSPORT_OPENED, new JsonObject
            {
                { "receiver_id", userId },
            });
        }
    }
}
