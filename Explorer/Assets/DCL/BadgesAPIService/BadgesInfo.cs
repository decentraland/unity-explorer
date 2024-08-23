using System;
using System.Collections.Generic;

namespace DCL.BadgesAPIService
{
    [Serializable]
    public class BadgesInfo
    {
        public List<BadgeInfo> achieved;
        public List<BadgeInfo> notAchieved;
    }

    [Serializable]
    public class BadgeInfo
    {
        public string id;
        public string name;
        public string description;
        public string category;
        public bool isTier;
        public string completedAt;
        public BadgeProgressData progress;
        public string image;

        // Extra fields
        public bool isLocked;
        public int? lastCompletedTierIndex;
        public int nextTierToCompleteIndex;
        public TierData[] tiers;
    }
}
