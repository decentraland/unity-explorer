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
    public class TiersInfo
    {
        public List<BadgeTierInfo> tiers;
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
        public string completedAt;
        public bool isTier;
        public int nextTierTotalProgress;
        public int nextTierCurrentProgress;
        public string lastTierCompletedAt;
        public int? lastCompletedTierIndex;
        public int nextTierToCompleteIndex;
        public BadgeTierInfo[] tiers;
    }

    [Serializable]
    public class BadgeTierInfo
    {
        public string id;
        public bool isLocked;
        public string name;
        public string description;
        public string image;
        public string awardedAt;
    }
}
