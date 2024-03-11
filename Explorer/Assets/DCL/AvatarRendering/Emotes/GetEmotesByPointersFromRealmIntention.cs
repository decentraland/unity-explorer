using CommunicationData.URLHelpers;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.AvatarRendering.Emotes
{
    public struct GetEmotesByPointersFromRealmIntention : IEquatable<GetEmotesByPointersFromRealmIntention>, ILoadingIntention
    {
        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;
        public CommonLoadingArguments CommonArguments { get; set; }

        public readonly IReadOnlyList<URN> Pointers;

        public GetEmotesByPointersFromRealmIntention(IReadOnlyList<URN> pointers, CommonLoadingArguments commonArguments)
        {
            Pointers = pointers;
            CommonArguments = commonArguments;
        }

        public bool Equals(GetEmotesByPointersFromRealmIntention other) =>
            Equals(Pointers, other.Pointers);

        public override bool Equals(object obj) =>
            obj is GetEmotesByPointersFromRealmIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Pointers);
    }
}
