using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBusController.NotificationTypes
{
    public class CommunityUserRequestToJoinNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "Membership Request Received";
        private const string NOTIFICATION_TITLE = "<b>{0}</b> wants to join the <b>{1}</b> Community.";

        [JsonProperty("metadata")]
        public CommunityUserRequestToJoinNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            NOTIFICATION_HEADER;

        public override string GetTitle() =>
            string.Format(NOTIFICATION_TITLE, Metadata.UserName, Metadata.CommunityName);

        public override string GetThumbnail() =>
            Metadata.ThumbnailUrl;
    }

    [Serializable]
    public struct CommunityUserRequestToJoinNotificationMetadata
    {
        [JsonProperty("communityName")]
        public string CommunityName { get; set; }

        [JsonProperty("communityId")]
        public string CommunityId { get; set; }

        [JsonProperty("memberName")]
        public string UserName { get; set; }

        [JsonProperty("thumbnailUrl")]
        public string ThumbnailUrl { get; set; }
    }
}
