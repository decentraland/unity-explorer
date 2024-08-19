using System;
using System.Collections.Generic;

namespace DCL.BadgesAPIService
{
    [Serializable]
    public class BadgesResponse
    {
        public List<BadgeData> unlocked;
        public List<BadgeData> locked;
    }

    [Serializable]
    public class BadgeData
    {
        public string id;
        public bool isLocked;
        public string category;
        public string name;
        public string description;
        public string image;
        public string awardedAt;
        public bool isTier;
        public int totalProgress;
        public int currentProgress;
        public int currentTier;
        public BadgeTierData[] tiers;
    }

    [Serializable]
    public class BadgeTierData
    {
        public string id;
        public bool isLocked;
        public string name;
        public string description;
        public string image;
        public string awardedAt;
    }
}
