using System;
using System.Collections.Generic;

namespace DCL.BadgesAPIService
{
    [Serializable]
    public class BadgesResponse
    {
        public ProfileBadgesData data;
    }

    [Serializable]
    public class TiersResponse
    {
        public List<TierData> data;
    }

    [Serializable]
    public class ProfileBadgesData
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
        public TierData[] tiers;
        public string image;
    }

    [Serializable]
    public class BadgeProgressData
    {
        public int stepsDone;
        public int? nextStepsTarget;
        public string lastCompletedTierAt;

        public int totalStepsTarget;
        public string lastCompletedTierName;
        public string lastCompletedTierImage;

    }

    [Serializable]
    public class TierData
    {
        public string tierId;
        public string tierName;
        public string description;
        public BadgeTierCriteria criteria;
        public string completedAt;
        public string image;
    }

    [Serializable]
    public class BadgeTierCriteria
    {
        public int steps;
    }
}
