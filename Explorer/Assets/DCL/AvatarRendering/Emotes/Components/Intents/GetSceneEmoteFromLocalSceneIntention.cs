using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.ECSComponents;
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
        public ISceneData SceneData { get; }
        public string EmotePath { get; }
        public string EmoteHash { get; }
        public bool Loop { get; }
        public BodyShape BodyShape { get; }
        public AvatarEmoteMask Mask { get; }
        public LoadTimeout Timeout { get; private set; }
        public CancellationTokenSource CancellationTokenSource { get; }

        public GetSceneEmoteFromLocalSceneIntention(
            ISceneData sceneData,
            string emotePath,
            string emoteHash,
            BodyShape bodyShape,
            bool loop,
            AvatarEmoteMask mask,
            int timeout = StreamableLoadingDefaults.TIMEOUT)
        {
            EmotePath = emotePath;
            SceneData = sceneData;
            EmoteHash = emoteHash;
            BodyShape = bodyShape;
            Loop = loop;
            Mask = mask;
            CancellationTokenSource = new CancellationTokenSource();
            Timeout = new LoadTimeout(timeout, 0);
        }

        public bool Equals(GetSceneEmoteFromLocalSceneIntention other) =>
            EmoteHash.Equals(other.EmoteHash) && Loop == other.Loop && BodyShape.Equals(other.BodyShape) && Mask == other.Mask;

        public readonly URN NewSceneEmoteURN() =>
            $"{GetSceneEmoteFromRealmIntention.SCENE_EMOTE_PREFIX}:{SceneData.SceneShortInfo.Name}-{EmoteHash}-{Loop.ToString().ToLower()}";

        public void CreateAndAddPromiseToWorld(World world, IPartitionComponent partitionComponent, URLSubdirectory? customStreamingSubdirectory, IEmote emote)
        {
            // Local scene emotes always load as Legacy: gltFast cannot generate Mecanim clips at runtime
            var promise = GltfPromise.Create(world,
                GetGLTFIntention.Create(this.EmotePath, this.EmoteHash, mecanimAnimationClips: false),
                partitionComponent);

            world.Create(promise, emote, this.BodyShape);
        }

        public bool IsTimeout(float deltaTime)
        {
            // Timeout access returns a temporary value. We need to reassign the field or we lose the changes
            Timeout = new LoadTimeout(Timeout.Timeout, Timeout.ElapsedTime + deltaTime);
            bool result = Timeout.IsTimeout;
            return result;
        }
    }
}
