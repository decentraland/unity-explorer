using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBusController.NotificationTypes
{
    public class StreamingFeatureNotification : NotificationBase
    {
        [JsonProperty("metadata")]
        public StreamingFeatureNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            Metadata.Title;

        public override string GetTitle() =>
            Metadata.Description;
    }


    [Serializable]
    public struct StreamingFeatureNotificationMetadata
    {
        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("isWorld")]
        public bool IsWorld { get; set; }

        [JsonProperty("position")]
        public string Position { get; set; }

        [JsonProperty("worldName")]
        public string WorldName { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }
    }}
