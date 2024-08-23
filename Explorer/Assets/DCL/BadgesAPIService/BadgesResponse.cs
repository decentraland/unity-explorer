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
        public string image;
    }

    [Serializable]
    public class BadgeProgressData
    {
        public int stepsDone;
        public int? nextStepsTarget;
        public int totalStepsTarget;
        public string lastCompletedTierAt;
        public string lastCompletedTierName;
        public string lastCompletedTierImage;
    }
}
