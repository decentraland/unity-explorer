using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBus.NotificationTypes
{
    public class CommunityOwnershipTransferredNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "Community Ownership Transfered";
        private const string NOTIFICATION_TITLE = "The <b>{0}</b> Community has been transferred to you by its owner.";

        [JsonProperty("metadata")]
        public CommunityOwnershipTransferredNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            NOTIFICATION_HEADER;

        public override string GetTitle() =>
            string.Format(NOTIFICATION_TITLE, Metadata.CommunityName);

        public override string GetThumbnail() =>
            Metadata.ThumbnailUrl;
    }

    [Serializable]
    public struct CommunityOwnershipTransferredNotificationMetadata
    {
        [JsonProperty("communityId")]
        public string CommunityId { get; set; }

        [JsonProperty("communityName")]
        public string CommunityName { get; set; }

        [JsonProperty("oldOwnerAddress")]
        public string OldOwnerAddress { get; set; }

        [JsonProperty("newOwnerAddress")]
        public string NewOwnerAddress { get; set; }

        [JsonProperty("thumbnailUrl")]
        public string ThumbnailUrl { get; set; }
    }
}
