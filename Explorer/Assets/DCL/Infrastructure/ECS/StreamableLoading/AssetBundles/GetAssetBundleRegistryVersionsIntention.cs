using CommunicationData.URLHelpers;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace ECS.StreamableLoading.AssetBundles
{
    public struct GetAssetBundleRegistryVersionsIntention : ILoadingIntention, IEquatable<GetAssetBundleRegistryVersionsIntention>
    {
        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;
        public CommonLoadingArguments CommonArguments { get; set; }
        public readonly URN[] Pointers;

        private GetAssetBundleRegistryVersionsIntention(URN[] pointers, CommonLoadingArguments commonArguments)
        {
            Pointers = pointers;
            CommonArguments = commonArguments;
        }

        public bool Equals(GetAssetBundleRegistryVersionsIntention other)
        {
            if (other.Pointers.Length != Pointers.Length) return false;

            for (int i = 0; i < other.Pointers.Length; i++)
            {
                if (Pointers[i] != other.Pointers[i])
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj) =>
            obj is GetAssetBundleManifestIntention other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var s in Pointers)
            {
                hash.Add(s);
            }
            return hash.ToHashCode();
        }

        public static GetAssetBundleRegistryVersionsIntention Create(URN[] pointers, CommonLoadingArguments commonArguments) =>
            new (pointers, commonArguments);
    }
}
