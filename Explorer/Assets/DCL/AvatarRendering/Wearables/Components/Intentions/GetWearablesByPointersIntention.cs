using AssetManagement;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Threading;
using static DCL.AvatarRendering.Wearables.Helpers.WearableComponentsUtils;

namespace DCL.AvatarRendering.Wearables.Components.Intentions
{
    public struct GetWearablesByPointersIntention : IAssetIntention, IDisposable, IEquatable<GetWearablesByPointersIntention>
    {
        public readonly List<string> Pointers;
        public readonly IWearable[] Results;

        public readonly AssetSource PermittedSources;
        public readonly BodyShape BodyShape;
        public readonly bool FallbackToDefaultWearables;

        public CancellationTokenSource CancellationTokenSource { get; }

        internal GetWearablesByPointersIntention(List<string> pointers, IWearable[] result, BodyShape bodyShape, AssetSource permittedSources = AssetSource.ALL,
            bool fallbackToDefaultWearables = true)
        {
            Pointers = pointers;
            Results = result;
            BodyShape = bodyShape;
            FallbackToDefaultWearables = fallbackToDefaultWearables;
            PermittedSources = permittedSources;
            CancellationTokenSource = new CancellationTokenSource();
        }

        public bool Equals(GetWearablesByPointersIntention other) =>
            Equals(Pointers, other.Pointers);

        public override bool Equals(object obj) =>
            obj is GetWearablesByPointersIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Pointers);

        public void Dispose()
        {
            POINTERS_POOL.Release(Pointers);
            RESULTS_POOL.Return(Results, clearArray: true);
        }
    }
}
