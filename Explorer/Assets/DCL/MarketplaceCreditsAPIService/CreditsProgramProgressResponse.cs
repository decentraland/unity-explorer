using System;
using System.Collections.Generic;

namespace DCL.MarketplaceCreditsAPIService
{
    [Serializable]
    public struct CreditsProgramProgressResponse
    {
	    public SeasonData lastSeason;
	    public SeasonData currentSeason;
	    public Week currentWeek;
	    public SeasonData nextSeason;
        public UserData user;
        public CreditsData credits;
        public List<GoalData> goals;
    }

    [Serializable]
    public struct SeasonData
    {
    	public int id;
    	public string name;
    	public string startDate;
    	public string endDate;
    	public string maxMana;
	    public int timeLeft;
    	public int amountOfWeeks;
    	public string state;
    }

    [Serializable]
    public struct Week
    {
    	public int weekNumber;
    	public uint timeLeft;
    	public string startDate;
    	public string endDate;
    	public uint secondsRemaining;
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
	
	/// <summary>
	/// Struct used to deserialize backend json data.
	/// </summary>
	public struct SeasonsData
	{
		public SeasonData lastSeason;
		public CurrentSeasonInfo currentSeason;
		public SeasonData nextSeason;
	}

	/// <summary>
	/// Struct used to deserialize backend json data.
	/// </summary>
	[Serializable]
	public struct CurrentSeasonInfo
	{
		public SeasonData season;
		public Week week;
	}
}
