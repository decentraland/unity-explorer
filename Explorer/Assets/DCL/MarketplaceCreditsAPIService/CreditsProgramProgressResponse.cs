using System;
using System.Collections.Generic;

namespace DCL.MarketplaceCreditsAPIService
{
    [Serializable]
    public class CreditsProgramProgressResponse
    {
        public SeasonData season;
        public CurrentWeekData currentWeek;
        public UserData user;
        public CreditsData credits;
        public List<GoalData> goals;
    }

    [Serializable]
    public class SeasonData
    {
        public string startDate;
        public string endDate;
        public uint timeLeft;
        public bool isOutOfFunds;
    }

    [Serializable]
    public class CurrentWeekData
    {
        public uint timeLeft;
    }

    [Serializable]
    public class UserData
    {
        public string email;
        public bool isEmailConfirmed;
    }

    [Serializable]
    public class CreditsData
    {
        public float available;
        public uint expireIn;
    }

    [Serializable]
    public class GoalData
    {
        public string title;
        public string thumbnail;
        public GoalProgressData progress;
        public float reward;
        public bool isClaimed;
    }

    [Serializable]
    public class GoalProgressData
    {
        public int totalSteps;
        public int completedSteps;
    }
}
