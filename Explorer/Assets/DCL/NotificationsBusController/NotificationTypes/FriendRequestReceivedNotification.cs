using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBusController.NotificationTypes
{
    [Serializable]
    public class FriendRequestReceivedNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "Friend Request Received";
        private const string NOTIFICATION_TITLE_FORMAT = "{0}#{1} wants to be your friend!";

        [JsonProperty("metadata")]
        public FriendRequestReceivedNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            NOTIFICATION_HEADER;

        public override string GetTitle() =>
            string.Format(NOTIFICATION_TITLE_FORMAT, Metadata.Sender.Name, Metadata.Sender.Address[..^4]);

        public override string GetThumbnail() =>
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

        [JsonProperty("friendRequestId")]
        public string FriendRequestId { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }
    }
}
