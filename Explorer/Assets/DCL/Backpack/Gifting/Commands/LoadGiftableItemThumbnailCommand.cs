using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.Gifting.Events;
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

        public async UniTaskVoid ExecuteAsync(IWearable wearable, CancellationToken ct)
        {
            try
            {
                var sprite = await thumbnailProvider.GetAsync(wearable, ct);
                if (ct.IsCancellationRequested) return;

                eventBus.Publish(new GiftingEvents.ThumbnailLoadedEvent(wearable.GetUrn(), sprite, success: true));
            }
            catch (OperationCanceledException)
            {
                /* Suppress */
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                eventBus.Publish(new GiftingEvents.ThumbnailLoadedEvent(wearable.GetUrn(), null, success: false));
            }
        }
    }
}