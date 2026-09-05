using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBus.NotificationTypes
{
    public class CommunityUserInvitedNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "Community Invite Received";
        private const string NOTIFICATION_TITLE = "You've been invited to join the <b>{0}</b> Community.";

        [JsonProperty("metadata")]
        public CommunityUserInvitedNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            NOTIFICATION_HEADER;

        public override string GetTitle() =>
            string.Format(NOTIFICATION_TITLE, Metadata.CommunityName);

        public override string GetThumbnail() =>
            Metadata.ThumbnailUrl;
    }

    [Serializable]
    public struct CommunityUserInvitedNotificationMetadata
    {
        [JsonProperty("communityName")]
        public string CommunityName { get; set; }

        [JsonProperty("communityId")]
        public string CommunityId { get; set; }

        [JsonProperty("memberAddress")]
        public string UserAddress { get; set; }

        [JsonProperty("thumbnailUrl")]
        public string ThumbnailUrl { get; set; }
    }
}
