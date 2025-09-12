using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBusController.NotificationTypes
{
    public class CommunityRenamedNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "Community Renamed";
        private const string NOTIFICATION_TITLE = "The <b>[{0}]</b> Community has been renamed to <b>[{1}]</b>.";

        [JsonProperty("metadata")]
        public CommunityRenamedNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            NOTIFICATION_HEADER;

        public override string GetTitle() =>
            string.Format(NOTIFICATION_TITLE, Metadata.OldCommunityName, Metadata.NewCommunityName);

        public override string GetThumbnail() =>
            Metadata.ThumbnailUrl;
    }

    [Serializable]
    public struct CommunityRenamedNotificationMetadata
    {
        [JsonProperty("communityId")]
        public string CommunityId { get; set; }

        [JsonProperty("oldName")]
        public string OldCommunityName { get; set; }

        [JsonProperty("newName")]
        public string NewCommunityName { get; set; }

        [JsonProperty("thumbnailUrl")]
        public string ThumbnailUrl { get; set; }
    }
}
