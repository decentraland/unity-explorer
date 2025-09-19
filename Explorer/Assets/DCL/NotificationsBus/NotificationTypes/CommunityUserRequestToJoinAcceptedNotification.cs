using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBus.NotificationTypes
{
    public class CommunityUserRequestToJoinAcceptedNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "Membership Request Accepted";
        private const string NOTIFICATION_TITLE = "Congrats! You're now a member of the <b>[{0}]</b> Community.";

        [JsonProperty("metadata")]
        public CommunityUserRequestToJoinAcceptedNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            NOTIFICATION_HEADER;

        public override string GetTitle() =>
            string.Format(NOTIFICATION_TITLE, Metadata.CommunityName);

        public override string GetThumbnail() =>
            Metadata.ThumbnailUrl;
    }

    [Serializable]
    public struct CommunityUserRequestToJoinAcceptedNotificationMetadata
    {
        [JsonProperty("communityName")]
        public string CommunityName { get; set; }

        [JsonProperty("communityId")]
        public string CommunityId { get; set; }

        [JsonProperty("thumbnailUrl")]
        public string ThumbnailUrl { get; set; }
    }
}
