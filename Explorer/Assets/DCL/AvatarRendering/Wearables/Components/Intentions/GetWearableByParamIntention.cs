using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.AvatarRendering.Wearables.Components.Intentions
{
    public struct GetWearableByParamIntention : IAssetIntention, IEquatable<GetWearableByParamIntention>
    {
        public (string, string)[] Params;
        public string UserID;

        //Used for pooling. Its the responsibility of the consumer to rent and release
        public List<Wearable> Results;
        public CancellationTokenSource CancellationTokenSource { get; }

        public bool Equals(GetWearableByParamIntention other) =>
            Equals(Params, other.Params) && UserID == other.UserID;

        public override bool Equals(object obj) =>
            obj is GetWearableByParamIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Params, UserID);
    }
}
