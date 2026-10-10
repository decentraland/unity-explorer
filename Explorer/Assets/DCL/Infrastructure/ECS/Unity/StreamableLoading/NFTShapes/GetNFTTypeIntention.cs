using CommunicationData.URLHelpers;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace ECS.StreamableLoading.NFTShapes
{
    public struct GetNFTTypeIntention : ILoadingIntention, IEquatable<GetNFTTypeIntention>
    {
        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;
        public CommonLoadingArguments CommonArguments { get; set; }


        public GetNFTTypeIntention(URLAddress urn)
        {
            CommonArguments = new CommonLoadingArguments(urn);
        }

        public bool Equals(GetNFTTypeIntention other) =>
            CommonArguments.URL == other.CommonArguments.URL;

        public override bool Equals(object? obj) =>
            obj is GetNFTTypeIntention other && Equals(other);

        public override int GetHashCode() =>
            CommonArguments.URL.GetHashCode();
    }
}
