using Newtonsoft.Json;
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
        public BadgeAssetsData assets;
        public BadgeProgressData progress;
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
        public List<AchievedTierData> achievedTiers;
    }

    [Serializable]
    public class AchievedTierData
    {
        public string tierId;
        public string completedAt;
    }

    [Serializable]
    public class BadgeAssetsData
    {
        [JsonProperty("2d")]
        public BadgeTexturesData textures2d;

        [JsonProperty("3d")]
        public BadgeTexturesData textures3d;
    }

    [Serializable]
    public class BadgeTexturesData
    {
        public string normal;
        public string hrm;
        public string baseColor;
    }
}
