using Newtonsoft.Json;
using System;

namespace DCL.NotificationsBus.NotificationTypes
{
    [Serializable]
    public class GiftReceivedNotification : NotificationBase
    {
        private const string NOTIFICATION_HEADER = "Gift received";
        private const string NOTIFICATION_TITLE = "sent you a gift!";

        [JsonProperty("metadata")]
        public GiftReceivedNotificationMetadata Metadata { get; set; }

        public override string GetHeader()
        {
            return NOTIFICATION_HEADER;
        }

        public override string GetTitle()
        {
            return NOTIFICATION_TITLE;
        }

        public override string GetThumbnail()
        {
            // Prefer item thumbnail, fallback to sender avatar
            if (!string.IsNullOrEmpty(Metadata.Item.ImageUrl))
                return Metadata.Item.ImageUrl;

            return Metadata.Sender.ProfileImageUrl;
        }
    }

    [Serializable]
    public struct GiftProfile
    {
        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("profileImageUrl")]
        public string ProfileImageUrl { get; set; }

        [JsonProperty("hasClaimedName")]
        public bool HasClaimedName { get; set; }
    }

    [Serializable]
    public struct GiftItemMetadata
    {
        [JsonProperty("name")]
        public string GiftName { get; set; }

        [JsonProperty("imageUrl")]
        public string ImageUrl { get; set; }

        [JsonProperty("category")]
        public string GiftCategory { get; set; }

        [JsonProperty("rarity")]
        public string GiftRarity { get; set; }

        [JsonProperty("tokenId")]
        public string TokenId { get; set; }
    }

    [Serializable]
    public struct GiftReceivedNotificationMetadata
    {
        [JsonProperty("sender")]
        public GiftProfile Sender { get; set; }

        [JsonProperty("receiver")]
        public GiftProfile Receiver { get; set; }

        [JsonProperty("requestId")]
        public string RequestId { get; set; }

        [JsonProperty("item")]
        public GiftItemMetadata Item { get; set; }
    }
}