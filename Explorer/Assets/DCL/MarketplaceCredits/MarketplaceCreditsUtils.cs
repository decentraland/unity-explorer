using DCL.MarketplaceCreditsAPIService;
using System;

namespace DCL.MarketplaceCredits
{
    public class MarketplaceCreditsUtils
    {
        public static GoalsOfTheWeekInfo GoalsOfTheWeekResponseToGoalsOfTheWeekInfo(GoalsOfTheWeekResponse goalsOfTheWeekResponse) =>
            new()
            {
                timeLeft = FormatEndOfTheWeekDateTimestamp(goalsOfTheWeekResponse.data.endOfTheWeekDate),
                totalCredits = FormatTotalCredits(goalsOfTheWeekResponse.data.totalCredits),
                creditsAvailableToClaim = goalsOfTheWeekResponse.data.creditsAvailableToClaim,
            };

        private static string FormatEndOfTheWeekDateTimestamp(string timestamp)
        {
            if (!DateTime.TryParse(timestamp, out DateTime targetTime))
                return "Invalid timestamp";

            TimeSpan timeLeft = targetTime - DateTime.UtcNow;

            int days = Math.Max(0, (int)timeLeft.TotalDays);
            int hours = Math.Max(0, timeLeft.Hours);

            return $"{days} Days, {hours} Hours";
        }

        private static string FormatTotalCredits(float totalCredits) =>
            totalCredits.ToString("F2");
    }
}
