using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;

namespace DCL.AvatarRendering.Emotes
{
    public struct GetOwnedEmotesFromRealmIntention : ICountedLoadingIntention<IEmote>, IEquatable<GetOwnedEmotesFromRealmIntention>
    {
        public List<IEmote> Result;

        public CancellationTokenSource CancellationTokenSource { get; }
        public CommonLoadingArguments CommonArguments { get; set; }

        public GetOwnedEmotesFromRealmIntention(CommonLoadingArguments commonArguments) : this()
        {
            CommonArguments = commonArguments;
            CancellationTokenSource = new CancellationTokenSource();
            Result = ListPool<IEmote>.Get()!;
        }

        public bool Equals(GetOwnedEmotesFromRealmIntention other) =>
            CommonArguments.URL.Equals(other.CommonArguments.URL);

        public override bool Equals(object? obj) =>
            obj is GetOwnedEmotesFromRealmIntention other && Equals(other);

        public override int GetHashCode() =>
            CommonArguments.GetHashCode();

        public int TotalAmount { get; private set; }

        public void SetTotal(int total)
        {
            TotalAmount = total;
        }

        public void AppendToResult(IEmote resultElement)
        {
            Result.Add(resultElement);
        }
    }
}
