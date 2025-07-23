using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBusController.NotificationTypes
{
    public class CommunityUserBannedNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "Banned From Community";
        private const string NOTIFICATION_TITLE = "You've been banned from the <b>[{0}]</b> Community.";

        [JsonProperty("metadata")]
        public CommunityUserBannedNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            NOTIFICATION_HEADER;

        public override string GetTitle() =>
            string.Format(NOTIFICATION_TITLE, Metadata.CommunityName);

        public override string GetThumbnail() =>
            Metadata.ThumbnailUrl;
    }

    [Serializable]
    public struct CommunityUserBannedNotificationMetadata
    {
        [JsonProperty("id")]
        public string CommunityId { get; set; }

        [JsonProperty("memberAddress")]
        public string UserAddress { get; set; }

        [JsonProperty("name")]
        public string CommunityName { get; set; }

        [JsonProperty("thumbnailUrl")]
        public string ThumbnailUrl { get; set; }
    }
}
