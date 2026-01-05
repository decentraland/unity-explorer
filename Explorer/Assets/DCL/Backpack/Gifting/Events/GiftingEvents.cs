using UnityEngine;

namespace DCL.Backpack.Gifting.Events
{
    public static class GiftingEvents
    {
        /// <summary>
        ///     Triggered By: LoadGiftableItemThumbnailCommand
        ///     When: A wearable or emote thumbnail has finished downloading (or failed).
        ///     Subscribers: WearablesGridPresenter, EmotesGridPresenter
        /// </summary>
        public readonly struct ThumbnailLoadedEvent
        {
            public readonly string Urn;
            public readonly Sprite? Sprite;
            public readonly bool Success;

            public ThumbnailLoadedEvent(string urn, Sprite? sprite, bool success)
            {
                Urn = urn;
                Sprite = sprite;
                Success = success;
            }
        }

        public readonly struct GiftTransferOpenRequested
        {
            public readonly string RecipientUserId;
            public readonly string RecipientName;
            public readonly string Urn;
            public readonly string DisplayName;
            public readonly Sprite? Thumbnail;

            public GiftTransferOpenRequested(
                string recipientUserId, string recipientName,
                string urn, string displayName, Sprite? thumbnail)
            {
                RecipientUserId = recipientUserId;
                RecipientName   = recipientName;
                Urn             = urn;
                DisplayName     = displayName;
                Thumbnail       = thumbnail;
            }
        }

        public enum GiftTransferPhase { WaitingForWallet, Authorizing, Broadcasting, Confirming, Completed, Failed }

        public readonly struct GiftTransferProgress
        {
            public readonly string Urn;
            public readonly GiftTransferPhase Phase;
            public readonly string? Message;

            public GiftTransferProgress(string urn, GiftTransferPhase phase, string? message = null)
            {
                Urn = urn;
                Phase = phase;
                Message = message;
            }
        }

        public readonly struct GiftTransferSucceeded
        {
            public readonly string Urn;

            public GiftTransferSucceeded(string urn)
            {
                Urn = urn;
            }
        }

        public readonly struct GiftTransferFailed
        {
            public readonly string Urn;
            public readonly string Reason;

            public GiftTransferFailed(string urn, string reason)
            {
                Urn    = urn;
                Reason = reason;
            }
        }


        public readonly struct OnSuccessfulGift
        {
            public readonly string ItemUrn;
            public readonly string SenderAddress;
            public readonly string ReceiverAddress;
            public readonly string ItemType;

            public OnSuccessfulGift(string itemUrn, string senderAddress, string receiverAddress, string itemType)
            {
                ItemUrn = itemUrn;
                SenderAddress = senderAddress;
                ReceiverAddress = receiverAddress;
                ItemType = itemType;
            }
        }

        /// <summary>
        ///     Published when a gift transfer fails, containing all data for analytics.
        /// </summary>
        public readonly struct OnFailedGift
        {
            public readonly string ItemUrn;
            public readonly string SenderAddress;
            public readonly string ReceiverAddress;
            public readonly string ItemType;

            public OnFailedGift(string itemUrn, string senderAddress, string receiverAddress, string itemType)
            {
                ItemUrn = itemUrn;
                SenderAddress = senderAddress;
                ReceiverAddress = receiverAddress;
                ItemType = itemType;
            }
        }

        public readonly struct OnCanceledGift
        {
            public readonly string ItemUrn;
            public readonly string SenderAddress;
            public readonly string ReceiverAddress;
            public readonly string ItemType;

            public OnCanceledGift(string itemUrn, string senderAddress, string receiverAddress, string itemType)
            {
                ItemUrn = itemUrn;
                SenderAddress = senderAddress;
                ReceiverAddress = receiverAddress;
                ItemType = itemType;
            }
        }

        public readonly struct OnSentGift
        {
            public readonly string ItemUrn;
            public readonly string SenderAddress;
            public readonly string ReceiverAddress;
            public readonly string ItemType;

            public OnSentGift(string itemUrn, string senderAddress, string receiverAddress, string itemType)
            {
                ItemUrn = itemUrn;
                SenderAddress = senderAddress;
                ReceiverAddress = receiverAddress;
                ItemType = itemType;
            }
        }
    }
}