using DCL.MarketplaceCreditsAPIService;
using System;
using System.Globalization;
using UnityEngine;

namespace DCL.MarketplaceCredits
{
    public static class MarketplaceCreditsUtils
    {
        public const string WEEKLY_REWARDS_INFO_LINK = "https://docs.decentraland.org";
        public const string LEARN_MORE_LINK = "https://docs.decentraland.org";
        public const string TIME_LEFT_INFO_LINK = "https://docs.decentraland.org";
        public const string GO_SHOPPING_LINK = "https://decentraland.org/marketplace";
        public const string SUBSCRIBE_LINK = "https://decentraland.beehiiv.com/?utm_org=dcl&utm_source=client&utm_medium=organic&utm_campaign=marketplacecredits&utm_term=trialend";
        public const string X_LINK = "https://x.com/decentraland";
        public const int CREDITS_UNLOCKED_DURATION = 5;
        public const int ERROR_NOTIFICATION_DURATION = 3;
        public const int CHECKING_SIDEBAR_BUTTON_STATE_TIME_INTERVAL = 10;

        private const string FEATURE_OPEN_BY_FIRST_TIME_LOCAL_STORAGE_KEY = "MarketplaceCreditsFeatureOpenByFirstTime";

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

        public static string FormatClaimedGoalReward(float claimedCredits) =>
            $"{FormatGoalReward(claimedCredits)} Marketplace Credit{(claimedCredits >= 2 ? "s" : "")}";

        public static string FormatCreditsExpireIn(uint timeLeftInMilliseconds)
        {
            uint days = timeLeftInMilliseconds / (1000 * 60 * 60 * 24);
            return $"Expire in {days} days";
        }

        public static string FormatSeasonDateRange(string startDate, string endDate)
        {
            DateTime startDateDT = DateTime.ParseExact(startDate, "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
            DateTime endDateDT = DateTime.ParseExact(endDate, "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
            return $"{startDateDT.ToString("MMMM dd", CultureInfo.InvariantCulture)}-{endDateDT.ToString("MMMM dd", CultureInfo.InvariantCulture)}";
        }

        public static int GetProgressPercentage(this GoalProgressData goalProgress) =>
            goalProgress.completedSteps * 100 / goalProgress.totalSteps;

        public static bool IsProgramEnded(this CreditsProgramProgressResponse creditsProgramProgressResponse) =>
            creditsProgramProgressResponse.season.timeLeft <= 0f || creditsProgramProgressResponse.season.isOutOfFunds;

        public static bool HasFeatureBeenOpenedByFirstTime() =>
            PlayerPrefs.GetInt(FEATURE_OPEN_BY_FIRST_TIME_LOCAL_STORAGE_KEY, 0) == 1;

        public static void SetFeatureAsOpenedByFirstTime()
        {
            PlayerPrefs.SetInt(FEATURE_OPEN_BY_FIRST_TIME_LOCAL_STORAGE_KEY, 1);
            PlayerPrefs.Save();
        }

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
