using DCL.MarketplaceCreditsAPIService;
using System;

namespace DCL.MarketplaceCredits
{
    public static class MarketplaceCreditsUtils
    {
        public const string INFO_LINK = "https://docs.decentraland.org/";
        public const string GO_SHOPPING_LINK = "https://decentraland.org/marketplace/";

        public static string FormatEndOfTheWeekDateTimestamp(string timestamp)
        {
            if (!DateTime.TryParse(timestamp, out DateTime targetTime))
                return "Invalid timestamp";

            TimeSpan timeLeft = targetTime - DateTime.UtcNow;

            int days = Math.Max(0, (int)timeLeft.TotalDays);
            int hours = Math.Max(0, timeLeft.Hours);

            return $"{days} Days, {hours} Hours";
        }

        public static string FormatTotalCredits(float totalCredits) =>
            totalCredits % 1 == 0 ? totalCredits.ToString("F0") : totalCredits.ToString("F2");

        public static int GetProgressPercentage(this GoalProgressData goalProgress) =>
            goalProgress.stepsDone * 100 / goalProgress.totalSteps;
    }
}
