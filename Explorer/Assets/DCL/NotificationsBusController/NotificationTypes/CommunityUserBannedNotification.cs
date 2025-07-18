using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBusController.NotificationTypes
{
    public class CommunityUserBannedNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "{0}";
        private const string NOTIFICATION_TITLE = "You have been banned from the community";

        [JsonProperty("metadata")]
        public CommunityUserBannedNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            string.Format(NOTIFICATION_HEADER, Metadata.CommunityName);

        public override string GetTitle() =>
            NOTIFICATION_TITLE;

        public override string GetThumbnail() =>
            Metadata.ThumbnailUrl;
    }

    [Serializable]
    public struct CommunityUserBannedNotificationMetadata
    {
        [JsonProperty("community_name")]
        public string CommunityName { get; set; }

        [JsonProperty("thumbnail_url")]
        public string ThumbnailUrl { get; set; }
    }
}
