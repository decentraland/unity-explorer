using DCL.FeatureFlags;
using DCL.MarketplaceCreditsAPIService;
using System;
using System.Globalization;
using System.Threading;

namespace DCL.MarketplaceCredits
{
    public static class MarketplaceCreditsUtils
    {
        public enum SeasonState
        {
            RUNNING,
            ENDED,
            ERR_SEASON_RUN_OUT_OF_FUNDS,
            ERR_WEEK_RUN_OUT_OF_FUNDS,
            ERR_PROGRAM_PAUSED,
        }

        /// <summary>
        /// Formats the time left until the end of the week in a human-readable format.
        /// </summary>
        /// <param name="timeLeftInSeconds">The time left in seconds.</param>
        /// <returns>A string representing the time left in a human-readable format.</returns>
        public static string FormatEndOfTheWeekDate(uint timeLeftInSeconds)
        {
            uint totalHours = timeLeftInSeconds / (60 * 60);
            uint days = totalHours / 24;
            uint hours = totalHours % 24;

            return $"{days} Days, {hours} Hours";
        }

        /// <summary>
        /// Formats the total credits in a human-readable format.
        /// </summary>
        /// <param name="totalCredits">The total amount of credits.</param>
        /// <returns>>A string representing the total amount credits in a human-readable format.</returns>
        public static string FormatTotalCredits(float totalCredits) =>
            totalCredits % 1 == 0 ? totalCredits.ToString("F0") : totalCredits.ToString("F2");

        /// <summary>
        /// Formats the goal reward in a human-readable format.
        /// </summary>
        /// <param name="goalCredits">The amount of credits that will be won with the goal.</param>
        /// <returns>>A string representing the goal reward in a human-readable format.</returns>
        public static string FormatGoalReward(float goalCredits) =>
            $"{(goalCredits % 1 == 0 ? $"{goalCredits:+0}" : $"{goalCredits:+0.0}")}";

        /// <summary>
        /// Formats the claimed goal reward in a human-readable format.
        /// </summary>
        /// <param name="claimedCredits">The amount of credits that have been claimed.</param>
        /// <returns>>A string representing the claimed goal reward in a human-readable format.</returns>
        public static string FormatClaimedGoalReward(float claimedCredits) =>
            $"{FormatGoalReward(claimedCredits)} Marketplace Credit{(claimedCredits >= 2 ? "s" : "")}";

        /// <summary>
        /// Formats the credits expiration time in a human-readable format.
        /// </summary>
        /// <param name="timeLeftInSeconds">The time left in seconds until the credits expire.</param>
        /// <returns>>A string representing the credits expiration time in a human-readable format.</returns>
        public static string FormatCreditsExpireIn(uint timeLeftInSeconds)
        {
            uint days = timeLeftInSeconds / (60 * 60 * 24);
            return $"Expire in {days} days";
        }

