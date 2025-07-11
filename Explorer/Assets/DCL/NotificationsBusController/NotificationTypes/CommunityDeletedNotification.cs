using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBusController.NotificationTypes
{
    public class CommunityDeletedNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "{0}";
        private const string NOTIFICATION_TITLE = "[{0}] has been deleted";

        [JsonProperty("metadata")]
        public CommunityDeletedNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            string.Format(NOTIFICATION_HEADER, Metadata.CommunityName);

        public override string GetTitle() =>
            string.Format(NOTIFICATION_TITLE, Metadata.CommunityName);

        public override string GetThumbnail() =>
            Metadata.ThumbnailUrl;
    }

    [Serializable]
    public struct CommunityDeletedNotificationMetadata
    {
        [JsonProperty("community_name")]
        public string CommunityName { get; set; }

        [JsonProperty("thumbnail_url")]
        public string ThumbnailUrl { get; set; }
    }
}
