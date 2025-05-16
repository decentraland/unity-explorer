using System;
using System.Collections.Generic;

namespace DCL.MarketplaceCreditsAPIService
{
    [Serializable]
    public struct CreditsProgramProgressResponse
    {
        public SeasonData season;
        public CurrentWeekData currentWeek;
        public UserData user;
        public CreditsData credits;
        public List<GoalData> goals;
    }

    [Serializable]
    public struct SeasonData
    {
        public string startDate;
        public string endDate;
        public uint timeLeft;
        public string seasonState;
    }

    [Serializable]
    public struct CurrentWeekData
    {
        public uint timeLeft;
    }

    [Serializable]
    public struct UserData
    {
        public string email;
        public bool isEmailConfirmed;
        public bool hasStartedProgram;
    }

    [Serializable]
    public struct CreditsData
    {
        public float available;
        public uint expiresIn;
        public bool isBlockedForClaiming;
    }

    [Serializable]
    public struct GoalData
    {
        public string title;
        public string thumbnail;
        public GoalProgressData progress;
        public float reward;
        public bool isClaimed;
    }

    [Serializable]
    public struct GoalProgressData
    {
        public uint totalSteps;
        public uint completedSteps;
    }
}