        public static string FormatSecondsToMonthDays(uint timeLeftInSeconds)
        {
            DateTime startDate = DateTime.Now;
            
            DateTime targetDate = startDate.AddSeconds(timeLeftInSeconds);
            
            return targetDate.ToString("MMMM d", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Formats the season date range in a human-readable format.
        /// </summary>
        /// <param name="startDate">The start date of the season in ISO 8601 format.</param>
        /// <param name="endDate">The end date of the season in ISO 8601 format.</param>
        /// <returns>>A string representing the season date range in a human-readable format.</returns>
        public static string FormatSeasonDateRange(string startDate, string endDate)
        {
            DateTime startDateDT = DateTime.Parse(startDate, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
            DateTime endDateDT = DateTime.Parse(endDate, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
            return $"{startDateDT.ToString("MMMM dd", CultureInfo.InvariantCulture)}-{endDateDT.ToString("MMMM dd", CultureInfo.InvariantCulture)}";
        }

        /// <summary>
        /// Calculates the progress percentage of a goal based on the completed and total steps.
        /// </summary>
        /// <param name="goalProgress">The goal progress data containing the completed and total steps.</param>
        /// <returns>>The progress percentage of the goal.</returns>
        public static uint GetProgressPercentage(this GoalProgressData goalProgress) =>
            goalProgress.completedSteps * 100 / goalProgress.totalSteps;

        /// <summary>
        /// Checks if the user has started the program.
        /// </summary>
        /// <returns>True if the user has started the program, false otherwise.</returns>
        public static bool HasUserStartedProgram(this CreditsProgramProgressResponse creditsProgramProgressResponse) =>
            creditsProgramProgressResponse.user is { hasStartedProgram: true };

        /// <summary>
        /// Checks if the user has an email registered in the program.
        /// </summary>
        /// <returns>True if the user has an email registered, false otherwise.</returns>
        public static bool IsUserEmailRegistered(this CreditsProgramProgressResponse creditsProgramProgressResponse) =>
            !string.IsNullOrEmpty(creditsProgramProgressResponse.user.email);

        /// <summary>
        /// Checks if the user has verified their email in the program.
        /// </summary>
        /// <returns>True if the user has verified their email, false otherwise.</returns>
        public static bool IsUserEmailVerified(this CreditsProgramProgressResponse creditsProgramProgressResponse) =>
            creditsProgramProgressResponse.IsUserEmailRegistered() && creditsProgramProgressResponse.user.isEmailConfirmed;

        /// <summary>
        /// Checks if the program has ended.
        /// </summary>
        /// <returns>True if the program has ended, false otherwise.</returns>
        public static bool IsProgramEnded(this CreditsProgramProgressResponse creditsProgramProgressResponse) =>
            creditsProgramProgressResponse.season.timeLeft <= 0f ||
            creditsProgramProgressResponse.season.seasonState == nameof(SeasonState.ENDED) ||
            creditsProgramProgressResponse.season.seasonState == nameof(SeasonState.ERR_SEASON_RUN_OUT_OF_FUNDS) ||
            creditsProgramProgressResponse.season.seasonState == nameof(SeasonState.ERR_WEEK_RUN_OUT_OF_FUNDS) ||
            creditsProgramProgressResponse.season.seasonState == nameof(SeasonState.ERR_PROGRAM_PAUSED);

        /// <summary>
        /// Checks if the user has completed all the weekly goals.
        /// </summary>
        /// <returns>True if all weekly goals are completed, false otherwise.</returns>
        public static bool AreWeekGoalsCompleted(this CreditsProgramProgressResponse creditsProgramProgressResponse)
        {
            foreach (var goal in creditsProgramProgressResponse.goals)
            {
                if (!goal.isClaimed)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if there are any rewards available to claim.
        /// </summary>
        /// <returns>True if there are rewards to claim, false otherwise.</returns>
        public static bool SomethingToClaim(this CreditsProgramProgressResponse creditsProgramProgressResponse)
        {
            foreach (var goal in creditsProgramProgressResponse.goals)
            {
                if (goal.progress.completedSteps == goal.progress.totalSteps && !goal.isClaimed)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if the user is allowed to use the feature based on the white list from the feature flag.
        /// </summary>
        /// <returns>True if the user is allowed to use the feature, false otherwise.</returns>
        public static bool IsUserAllowedToUseTheFeatureAsync(bool includeMarketplaceCredits, string userId, CancellationToken ct)
        {
            if (!includeMarketplaceCredits)
                return false;

            FeatureFlagsConfiguration.Instance.TryGetTextPayload(FeatureFlagsStrings.MARKETPLACE_CREDITS, FeatureFlagsStrings.MARKETPLACE_CREDITS_WALLETS_VARIANT, out string walletsForTestingMarketplaceCredits);

            return !string.IsNullOrEmpty(userId) && (walletsForTestingMarketplaceCredits == null || walletsForTestingMarketplaceCredits.Contains(userId, StringComparison.OrdinalIgnoreCase));
        }
    }
}
