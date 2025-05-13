using DCL.Passport;
using Segment.Serialization;
using System;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
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
            this.passportController.JumpToFriendClicked += JumpToFriendClicked;
            this.passportController.NameClaimRequested += OnNameClaimRequested;
        }

        public void Dispose()
        {
            passportController.PassportOpened -= OnPassportOpened;
            passportController.BadgesSectionOpened -= OnBadgesSectionOpened;
            passportController.BadgeSelected -= OnBadgeSelected;
            passportController.JumpToFriendClicked -= JumpToFriendClicked;
            passportController.NameClaimRequested -= OnNameClaimRequested;
        }

        private void JumpToFriendClicked(string targetAddress, Vector2Int parcel) =>
            analytics.Track(AnalyticsEvents.Friends.JUMP_TO_FRIEND_CLICKED, new JsonObject
            {
                {"receiver_id", targetAddress},
                {"friend_position", parcel.ToString()},
            });

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
            }, isInstant: eventName == AnalyticsEvents.Profile.PASSPORT_OPENED);
        }

        private void OnNameClaimRequested() =>
            analytics.Track(AnalyticsEvents.Profile.NAME_CLAIM_REQUESTED);
    }
}
