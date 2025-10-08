using CommunicationData.URLHelpers;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace ECS.StreamableLoading.NFTShapes
{
    public struct GetNFTVideoIntention : ILoadingIntention, IEquatable<GetNFTVideoIntention>
    {
        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;
        public CommonLoadingArguments CommonArguments { get; set; }

        public GetNFTVideoIntention(URLAddress url)
        {
            CommonArguments = new CommonLoadingArguments(url);
        }

        public bool Equals(GetNFTVideoIntention other) =>
            CommonArguments.URL == other.CommonArguments.URL;

        public override bool Equals(object? obj) =>
            obj is GetNFTVideoIntention other && Equals(other);

        public override int GetHashCode() =>
            CommonArguments.URL.GetHashCode();
    }
}
