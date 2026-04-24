using CommunicationData.URLHelpers;
using DCL.Ipfs;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace ECS.StreamableLoading.GLTF
{
    public struct GetGLTFIntention: ILoadingIntention, IEquatable<GetGLTFIntention>
    {
        public readonly string? Hash;
        public readonly string? Name; // File path
        public readonly bool MecanimAnimationClips;
        public readonly ContentDefinition[]? ContentMappings;

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;
        public CommonLoadingArguments CommonArguments { get; set; }

        private GetGLTFIntention(
            string? name = null,
            string? hash = null,
            bool mecanimAnimationClips = false,
            ContentDefinition[]? contentMappings = null,
            CancellationTokenSource? cancellationTokenSource = null)
        {
            Name = name;
            Hash = hash;
            MecanimAnimationClips = mecanimAnimationClips;
            ContentMappings = contentMappings;

            CommonArguments = new CommonLoadingArguments(
                URLAddress.EMPTY,
                cancellationTokenSource: cancellationTokenSource);
        }

        public static GetGLTFIntention Create(string name, string hash, bool mecanimAnimationClips = false, ContentDefinition[]? contentMappings = null) => new (name, hash, mecanimAnimationClips, contentMappings);

        // Identity: Hash + Name. Hash alone would be enough for content identity, but matching the
        // sibling GetGltfContainerAssetIntention's shape (Name + Hash) keeps the two layers
        // consistent. The previous OR equality (Hash || Name) was unsafe — two scenes can share a
        // Src path with different content hashes, and LSD hot-reload relies on Hash changing to
        // evict stale cache entries.
        public bool Equals(GetGLTFIntention other) =>
            Hash == other.Hash && Name == other.Name;

        public override bool Equals(object? obj) =>
            obj is GetGLTFIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Hash, Name);
    }
}
