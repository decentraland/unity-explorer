using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBusController.NotificationTypes
{
    [Serializable]
    public class FriendRequestReceivedNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "Friend Request Received";
        private const string NOTIFICATION_TITLE = "wants to be your friend!";

        [JsonProperty("metadata")]
        public FriendRequestReceivedNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            NOTIFICATION_HEADER;

        public override string GetTitle() =>
            NOTIFICATION_TITLE;

        public override Uri GetThumbnail() =>
            Metadata.Sender.ProfileImageUrl;
    }

    [Serializable]
    public struct FriendRequestReceivedNotificationMetadata
    {
        [JsonProperty("sender")]
        public FriendRequestProfile Sender { get; set; }

        [JsonProperty("receiver")]
        public FriendRequestProfile Receiver { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("requestId")]
        public string RequestId { get; set; }

    }
}
