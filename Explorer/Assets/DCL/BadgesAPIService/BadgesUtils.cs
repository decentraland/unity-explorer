using DCL.Prefs;
using System;
using System.Globalization;

namespace DCL.BadgesAPIService
{
    public static class BadgesUtils
    {
        public static string FormatTimestampDate(string timestampString)
        {
            DateTime date = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(timestampString)).DateTime;
            var formattedDate = date.ToString("MMM. yyyy", CultureInfo.InvariantCulture);
            return formattedDate;
        }

        public static bool IsBadgeNew(string badgeId)
        {
            string allNewBadges = DCLPlayerPrefs.GetString(DCLPrefKeys.NEW_BADGES, string.Empty);
            return allNewBadges.Contains(badgeId);
        }

        public static void SetBadgeAsNew(string badgeId)
        {
            string allNewBadges = DCLPlayerPrefs.GetString(DCLPrefKeys.NEW_BADGES, string.Empty);

            if (allNewBadges.Contains(badgeId))
                return;

            DCLPlayerPrefs.SetString(DCLPrefKeys.NEW_BADGES, $"{allNewBadges}{badgeId},");
            DCLPlayerPrefs.Save();
        }

        public static void SetBadgeAsRead(string badgeId)
        {
            string allNewBadges = DCLPlayerPrefs.GetString(DCLPrefKeys.NEW_BADGES, string.Empty);
            DCLPlayerPrefs.SetString(DCLPrefKeys.NEW_BADGES, allNewBadges.Replace($"{badgeId},", string.Empty));
            DCLPlayerPrefs.Save();
        }

        public static string GetTierCompletedDate(this BadgeInfo badgeInfo, string tierId)
        {
            string tierCompletedAt = string.Empty;

            if (badgeInfo.data.progress.achievedTiers != null)
            {
                foreach (var achievedTier in badgeInfo.data.progress.achievedTiers)
                {
                    if (tierId != achievedTier.tierId)
                        continue;

                    tierCompletedAt = achievedTier.completedAt;
                    break;
                }
            }

            return tierCompletedAt;
        }

        public static int GetProgressPercentage(this in BadgeInfo badgeInfo) =>
            badgeInfo.data.progress.stepsDone * 100 / (badgeInfo.data.progress.nextStepsTarget ?? badgeInfo.data.progress.totalStepsTarget);
    }
}
