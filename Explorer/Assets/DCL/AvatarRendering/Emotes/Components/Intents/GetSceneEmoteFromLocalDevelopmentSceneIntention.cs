using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using ECS.StreamableLoading;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.AvatarRendering.Emotes
{
    public struct GetSceneEmoteFromLocalDevelopmentSceneIntention: IEquatable<GetSceneEmoteFromLocalDevelopmentSceneIntention>, IAssetIntention
    {
        private const string SCENE_EMOTE_PREFIX = "urn:decentraland:off-chain:scene-emote";

        public string SceneId { get; }
        public string EmoteHash { get; }
        public bool Loop { get; }
        public BodyShape BodyShape { get; }
        public LoadTimeout Timeout;
        public GameObject gltfRoot;

        public GetSceneEmoteFromLocalDevelopmentSceneIntention(
            string sceneId,
            string emoteHash,
            GameObject gltfRoot, BodyShape bodyShape, bool loop,int timeout = StreamableLoadingDefaults.TIMEOUT)
        {
            SceneId = sceneId;
            EmoteHash = emoteHash;
            this.gltfRoot = gltfRoot;
            BodyShape = bodyShape;
            Loop = loop;
            CancellationTokenSource = new CancellationTokenSource();
            Timeout = new LoadTimeout(timeout);
        }

        public bool Equals(GetSceneEmoteFromLocalDevelopmentSceneIntention other) =>
            EmoteHash.Equals(other.EmoteHash) && Loop == other.Loop && BodyShape.Equals(other.BodyShape) && gltfRoot == other.gltfRoot;

        public readonly URN NewSceneEmoteURN() =>
            $"{SCENE_EMOTE_PREFIX}:{SceneId}-{EmoteHash}-{Loop.ToString().ToLower()}";

        public CancellationTokenSource CancellationTokenSource { get; }
    }
}
