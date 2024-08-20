using System;
using System.Collections.Generic;

namespace DCL.BadgesAPIService
{
    [Serializable]
    public class BadgesResponse
    {
        public List<BadgeData> achieved;
        public List<BadgeData> notAchieved;
    }

    [Serializable]
    public class BadgeData
    {
        public string id;
        public string name;
        public string description;
        public string category;
        public bool isTier;
        public string completedAt;
        public BadgeProgressData progress;
        public BadgeTierData[] tiers;
        public string image;
    }

    [Serializable]
    public class BadgeProgressData
    {
        public int stepsDone;
        public int? stepsTarget;
    }

    [Serializable]
    public class BadgeTierData
    {
        public string tierId;
        public string tierName;
        public string description;
        public BadgeTierCriteria criteria;
        public string completedAt;
    }

    [Serializable]
    public class BadgeTierCriteria
    {
        public int steps;
    }
}
