using System;

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
        public bool creditsAvailableToClaim;
    }
}
