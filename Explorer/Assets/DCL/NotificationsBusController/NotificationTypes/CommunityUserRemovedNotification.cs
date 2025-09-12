using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBusController.NotificationTypes
{
    public class CommunityUserRemovedNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "Removed from Community";
        private const string NOTIFICATION_TITLE = "You've been removed from the <b>[{0}]</b> Community.";

        [JsonProperty("metadata")]
        public CommunityUserRemovedNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            NOTIFICATION_HEADER;

        public override string GetTitle() =>
            string.Format(NOTIFICATION_TITLE, Metadata.CommunityName);

        public override string GetThumbnail() =>
            Metadata.ThumbnailUrl;
    }

    [Serializable]
    public struct CommunityUserRemovedNotificationMetadata
    {
        [JsonProperty("communityId")]
        public string CommunityId { get; set; }

        [JsonProperty("memberAddress")]
        public string UserAddress { get; set; }

        [JsonProperty("communityName")]
        public string CommunityName { get; set; }

        [JsonProperty("thumbnailUrl")]
        public string ThumbnailUrl { get; set; }
    }
}
