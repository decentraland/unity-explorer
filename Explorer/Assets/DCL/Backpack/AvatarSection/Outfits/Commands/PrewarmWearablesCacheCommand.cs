using System;
using System.Collections.Generic;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables;
using DCL.Diagnostics;

namespace DCL.Backpack.AvatarSection.Outfits.Commands
{
    public class PrewarmWearablesCacheCommand
    {
        private readonly IWearablesProvider wearablesProvider;

        public PrewarmWearablesCacheCommand(IWearablesProvider wearablesProvider)
        {
            this.wearablesProvider = wearablesProvider;
        }

        public async UniTask ExecuteAsync(IReadOnlyCollection<URN> wearableUrns, CancellationToken ct)
        {
            if (wearableUrns == null || wearableUrns.Count == 0)
                return;

            try
            {
                // This triggers the LoadElementsByIntentionSystem, which writes the DTOs to WearableStorage (the cache).
                // We pass BodyShape.MALE as a placeholder; for just caching the DTO, the specific body shape is irrelevant,
                // but the underlying ECS system requires it to build the intention.
                // The URN list already contains the body shape URN itself, so we can use a default placeholder.
                await wearablesProvider.RequestPointersAsync(wearableUrns, BodyShape.MALE, ct);
                ReportHub.Log(ReportCategory.OUTFITS, $"[OUTFIT_PREWARM] Successfully requested cache pre-warm for {wearableUrns.Count} URNs.");
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation, suppress
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, "[OUTFIT_PREWARM] Failed during cache pre-warming.");
            }
        }
    }
}