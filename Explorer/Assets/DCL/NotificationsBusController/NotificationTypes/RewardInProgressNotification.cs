using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBusController.NotificationTypes
{
    public class RewardInProgressNotification : NotificationBase
    {
        [JsonProperty("metadata")]
        public IncomingRewardNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            Metadata.Title;

        public override string GetTitle() =>
            Metadata.Description;

        public override string GetThumbnail() =>
            Metadata.Image;
    }

    [Serializable]
    public struct IncomingRewardNotificationMetadata
    {
        [JsonProperty("tokenName")]
        public string Name { get; set; }

        [JsonProperty("tokenImage")]
        public string Image { get; set; }

        [JsonProperty("tokenRarity")]
        public string Rarity { get; set; }

        [JsonProperty("tokenCategory")]
        public string Category { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

    }
}
