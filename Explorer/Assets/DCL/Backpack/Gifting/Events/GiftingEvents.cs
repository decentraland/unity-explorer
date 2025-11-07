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
            public readonly Sprite Sprite;
            public readonly bool Success;

            public ThumbnailLoadedEvent(string urn, Sprite sprite, bool success)
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
            public readonly string? TxHash;

            public GiftTransferSucceeded(string urn, string? txHash)
            {
                Urn    = urn;
                TxHash = txHash;
            }
        }

        public readonly struct GiftTransferFailed
        {
            public readonly string Urn;
            public readonly string Reason; // short error for UX

            public GiftTransferFailed(string urn, string reason)
            {
                Urn    = urn;
                Reason = reason;
            }
        }

        public readonly struct OnSuccessfullGift
        {
            public readonly string Urn;
            public readonly Sprite Sprite;
            public readonly bool Success;

            public OnSuccessfullGift(string urn, Sprite sprite, bool success)
            {
                Urn = urn;
                Sprite = sprite;
                Success = success;
            }
        }

        public readonly struct OnFailedGift
        {
            public readonly string Urn;
            public readonly Sprite Sprite;
            public readonly bool Success;

            public OnFailedGift(string urn, Sprite sprite, bool success)
            {
                Urn = urn;
                Sprite = sprite;
                Success = success;
            }
        }
    }
}