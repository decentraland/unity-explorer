using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBusController.NotificationTypes
{
    public class ReferralNotification : NotificationBase
    {
        [JsonProperty("metadata")]
        public ReferralNotificationMetadata Metadata { get; set; }

        public ReferralNotification(NotificationType type)
        {
            Type = type;
        }

        public override string GetHeader() =>
            Metadata.title;

        public override string GetThumbnail() =>
            Metadata.image;

        public override string GetTitle() =>
            Metadata.description;
    }

    [Serializable]
    public struct ReferralNotificationMetadata
    {
        public string title;
        public string description;
        public string address;
        public int tier;
        public string url;
        public string image;
        public string invitedUserAddress;
        public int invitedUsers;
    }
}
