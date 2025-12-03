using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables;
using DCL.Backpack.Gifting.Events;
using DCL.Backpack.Gifting.Models;
using DCL.Diagnostics;
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

        public async UniTask ExecuteAsync(IGiftable giftable, string urn, CancellationToken ct)
        {
            try
            {
                var sprite = giftable switch
                {
                    WearableGiftable wg => await thumbnailProvider.GetAsync(wg.Wearable, ct),
                    EmoteGiftable eg => await thumbnailProvider.GetAsync(eg.Emote, ct),
                    _ => throw new NotSupportedException($"Unsupported giftable type: {giftable?.GetType().Name}")
                };

                if (ct.IsCancellationRequested) return;

                eventBus.Publish(new GiftingEvents.ThumbnailLoadedEvent(urn, sprite, success: true));
            }
            catch (OperationCanceledException)
            {
                // intentional no-op
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.GIFTING);
                eventBus.Publish(new GiftingEvents.ThumbnailLoadedEvent(urn, null, success: false));
            }
        }
    }
}