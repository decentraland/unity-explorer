using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBusController.NotificationTypes
{
    [Serializable]
    public class BadgeGrantedNotification : NotificationBase
    {
        [JsonProperty("metadata")]
        public BadgeGrantedNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            Metadata.Title;

        public override string GetTitle() =>
            Metadata.Description;

        public override string GetThumbnail() =>
            Metadata.Image;
    }

    [Serializable]
    public struct BadgeGrantedNotificationMetadata
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("image")]
        public string Image { get; set; }
    }
}
