using System;
using System.Globalization;
using UnityEngine;

namespace DCL.BadgesAPIService
{
    public static class BadgesUtils
    {
        private const string NEW_BADGES_LOCAL_STORAGE_KEY = "NewBadges";

        public static string FormatTimestampDate(string timestampString)
        {
            DateTime date = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(timestampString)).DateTime;
            var formattedDate = date.ToString("MMM. yyyy", CultureInfo.InvariantCulture);
            return formattedDate;
        }

        public static bool IsBadgeNew(string badgeId)
        {
            string allNewBadges = PlayerPrefs.GetString(NEW_BADGES_LOCAL_STORAGE_KEY, string.Empty);
            return allNewBadges.Contains(badgeId);
        }

        public static void SetBadgeAsNew(string badgeId)
        {
            string allNewBadges = PlayerPrefs.GetString(NEW_BADGES_LOCAL_STORAGE_KEY, string.Empty);

            if (allNewBadges.Contains(badgeId))
                return;

            PlayerPrefs.SetString(NEW_BADGES_LOCAL_STORAGE_KEY, $"{allNewBadges}{badgeId},");
            PlayerPrefs.Save();
        }

        public static void SetBadgeAsRead(string badgeId)
        {
            string allNewBadges = PlayerPrefs.GetString(NEW_BADGES_LOCAL_STORAGE_KEY, string.Empty);
            PlayerPrefs.SetString(NEW_BADGES_LOCAL_STORAGE_KEY, allNewBadges.Replace($"{badgeId},", string.Empty));
            PlayerPrefs.Save();
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
