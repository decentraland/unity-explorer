using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.NFTShapes.URNs;
using System;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.NFTShapes
{
    public struct GetNFTShapeIntention : ILoadingIntention, IEquatable<GetNFTShapeIntention>
    {
        public static readonly TextureWrapMode WRAP_MODE = TextureWrapMode.Clamp;
        public static readonly FilterMode FILTER_MODE = FilterMode.Bilinear;

        public readonly string URN;

        public CommonLoadingArguments CommonArguments { get; set; }

        public readonly CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public GetNFTShapeIntention(string urn, IURNSource urnSource)
        {
            this.URN = urn;
            CommonArguments = new CommonLoadingArguments(urnSource.UrlOrEmpty(urn));
        }

        public bool Equals(GetNFTShapeIntention other) =>
            URN == other.URN
            && CommonArguments.Equals(other.CommonArguments)
            && this.AreUrlEquals(other);

        public override bool Equals(object? obj) =>
            obj is GetNFTShapeIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(URN, CommonArguments, CancellationTokenSource);
    }
}
