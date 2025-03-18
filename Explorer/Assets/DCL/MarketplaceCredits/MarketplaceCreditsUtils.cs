using DCL.MarketplaceCreditsAPIService;
using System;

namespace DCL.MarketplaceCredits
{
    public static class MarketplaceCreditsUtils
    {
        public const string WEEKLY_REWARDS_INFO_LINK = "https://docs.decentraland.org/";
        public const string LEARN_MORE_LINK = "https://docs.decentraland.org/";
        public const string TIME_LEFT_INFO_LINK = "https://docs.decentraland.org/";
        public const string GO_SHOPPING_LINK = "https://decentraland.org/marketplace/";
        public const int CREDITS_UNLOCKED_DURATION = 5;
        public const int ERROR_NOTIFICATION_DURATION = 3;

        public static string FormatEndOfTheWeekDate(uint timeLeftInMilliseconds)
        {
            uint totalHours = timeLeftInMilliseconds / (1000 * 60 * 60);
            uint days = totalHours / 24;
            uint hours = totalHours % 24;

            return $"{days} Days, {hours} Hours";
        }

        public static string FormatTotalCredits(float totalCredits) =>
            totalCredits % 1 == 0 ? totalCredits.ToString("F0") : totalCredits.ToString("F2");

        public static string FormatGoalReward(float goalCredits) =>
            $"{Math.Floor(goalCredits):+0}";

        public static string FormatCreditsExpireIn(uint timeLeftInMilliseconds)
        {
            uint days = timeLeftInMilliseconds / (1000 * 60 * 60 * 24);
            return $"Expire in {days} days";
        }

        public static int GetProgressPercentage(this GoalProgressData goalProgress) =>
            goalProgress.completedSteps * 100 / goalProgress.totalSteps;

        public static bool AreWeekGoalsCompleted(this CreditsProgramProgressResponse creditsProgramProgressResponse)
        {
            foreach (var goal in creditsProgramProgressResponse.goals)
            {
                if (!goal.isClaimed)
                    return false;
            }

            return true;
        }

        public static bool SomethingToClaim(this CreditsProgramProgressResponse creditsProgramProgressResponse)
        {
            foreach (var goal in creditsProgramProgressResponse.goals)
            {
                if (goal.progress.completedSteps == goal.progress.totalSteps && !goal.isClaimed)
                    return true;
            }

            return false;
        }
    }
}
