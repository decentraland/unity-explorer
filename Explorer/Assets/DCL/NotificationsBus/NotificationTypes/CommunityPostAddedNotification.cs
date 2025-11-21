using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBus.NotificationTypes
{
    public class CommunityPostAddedNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "New Community Announcement";
        private const string NOTIFICATION_TITLE = "A new announcement has been posted in the <b>{0}</b> Community.";

        [JsonProperty("metadata")]
        public CommunityPostAddedNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            NOTIFICATION_HEADER;

        public override string GetTitle() =>
            string.Format(NOTIFICATION_TITLE, Metadata.CommunityName);

        public override string GetThumbnail() =>
            Metadata.ThumbnailUrl;
    }

    [Serializable]
    public struct CommunityPostAddedNotificationMetadata
    {
        [JsonProperty("communityId")]
        public string CommunityId { get; set; }

        [JsonProperty("communityName")]
        public string CommunityName { get; set; }

        [JsonProperty("thumbnailUrl")]
        public string ThumbnailUrl { get; set; }

        [JsonProperty("postId")]
        public string PostId { get; set; }

        [JsonProperty("authorAddress")]
        public string AuthorAddress { get; set; }
    }
}
