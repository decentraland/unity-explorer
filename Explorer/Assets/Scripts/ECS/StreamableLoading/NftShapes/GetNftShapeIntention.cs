using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.NftShapes.Urns;
using System;
using System.Threading;

namespace ECS.StreamableLoading.NftShapes
{
    public struct GetNftShapeIntention : ILoadingIntention, IEquatable<GetNftShapeIntention>
    {
        public readonly string URN;

        public CommonLoadingArguments CommonArguments { get; set; }

        public readonly CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public GetNftShapeIntention(string urn, IUrnSource urnSource)
        {
            this.URN = urn;
            CommonArguments = new CommonLoadingArguments(urnSource.UrlOrEmpty(urn));
        }

        public bool Equals(GetNftShapeIntention other) =>
            URN == other.URN
            && CommonArguments.Equals(other.CommonArguments)
            && CancellationTokenSource.Equals(other.CancellationTokenSource);

        public override bool Equals(object? obj) =>
            obj is GetNftShapeIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(URN, CommonArguments, CancellationTokenSource);
    }
}
