using System;
using System.Collections.Generic;

namespace DCL.BadgesAPIService
{
    [Serializable]
    public class BadgesData
    {
        public List<BadgeInfo> unlocked;
        public List<BadgeInfo> locked;
    }

    [Serializable]
    public class BadgeInfo
    {
        public string badge_id;
        public bool isLocked;
        public string category;
        public string name;
        public string description;
        public string imageUrl;
        public string awarded_at;
        public bool isTier;
        public int totalStepsToUnlock;
        public int completedSteps;
        public BadgeTier[] tiers;
    }

    [Serializable]
    public class BadgeTier
    {
        public string tier_id;
        public string name;
        public string previewModelUrl;
        public string awarded_at;
        public int stepsToUnlock;
    }
}
