using System;
using System.Collections.Generic;

namespace DCL.BadgesAPIService
{
    [Serializable]
    public class BadgesInfo
    {
        public List<BadgeInfo> unlocked;
        public List<BadgeInfo> locked;
    }

    [Serializable]
    public class BadgeInfo
    {
        public string id;
        public bool isLocked;
        public string category;
        public string name;
        public string description;
        public string image;
        public string awardedAt;
        public bool isTier;
        public int totalStepsToUnlock;
        public int completedSteps;
        public BadgeTierInfo[] tiers;
    }

    [Serializable]
    public class BadgeTierInfo
    {
        public string tierId;
        public string tierName;
        public string description;
        public string image;
        public string awardedAt;
        public int stepsToUnlock;
    }
}
