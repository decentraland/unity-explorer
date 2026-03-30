using DCL.Profiles;
using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBus.NotificationTypes
{
    [Serializable]
    public class TipReceivedNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "New Tip received!";
        private const string NOTIFICATION_TITLE_FORMAT = "sent you a <b>{0:#.##} MANA</b> tip for one of your scenes!";

        public Profile.CompactInfo? SenderProfile { get; set; }

        [JsonProperty("metadata")]
        public TipReceivedNotificationMetadata Metadata { get; set; }

        public override string GetHeader() =>
            NOTIFICATION_HEADER;

        public override string GetTitle() =>
            NOTIFICATION_TITLE_FORMAT;

        public override string GetThumbnail() =>
            SenderProfile?.FaceSnapshotUrl.Value ?? string.Empty;
    }

    [Serializable]
    public struct TipReceivedNotificationMetadata
    {
        [JsonProperty("senderAddress")]
        public string SenderAddress { get; set; }

        [JsonProperty("amount")]
        public decimal TipAmount { get; set; }

        [JsonProperty("receiverAddress")]
        public string ReceiverAddress { get; set; }

    }
}
