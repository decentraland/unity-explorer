using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBusController.NotificationTypes
{
    [Serializable]
    public class CommunityEventCreatedNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "Community Event Added";
        private const string NOTIFICATION_TITLE = "The <b>[{0}]</b> Community has added a new event.";

        [JsonProperty("metadata")]
        public CommunityEventCreatedNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            NOTIFICATION_HEADER;

        public override string GetTitle() =>
            string.Format(NOTIFICATION_TITLE, Metadata.CommunityName);

        public override string GetThumbnail() =>
            Metadata.EventImageUrl;
    }

    [Serializable]
    public struct CommunityEventCreatedNotificationMetadata
    {
        [JsonProperty("communityId")]
        public string CommunityId { get; set; }

        [JsonProperty("communityName")]
        public string CommunityName { get; set; }

        [JsonProperty("image")]
        public string EventImageUrl { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("communityThumbnail")]
        public string CommunityThumbnailUrl { get; set; }
    }
}
