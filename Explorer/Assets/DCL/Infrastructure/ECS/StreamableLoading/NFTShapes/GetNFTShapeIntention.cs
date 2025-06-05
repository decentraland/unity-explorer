using ECS.StreamableLoading.Cache.Disk.Cacheables;
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

        public bool DisableDiskCache => true;
        public CommonLoadingArguments CommonArguments { get; set; }

        public readonly CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public GetNFTShapeIntention(string urn, IURNSource urnSource)
        {
            this.URN = urn;

            // TODO nullity
            CommonArguments = new CommonLoadingArguments(urnSource.UrlOrEmpty(urn));
        }

        public bool Equals(GetNFTShapeIntention other) =>
            URN == other.URN;

        public override bool Equals(object? obj) =>
            obj is GetNFTShapeIntention other && Equals(other);

        public override int GetHashCode() =>
            URN.GetHashCode();

        public class DiskHashCompute : AbstractDiskHashCompute<GetNFTShapeIntention>
        {
            public static readonly DiskHashCompute INSTANCE = new ();

            private DiskHashCompute() { }

            protected override void FillPayload(IHashKeyPayload keyPayload, in GetNFTShapeIntention asset)
            {
                keyPayload.Put(asset.URN);
            }
        }
    }
}
