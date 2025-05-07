using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBusController.NotificationTypes
{
    [Serializable]
    public class MarketplaceCreditsNotification : NotificationBase
    {
        [JsonProperty("metadata")]
        public MarketplaceCreditsNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            Metadata.Title;

        public override string GetTitle() =>
            Metadata.Description;

        public override string GetThumbnail() =>
            Metadata.Image;
    }

    [Serializable]
    public struct MarketplaceCreditsNotificationMetadata
    {
        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("image")]
        public string Image { get; set; }
    }
}
