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
    }
}