using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBusController.NotificationTypes
{
    [Serializable]
    public class CommunityEventCreatedNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "{0} Event";
        private const string NOTIFICATION_TITLE = "A new event has been added to [{0}]";

        [JsonProperty("metadata")]
        public CommunityEventCreatedNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            string.Format(NOTIFICATION_HEADER, Metadata.CommunityName);

        public override string GetTitle() =>
            string.Format(NOTIFICATION_TITLE, Metadata.CommunityName);

        public override string GetThumbnail() =>
            Metadata.ThumbnailUrl;
    }

    [Serializable]
    public struct CommunityEventCreatedNotificationMetadata
    {
        [JsonProperty("community_name")]
        public string CommunityName { get; set; }

        [JsonProperty("thumbnail_url")]
        public string ThumbnailUrl { get; set; }

        [JsonProperty("community_id")]
        public string CommunityId { get; set; }
    }
}
