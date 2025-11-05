using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.Gifting.Events;
using DCL.Backpack.Gifting.Models;
using UnityEngine;
using Utility;

namespace DCL.Backpack.Gifting.Commands
{
    public class LoadGiftableItemThumbnailCommand
    {
        private readonly IThumbnailProvider thumbnailProvider;
        private readonly IEventBus eventBus;

        public LoadGiftableItemThumbnailCommand(IThumbnailProvider thumbnailProvider, IEventBus eventBus)
        {
            this.thumbnailProvider = thumbnailProvider;
            this.eventBus = eventBus;
        }

        public async UniTaskVoid ExecuteAsync(IGiftable giftable, string urn, CancellationToken ct)
        {
            try
            {
                Sprite sprite;

                switch (giftable)
                {
                    case WearableGiftable wg:
                        sprite = await thumbnailProvider.GetAsync(wg.Wearable, ct);
                        break;

                    case EmoteGiftable eg:
                        sprite = await thumbnailProvider.GetAsync(eg.Emote, ct);
                        break;

                    default:
                        throw new NotSupportedException($"Unsupported giftable type: {giftable?.GetType().Name}");
                }

                if (ct.IsCancellationRequested) return;

                eventBus.Publish(new GiftingEvents.ThumbnailLoadedEvent(urn, sprite, success: true));
            }
            catch (OperationCanceledException)
            {
                // intentional no-op
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                eventBus.Publish(new GiftingEvents.ThumbnailLoadedEvent(urn, null, success: false));
            }
        }
    }
}