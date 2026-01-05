using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBus.NotificationTypes
{
    public class CommunityVoiceChatStartedNotification : NotificationBase
    {
        [JsonProperty("metadata")]
        public CommunityVoiceChatStartedNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            "Community Voice Stream Started";

        public override string GetTitle() =>
            $"The {Metadata.CommunityName} is streaming! Click here to join the stream.";

        public override string GetThumbnail() =>
            Metadata.ThumbnailUrl;
    }

    [Serializable]
    public struct CommunityVoiceChatStartedNotificationMetadata
    {
        [JsonProperty("communityId")]
        public string CommunityId { get; set; }

        [JsonProperty("communityName")]
        public string CommunityName { get; set; }

        [JsonProperty("thumbnailUrl")]
        public string ThumbnailUrl { get; set; }
    }
}
