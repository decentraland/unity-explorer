using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading;
using ECS.StreamableLoading.GLTF;
using SceneRunner.Scene;
using System;
using System.Threading;

using GltfPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.GLTF.GLTFData, ECS.StreamableLoading.GLTF.GetGLTFIntention>;

namespace DCL.AvatarRendering.Emotes
{
    public struct GetSceneEmoteFromLocalSceneIntention : IEquatable<GetSceneEmoteFromLocalSceneIntention>, IEmoteAssetIntention
    {
        private const string SCENE_EMOTE_PREFIX = "urn:decentraland:off-chain:scene-emote";

        public ISceneData SceneData { get; }
        public string EmotePath { get; }
        public string EmoteHash { get; }
        public bool Loop { get; }
        public BodyShape BodyShape { get; }
        public LoadTimeout Timeout { get; }
        public CancellationTokenSource CancellationTokenSource { get; }

        public GetSceneEmoteFromLocalSceneIntention(
            ISceneData sceneData,
            string emotePath,
            string emoteHash,
            BodyShape bodyShape,
            bool loop,
            int timeout = StreamableLoadingDefaults.TIMEOUT)
        {
            EmotePath = emotePath;
            SceneData = sceneData;
            EmoteHash = emoteHash;
            BodyShape = bodyShape;
            Loop = loop;
            CancellationTokenSource = new CancellationTokenSource();
            Timeout = new LoadTimeout(timeout);
        }

        public bool Equals(GetSceneEmoteFromLocalSceneIntention other) =>
            EmoteHash.Equals(other.EmoteHash) && Loop == other.Loop && BodyShape.Equals(other.BodyShape);

        public readonly URN NewSceneEmoteURN() =>
            $"{SCENE_EMOTE_PREFIX}:{SceneData.SceneShortInfo.Name}-{EmoteHash}-{Loop.ToString().ToLower()}";

        public void CreateAndAddPromiseToWorld(World world, IPartitionComponent partitionComponent, URLSubdirectory? customStreamingSubdirectory, IEmote emote)
        {
            var promise = GltfPromise.Create(world,
                GetGLTFIntention.Create(this.EmotePath, this.EmoteHash, mecanimAnimationClips: false),
                partitionComponent);

            world.Create(promise, emote, this.BodyShape);
        }
    }
}
