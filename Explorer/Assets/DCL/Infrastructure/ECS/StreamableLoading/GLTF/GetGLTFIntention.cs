using CommunicationData.URLHelpers;
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

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;
        public CommonLoadingArguments CommonArguments { get; set; }

        private GetGLTFIntention(
            string? name = null,
            string? hash = null,
            bool mecanimAnimationClips = false,
            CancellationTokenSource? cancellationTokenSource = null)
        {
            Name = name;
            Hash = hash;
            MecanimAnimationClips = mecanimAnimationClips;

            CommonArguments = new CommonLoadingArguments(
                null!,
                cancellationTokenSource: cancellationTokenSource);
        }

        public static GetGLTFIntention Create(string name, string hash, bool mecanimAnimationClips = false) => new (name: name, hash: hash, mecanimAnimationClips: mecanimAnimationClips);

        public bool Equals(GetGLTFIntention other) =>
            StringComparer.OrdinalIgnoreCase.Equals(Hash, other.Hash) || Name == other.Name;
    }
}
