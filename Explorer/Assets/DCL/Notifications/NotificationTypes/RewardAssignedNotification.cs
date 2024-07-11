using Newtonsoft.Json;
using System;

namespace DCL.Notification
{
    public class RewardAssignedNotification : NotificationBase
    {
        [JsonProperty("metadata")]
        public RewardAssignedNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            Metadata.Title;

        public override string GetTitle() =>
            Metadata.Description;

        public override string GetThumbnail() =>
            Metadata.Image;
    }

    [Serializable]
    public struct RewardAssignedNotificationMetadata
    {
        [JsonProperty("tokenName")]
        public string Name { get; set; }

        [JsonProperty("tokenImage")]
        public string Image { get; set; }

        [JsonProperty("tokenRarity")]
        public string Rarity { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

    }
}
