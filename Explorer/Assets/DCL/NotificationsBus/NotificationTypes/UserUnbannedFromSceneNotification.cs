using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBus.NotificationTypes
{
    public class UserUnbannedFromSceneNotification : NotificationBase
    {
        private const string NOTIFICATION_TITLE = "This scene is no longer restricting access to chat and interactions.";

        [JsonProperty("metadata")]
        public UserUnbannedFromSceneNotificationMetadata Metadata { get; set; }

        public override string GetTitle() =>
            NOTIFICATION_TITLE;
    }

    [Serializable]
    public struct UserUnbannedFromSceneNotificationMetadata
    {
        [JsonProperty("placeTitle")]
        public string PlaceTitle { get; set; }

        [JsonProperty("userAddress")]
        public string UserAddress { get; set; }
    }
}
