using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.Profiling;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Threading;
using static DCL.AvatarRendering.Wearables.Helpers.WearableComponentsUtils;

namespace DCL.AvatarRendering.Wearables.Components.Intentions
{
    public struct GetWearablesByPointersIntention : IAssetIntention, IDisposable, IEquatable<GetWearablesByPointersIntention>
    {
        public HideWearablesResolution HideWearablesResolution;
        public readonly BodyShape BodyShape;
        public readonly bool FallbackToDefaultWearables;

        public readonly AssetSource PermittedSources;
        public readonly List<URN> Pointers;

        /// <summary>
        ///     Instead of storing a separate collection for the resolved wearables, we store the indices of the resolved wearables in the WearableAssetResults array.
        /// </summary>
        public long ResolvedWearablesIndices;

        internal GetWearablesByPointersIntention(List<URN> pointers, BodyShape bodyShape, IReadOnlyCollection<string> forceRender, AssetSource permittedSources = AssetSource.ALL,
            bool fallbackToDefaultWearables = true)
        {
            Pointers = pointers;
            BodyShape = bodyShape;
            HideWearablesResolution = new HideWearablesResolution(forceRender);
            FallbackToDefaultWearables = fallbackToDefaultWearables;
            PermittedSources = permittedSources;
            CancellationTokenSource = new CancellationTokenSource();
            ResolvedWearablesIndices = 0;

            ProfilingCounters.GetWearablesIntentionAmount.Value++;
        }

        public CancellationTokenSource CancellationTokenSource { get; }

        public void Dispose()
        {
            POINTERS_POOL.Release(Pointers);

            ProfilingCounters.GetWearablesIntentionAmount.Value--;
        }

        public bool Equals(GetWearablesByPointersIntention other) =>
            Equals(Pointers, other.Pointers);

        public override bool Equals(object obj) =>
            obj is GetWearablesByPointersIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Pointers);
    }
}
