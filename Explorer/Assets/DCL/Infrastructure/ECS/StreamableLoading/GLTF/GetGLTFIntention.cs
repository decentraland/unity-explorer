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

        public bool Equals(GetGLTFIntention other) =>
            StringComparer.OrdinalIgnoreCase.Equals(Hash, other.Hash) || Name == other.Name;
    }
}
