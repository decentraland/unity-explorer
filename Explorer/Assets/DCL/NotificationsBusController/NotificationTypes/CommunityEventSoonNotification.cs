using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBusController.NotificationTypes
{
    [Serializable]
    public class CommunityEventSoonNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "Community Event Starting";
        private const string NOTIFICATION_TITLE = "A <b>[{0}]</b> community event is about to start.";

        [JsonProperty("metadata")]
        public CommunityEventSoonNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            NOTIFICATION_HEADER;

        public override string GetTitle() =>
            string.Format(NOTIFICATION_TITLE, Metadata.CommunityName);

        public override string GetThumbnail() =>
            Metadata.ThumbnailUrl;
    }

    [Serializable]
    public struct CommunityEventSoonNotificationMetadata
    {
        [JsonProperty("id")]
        public string CommunityId { get; set; }

        [JsonProperty("name")]
        public string CommunityName { get; set; }

        [JsonProperty("thumbnailUrl")]
        public string ThumbnailUrl { get; set; }

        [JsonProperty("world")]
        public bool World { get; set; }

        [JsonProperty("worldName")]
        public string WorldName { get; set; }

        [JsonProperty("x")]
        public int X { get; set; }

        [JsonProperty("y")]
        public int Y { get; set; }
    }
}
