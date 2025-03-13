using System;

namespace DCL.MarketplaceCreditsAPIService
{
    [Serializable]
    public class ProgramRegistrationResponse
    {
        public bool isRegistered;
        public float totalCredits;
        public int daysToExpire;
        public bool areWeekGoalsCompleted;
        public bool isProgramEnded;
    }
}
