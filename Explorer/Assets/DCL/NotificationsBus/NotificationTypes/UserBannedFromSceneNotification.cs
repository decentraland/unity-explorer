using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBus.NotificationTypes
{
    public class UserBannedFromSceneNotification : NotificationBase
    {
        private const string NOTIFICATION_TITLE = "Your access to this scene has been restricted.";

        [JsonProperty("metadata")]
        public UserBannedFromSceneNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            Metadata.PlaceTitle;

        public override string GetTitle() =>
            NOTIFICATION_TITLE;
    }

    [Serializable]
    public struct UserBannedFromSceneNotificationMetadata
    {
        [JsonProperty("placeTitle")]
        public string PlaceTitle { get; set; }

        [JsonProperty("userAddress")]
        public string UserAddress { get; set; }
    }
}
