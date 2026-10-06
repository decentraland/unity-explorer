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

        // Newtonsoft-deserialized wire DTO (CreateFromJson with WRJsonParser.Newtonsoft); Unity serialization never sees this field.
#pragma warning disable UAC1001
        public int? nextStepsTarget;
#pragma warning restore UAC1001

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

    /// <summary>
    ///     <c>GET /users/{address}/scene-badges/mine</c>: the player's own scene badge awards, every state,
    ///     in the platform badge shape plus the award record.
    /// </summary>
    [Serializable]
    public class SceneBadgesResponse
    {
        public SceneBadgesResponseData data = null!;
    }

    [Serializable]
    public class SceneBadgesResponseData
    {
        public List<SceneBadgeData> badges = null!;
    }

    [Serializable]
    public class SceneBadgeData
    {
        public string id = null!;
        public string name = null!;
        public string? completedAt;
        public BadgeAssetsData? assets;
        public SceneBadgeAwardData? award;
    }

    [Serializable]
    public class SceneBadgeAwardData
    {
        public string id = null!;
        public string state = null!;
        public long? celebratedAt;
    }
}
