using DCL.Passport;
using Segment.Serialization;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class PassportAnalytics : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly PassportController passportController;

        public PassportAnalytics(IAnalyticsController analytics, PassportController passportController)
        {
            this.analytics = analytics;
            this.passportController = passportController;

            this.passportController.PassportOpened += OnPassportOpened;
            this.passportController.BadgesSectionOpened += OnBadgesSectionOpened;
            this.passportController.BadgeSelected += OnBadgeSelected;
        }

        public void Dispose()
        {
            passportController.PassportOpened -= OnPassportOpened;
            passportController.BadgesSectionOpened -= OnBadgesSectionOpened;
            this.passportController.BadgeSelected -= OnBadgeSelected;
        }

        private void OnBadgeSelected(string id, bool isOwnPassport)
        {
            analytics.Track(AnalyticsEvents.Profile.BADGE_UI_CLICK, new JsonObject
            {
                { "badge_id", id }, {"own_passport", isOwnPassport}
            });
        }

        private void OnBadgesSectionOpened(string userId, bool isOwnPassport, string origin)
        {
            analytics.Track(AnalyticsEvents.Profile.BADGES_TAB_OPENED, new JsonObject
            {
                { "receiver_id", userId }, {"own_passport", isOwnPassport}, {"origin", origin},
            });
        }

        private void OnPassportOpened(string userId, bool isOwnPassport)
        {
            string eventName = isOwnPassport ? AnalyticsEvents.Profile.OWN_PROFILE_OPENED : AnalyticsEvents.Profile.PASSPORT_OPENED;

            analytics.Track(eventName, new JsonObject
            {
                { "receiver_id", userId },
            });
        }
    }
}
