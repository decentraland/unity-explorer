using System;

namespace DCL.BadgesAPIService
{
    [Serializable]
    public class BadgeInfo
    {
        public string id;
        public string name;
        public string description;
        public string imageUrl;
        public string date;
        public bool isTier;
        public bool isTopTier;
        public bool isLocked;
        public int progressPercentage;
        public string category;
    }
}
