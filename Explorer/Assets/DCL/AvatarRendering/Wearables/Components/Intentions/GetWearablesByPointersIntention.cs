using AssetManagement;
using DCL.ECSComponents;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.AvatarRendering.Wearables.Components.Intentions
{
    public struct GetWearablesByPointersIntention : IAssetIntention, IEquatable<GetWearablesByPointersIntention>
    {
        public readonly List<string> Pointers;
        public readonly IWearable[] Results;
        public readonly AssetSource PermittedSources;
        public readonly BodyShape BodyShape;
        public readonly bool FallbackToDefaultWearables;

        public CancellationTokenSource CancellationTokenSource { get; }

        public GetWearablesByPointersIntention(List<string> pointers, IWearable[] result, PBAvatarShape bodyShape, bool fallbackToDefaultWearables = true)
            : this(pointers, result, (BodyShape)bodyShape, fallbackToDefaultWearables: fallbackToDefaultWearables) { }

        public GetWearablesByPointersIntention(List<string> pointers, IWearable[] result, BodyShape bodyShape, AssetSource permittedSources = AssetSource.ALL, bool fallbackToDefaultWearables = true)
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
    }
}
