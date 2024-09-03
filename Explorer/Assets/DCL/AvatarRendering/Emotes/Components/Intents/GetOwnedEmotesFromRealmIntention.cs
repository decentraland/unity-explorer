using DCL.AvatarRendering.Loading.Components;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace DCL.AvatarRendering.Emotes
{
    public struct GetOwnedEmotesFromRealmIntention : ICountedLoadingIntention<IEmote>, IEquatable<GetOwnedEmotesFromRealmIntention>
    {
        public RepoolableList<IEmote> Result { get; }

        public CancellationTokenSource CancellationTokenSource { get; }

        public CommonLoadingArguments CommonArguments { get; set; }

        public GetOwnedEmotesFromRealmIntention(CommonLoadingArguments commonArguments) : this()
        {
            CommonArguments = commonArguments;
            CancellationTokenSource = new CancellationTokenSource();
            Result = RepoolableList<IEmote>.NewList();
        }

        public bool Equals(GetOwnedEmotesFromRealmIntention other) =>
            CommonArguments.URL.Equals(other.CommonArguments.URL);

        public override bool Equals(object? obj) =>
            obj is GetOwnedEmotesFromRealmIntention other && Equals(other);

        public override int GetHashCode() =>
            CommonArguments.GetHashCode();

        public void AppendToResult(IEmote resultElement)
        {
            Result.List.Add(resultElement);
        }
    }
}
