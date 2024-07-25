using Newtonsoft.Json;
using System;

namespace DCL.Notification
{
    public class IncomingRewardNotification : NotificationBase
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
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("image")]
        public string Image { get; set; }

        [JsonProperty("rarity")]
        public string Rarity { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

    }
}
