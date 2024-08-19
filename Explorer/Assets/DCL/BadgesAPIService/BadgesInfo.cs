using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

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
        public int totalProgress;
        public int currentProgress;
        public int currentTier;
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
