using CommunicationData.URLHelpers;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using System.Threading;

namespace ECS.StreamableLoading.GLTF
{
    public struct GetGLTFIntention: ILoadingIntention, IEquatable<GetGLTFIntention>
    {
        public readonly string? Hash;
        public readonly string? Name; // File path
        public readonly ISceneData SceneData;
        public readonly bool MecanimAnimationClips;

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;
        public CommonLoadingArguments CommonArguments { get; set; }

        private GetGLTFIntention(
            ISceneData sceneData,
            bool mecanimAnimationClips,
            string? name = null,
            string? hash = null,
            CancellationTokenSource? cancellationTokenSource = null)
        {
            SceneData = sceneData;
            MecanimAnimationClips = mecanimAnimationClips;
            Name = name;
            Hash = hash;

            CommonArguments = new CommonLoadingArguments(
                URLAddress.EMPTY,
                cancellationTokenSource: cancellationTokenSource);
        }

        public static GetGLTFIntention Create(ISceneData sceneData, string name, string hash, bool mecanimAnimationClips) =>
            new (sceneData, mecanimAnimationClips, name, hash) ;

        public bool Equals(GetGLTFIntention other) =>
            StringComparer.OrdinalIgnoreCase.Equals(Hash, other.Hash) || Name == other.Name;
    }
}
