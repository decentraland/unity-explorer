using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBusController.NotificationTypes
{
    [Serializable]
    public class FriendRequestAcceptedNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "Friend Request Accepted!";
        private const string NOTIFICATION_TITLE = "accepted your friend request.";

        [JsonProperty("metadata")]
        public FriendRequestAcceptedNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            NOTIFICATION_HEADER;

        public override string GetTitle() =>
            NOTIFICATION_TITLE;

        public override Uri GetThumbnail() =>
            Metadata.Sender.ProfileImageUrl;
    }

    [Serializable]
    public struct FriendRequestAcceptedNotificationMetadata
    {
        [JsonProperty("sender")]
        public FriendRequestProfile Sender { get; set; }

        [JsonProperty("receiver")]
        public FriendRequestProfile Receiver { get; set; }

        [JsonProperty("requestId")]
        public string RequestId { get; set; }
    }

    [Serializable]
    public struct FriendRequestProfile
    {
        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("profileImageUrl")]
        public Uri ProfileImageUrl { get; set; }

        [JsonProperty("hasClaimedName")]
        public bool HasClaimedName { get; set; }
    }
}
