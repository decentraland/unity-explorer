using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBus.NotificationTypes
{
    public class CommunityDeletedContenViolationNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "Your Community Has Been Deleted";
        private const string NOTIFICATION_TITLE = "The <b>{0}</b> Community was deleted for violating Decentraland's Guidelines.";

        [JsonProperty("metadata")]
        public OwnerCommunityDeletedNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            NOTIFICATION_HEADER;

        public override string GetTitle() =>
            string.Format(NOTIFICATION_TITLE, Metadata.CommunityName);

        public override string GetThumbnail() =>
            Metadata.ThumbnailUrl;
    }

    [Serializable]
    public struct OwnerCommunityDeletedNotificationMetadata
    {
        [JsonProperty("communityName")]
        public string CommunityName { get; set; }

        [JsonProperty("communityId")]
        public string CommunityId { get; set; }

        [JsonProperty("thumbnailUrl")]
        public string ThumbnailUrl { get; set; }
    }
}
