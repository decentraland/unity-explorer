using CommunicationData.URLHelpers;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace ECS.StreamableLoading.NFTShapes
{
    public struct GetNFTImageIntention : ILoadingIntention, IEquatable<GetNFTImageIntention>
    {
        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;
        public CommonLoadingArguments CommonArguments { get; set; }

        public GetNFTImageIntention(URLAddress url)
        {
            CommonArguments = new CommonLoadingArguments(url);
        }

        public bool Equals(GetNFTImageIntention other) =>
            CommonArguments.URL == other.CommonArguments.URL;

        public override bool Equals(object? obj) =>
            obj is GetNFTImageIntention other && Equals(other);

        public override int GetHashCode() =>
            CommonArguments.URL.GetHashCode();
    }
}
