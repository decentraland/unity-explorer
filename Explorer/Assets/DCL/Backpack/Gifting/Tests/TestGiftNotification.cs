using DCL.Diagnostics;
using UnityEngine;
using System;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using Random = UnityEngine.Random;

namespace DCL.Backpack.Gifting.Tests
{
    public class TestGiftNotification : MonoBehaviour
    {
        private const string SENDER_WALLET = "0xc81f875d23e9de99018fd109178a4856b1dd5e42";
        private const string MY_WALLET = "0x0000000000000000000000000000000000000000";
        
        private readonly string[] validTokenUris = 
        {
            "https://peer.decentraland.org/lambdas/collections/standard/erc721/137/0xc73b75640bac8bced8829d07aa57e694b446b3f9/4/1678",
            "https://peer.decentraland.org/lambdas/collections/standard/erc721/137/0x5b1f2f9462d7fc6535bbfb4b4c0dd5d85896be5f/2/1678",
            // "https://peer-ue-2.decentraland.zone/lambdas/collections/standard/erc721/137/0x4fcd400a147618a2184836927e0b458559a1ad16/0/1678",
            // "https://peer-ue-2.decentraland.zone/lambdas/collections/standard/erc721/137/0x3b5306be0da3202a5e7b00d1acc16a46cd88dfdc/9/1678",
        };
        
        [ContextMenu("Trigger Gift Notification (new payload")]
        public void TriggerNotificationNewPayload()
        {
            string randomUri = validTokenUris[Random.Range(0, validTokenUris.Length)];
            
            var meta = new GiftReceivedNotificationMetadata
            {
                SenderAddress = SENDER_WALLET,
                ReceiverAddress = MY_WALLET,
                TokenUri = randomUri
            };

            var notification = new GiftReceivedNotification
            {
                Id = Guid.NewGuid().ToString(), Type = NotificationType.TRANSFER_RECEIVED,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
                Read = false,
                Metadata = meta
            };

            ReportHub.Log(ReportCategory.GIFTING, $"[TestGift] Simulating gift from {randomUri}");
            NotificationsBusController.Instance.AddNotification(notification);
        }
    }
}