using Newtonsoft.Json;
using System;
using DCL.Diagnostics;

namespace DCL.NotificationsBus.NotificationTypes
{
    [Serializable]
    public class GiftReceivedNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "Gift received";
        private const string NOTIFICATION_TITLE = "sent you a gift!";

        [JsonProperty("metadata")]
        public GiftReceivedNotificationMetadata Metadata { get; set; }

        public GiftReceivedNotification()
        {
            NotificationsBusController.Instance.SubscribeToNotificationTypeClick(NotificationType.TRANSFER_RECEIVED, GiftReceivedClick);
        }

        private void GiftReceivedClick(object[] parameters)
        {
            ReportHub.Log(ReportCategory.GIFTING, "Gift received notification clicked");
        }

        public override string GetHeader() => NOTIFICATION_HEADER;

        public override string GetTitle() => NOTIFICATION_TITLE;

        public override string GetThumbnail() => "";
    }

    [Serializable]
    public struct GiftReceivedNotificationMetadata
    {
        [JsonProperty("senderAddress")]
        public string SenderAddress { get; set; }

        [JsonProperty("receiverAddress")]
        public string ReceiverAddress { get; set; }

        [JsonProperty("tokenUri")]
        public string TokenUri { get; set; }
    }
}