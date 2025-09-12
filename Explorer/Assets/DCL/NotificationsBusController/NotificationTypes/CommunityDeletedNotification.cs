using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBusController.NotificationTypes
{
    public class CommunityDeletedNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "Community Deleted";
        private const string NOTIFICATION_TITLE = "The <b>[{0}]</b> Community has been deleted.";

        [JsonProperty("metadata")]
        public CommunityDeletedNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            NOTIFICATION_HEADER;

        public override string GetTitle() =>
            string.Format(NOTIFICATION_TITLE, Metadata.CommunityName);

        public override string GetThumbnail() =>
            Metadata.ThumbnailUrl;
    }

    [Serializable]
    public struct CommunityDeletedNotificationMetadata
    {
        [JsonProperty("communityName")]
        public string CommunityName { get; set; }

        [JsonProperty("communityId")]
        public string CommunityId { get; set; }

        [JsonProperty("thumbnailUrl")]
        public string ThumbnailUrl { get; set; }
    }
}
