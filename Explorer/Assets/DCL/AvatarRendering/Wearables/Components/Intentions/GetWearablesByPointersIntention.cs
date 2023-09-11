using DCL.AvatarRendering.Wearables.Helpers;
using DCL.ECSComponents;
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

        public GetWearablesByPointersIntention(List<string> pointers, Wearable[] result, PBAvatarShape bodyShape)
        {
            Pointers = pointers;
            Results = result;
            BodyShape = bodyShape;
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
