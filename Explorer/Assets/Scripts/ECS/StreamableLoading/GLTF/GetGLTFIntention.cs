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
        public readonly bool IsSceneEmote;

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;
        public CommonLoadingArguments CommonArguments { get; set; }

        private GetGLTFIntention(
            ISceneData sceneData,
            string? name = null,
            string? hash = null,
            bool isSceneEmote = false,
            CancellationTokenSource? cancellationTokenSource = null)
        {
            SceneData = sceneData;
            Name = name;
            Hash = hash;
            IsSceneEmote = isSceneEmote;

            CommonArguments = new CommonLoadingArguments(
                URLAddress.EMPTY,
                cancellationTokenSource: cancellationTokenSource);
        }

        public static GetGLTFIntention Create(ISceneData sceneData, string name, string hash, bool isSceneEmote = false) => new (sceneData, name, hash, isSceneEmote);

        public bool Equals(GetGLTFIntention other) =>
            StringComparer.OrdinalIgnoreCase.Equals(Hash, other.Hash) || Name == other.Name;
    }
}
