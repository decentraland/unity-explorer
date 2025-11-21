namespace DCL.Backpack.Gifting.Tests
{
    using UnityEngine;
    using NotificationsBus;
    using NotificationsBus.NotificationTypes;
    using System;

    public class TestGiftNotification : MonoBehaviour
    {
        private const string FIXED_WALLET = "0xc81f875d23e9de99018fd109178a4856b1dd5e42";
        private const string FIXED_IMAGE_URL = "https://assets-cdn.decentraland.org/social/communities/c5815049-5a02-4c22-a068-19cc44b1b8a4/raw-thumbnail.png";

        // "Real" looking data pools
        private readonly string[] possibleNames =
        {
            "CryptoKing", "MetaExplorer", "DCL_Architect", "PixelPioneer", "EtherBaron", "ManaTrader", "VoxelArtist", "DecentraFan"
        };

        private readonly string[] itemNames =
        {
            "Cyberpunk Sneakers", "Golden Wings", "Mana Hoodie", "Neon Visor", "Ancient Staff", "Space Helmet", "Festival T-Shirt", "Diamond Earnings"
        };

        private readonly string[] rarities =
        {
            "common", "uncommon", "rare", "epic", "legendary", "mythic", "unique"
        };

        private readonly string[] categories =
        {
            "wearable", "emote", "smart_wearable"
        };

        [ContextMenu("Trigger Gift Notification")]
        public void TriggerNotification()
        {
            // 1. Generate Random Data
            string randomSenderName = possibleNames[UnityEngine.Random.Range(0, possibleNames.Length)];
            string randomItemName = itemNames[UnityEngine.Random.Range(0, itemNames.Length)];
            string randomRarity = rarities[UnityEngine.Random.Range(0, rarities.Length)];
            string randomCategory = categories[UnityEngine.Random.Range(0, categories.Length)];

            // Random Token ID (e.g., 1 to 5000)
            string randomTokenId = UnityEngine.Random.Range(1, 5000).ToString();

            // Random Request ID (GUID)
            string randomRequestId = Guid.NewGuid().ToString();

            // Random Claimed Name status
            bool isClaimedName = UnityEngine.Random.value > 0.5f;

            // 2. Create Item Metadata
            var giftItem = new GiftItemMetadata
            {
                GiftName = randomItemName, GiftRarity = randomRarity, ImageUrl = FIXED_IMAGE_URL, GiftCategory = randomCategory,
                TokenId = randomTokenId
            };

            // 3. Create Notification Object
            var notification = new GiftReceivedNotification
            {
                Id = Guid.NewGuid().ToString(), Type = NotificationType.GIFT_RECEIVED, Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(), Read = false,
                Metadata = new GiftReceivedNotificationMetadata
                {
                    Sender = new GiftProfile
                    {
                        Address = FIXED_WALLET, Name = randomSenderName, ProfileImageUrl = "", HasClaimedName = isClaimedName
                    },
                    Receiver = new GiftProfile
                    {
                        Address = "0xMyAddress...", Name = "Me", HasClaimedName = true
                    },
                    RequestId = randomRequestId, Item = giftItem
                }
            };

            // 4. Send to Bus
            Debug.Log($"[TestGift] Sending notification from {randomSenderName} sending {randomItemName} ({randomRarity})");
            NotificationsBusController.Instance.AddNotification(notification);
        }
    }
}