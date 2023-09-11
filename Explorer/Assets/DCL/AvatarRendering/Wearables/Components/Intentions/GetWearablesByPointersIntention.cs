using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.AvatarRendering.Wearables.Components.Intentions
{
    public struct GetWearablesByPointersIntention : IAssetIntention, IEquatable<GetWearablesByPointersIntention>
    {
        public List<string> Pointers;
        public Wearable[] Results;
        public WearablesLiterals.BodyShape BodyShape;
        public CancellationTokenSource CancellationTokenSource { get; }

        public bool Equals(GetWearablesByPointersIntention other) =>
            Equals(Pointers, other.Pointers);

        public override bool Equals(object obj) =>
            obj is GetWearablesByPointersIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Pointers);
    }
}
