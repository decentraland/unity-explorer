using DCL.BadgesAPIService;
using System;

namespace DCL.Passport.Utils
{
    public static class BadgesUtils
    {
        public static string FormatTimestampDate(string timestampString)
        {
            DateTime date = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(timestampString)).DateTime;
            var formattedDate = date.ToString("MMM. yyyy", System.Globalization.CultureInfo.InvariantCulture);
            return formattedDate;
        }

        public static string GetTierCompletedDate(this BadgeInfo badgeInfo, string tierId)
        {
            string tierCompletedAt = string.Empty;

            if (badgeInfo.progress.achievedTiers != null)
            {
                foreach (var achievedTier in badgeInfo.progress.achievedTiers)
                {
                    if (tierId != achievedTier.tierId)
                        continue;

                    tierCompletedAt = achievedTier.completedAt;
                    break;
                }
            }

            return tierCompletedAt;
        }
    }
}
