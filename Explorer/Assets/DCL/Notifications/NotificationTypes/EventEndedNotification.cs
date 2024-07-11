using Newtonsoft.Json;
using System;

namespace DCL.Notification
{
    [Serializable]
    public class EventEndedNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "An event has ended!";

        [JsonProperty("metadata")]
        public EventEndedNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            NOTIFICATION_HEADER;

        public override string GetTitle() =>
            Metadata.Title;
    }

    [Serializable]
    public struct EventEndedNotificationMetadata
    {
        [JsonProperty("link")]
        public string Link { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("image")]
        public string Image { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

    }
}
