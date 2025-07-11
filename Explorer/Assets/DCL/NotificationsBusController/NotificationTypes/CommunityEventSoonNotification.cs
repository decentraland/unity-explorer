using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBusController.NotificationTypes
{
    [Serializable]
    public class CommunityEventSoonNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "{0} Event";
        private const string NOTIFICATION_TITLE = "An event of the community [{0}] is about to start";

        [JsonProperty("metadata")]
        public CommunityEventSoonNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            string.Format(NOTIFICATION_HEADER, Metadata.CommunityName);

        public override string GetTitle() =>
            string.Format(NOTIFICATION_TITLE, Metadata.CommunityName);

        public override string GetThumbnail() =>
            Metadata.ThumbnailUrl;
    }

    [Serializable]
    public struct CommunityEventSoonNotificationMetadata
    {
        [JsonProperty("community_name")]
        public string CommunityName { get; set; }

        [JsonProperty("thumbnail_url")]
        public string ThumbnailUrl { get; set; }

        [JsonProperty("world")]
        public bool World { get; set; }

        [JsonProperty("x")]
        public int X { get; set; }

        [JsonProperty("y")]
        public int Y { get; set; }
    }
}
