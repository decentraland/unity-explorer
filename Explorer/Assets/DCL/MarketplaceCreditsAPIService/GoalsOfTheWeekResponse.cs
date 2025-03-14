using System;
using System.Collections.Generic;

namespace DCL.MarketplaceCreditsAPIService
{
    [Serializable]
    public class GoalsOfTheWeekResponse
    {
        public GoalsOfTheWeekData data;
    }

    [Serializable]
    public class GoalsOfTheWeekData
    {
        public string endOfTheWeekDate;
        public float totalCredits;
        public int daysToExpire;
        public List<GoalData> goals;
        public bool creditsAvailableToClaim;
    }

    [Serializable]
    public class GoalData
    {
        public string thumbnail;
        public string title;
        public GoalProgressData progress;
        public int credits;
        public bool isClaimed;
    }

    [Serializable]
    public class GoalProgressData
    {
        public int totalSteps;
        public int stepsDone;
    }
}
