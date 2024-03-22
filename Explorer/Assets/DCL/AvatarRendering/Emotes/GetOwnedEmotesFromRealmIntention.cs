using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace DCL.AvatarRendering.Emotes
{
    public struct GetOwnedEmotesFromRealmIntention : ILoadingIntention, IEquatable<GetOwnedEmotesFromRealmIntention>
    {
        public CancellationTokenSource CancellationTokenSource { get; }
        public CommonLoadingArguments CommonArguments { get; set; }

        public GetOwnedEmotesFromRealmIntention(CommonLoadingArguments commonArguments) : this()
        {
            CommonArguments = commonArguments;
            CancellationTokenSource = new CancellationTokenSource();
        }

        public bool Equals(GetOwnedEmotesFromRealmIntention other) =>
            CommonArguments.URL.Equals(other.CommonArguments.URL);

        public override bool Equals(object? obj) =>
            obj is GetOwnedEmotesFromRealmIntention other && Equals(other);

        public override int GetHashCode() =>
            CommonArguments.GetHashCode();
    }
}
