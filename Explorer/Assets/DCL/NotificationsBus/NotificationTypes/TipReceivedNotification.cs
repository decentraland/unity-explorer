using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBus.NotificationTypes
{
    [Serializable]
    public class TipReceivedNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "New tip received";
        private const string NOTIFICATION_TITLE = "tipped your scene <b>{0}</b> with <b>{1:#.##} MANA</b>";

        [JsonProperty("metadata")]
        public TipReceivedNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            NOTIFICATION_HEADER;

        public override string GetTitle() =>
            string.Format(NOTIFICATION_TITLE, Metadata.SceneName, Metadata.TipAmount);

        public override string GetThumbnail() =>
            Metadata.Sender.ProfileImageUrl;
    }

    [Serializable]
    public struct TipReceivedNotificationMetadata
    {
        [JsonProperty("sender")]
        public FriendRequestProfile Sender { get; set; }

        [JsonProperty("tipAmount")]
        public decimal TipAmount { get; set; }

        [JsonProperty("sceneName")]
        public string SceneName { get; set; }

        [JsonProperty("requestId")]
        public string RequestId { get; set; }

    }
}
