using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using ECS.StreamableLoading;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.AvatarRendering.Emotes
{
    public struct GetSceneEmoteFromLocalDevelopmentSceneIntention: IEquatable<GetSceneEmoteFromLocalDevelopmentSceneIntention>, IAssetIntention
    {
        private const string SCENE_EMOTE_PREFIX = "urn:decentraland:off-chain:scene-emote";

        public ISceneData SceneData { get; }
        public string EmotePath{ get; }
        public string EmoteHash { get; }
        public bool Loop { get; }
        public BodyShape BodyShape { get; }
        public LoadTimeout Timeout;
        //public GameObject gltfRoot;

        public GetSceneEmoteFromLocalDevelopmentSceneIntention(
            ISceneData sceneData,
            string emotePath,
            string emoteHash,
            //GameObject gltfRoot,
            BodyShape bodyShape, bool loop,int timeout = StreamableLoadingDefaults.TIMEOUT)
        {
            EmotePath = emotePath;
            SceneData = sceneData;
            EmoteHash = emoteHash;
            //this.gltfRoot = gltfRoot;
            BodyShape = bodyShape;
            Loop = loop;
            CancellationTokenSource = new CancellationTokenSource();
            Timeout = new LoadTimeout(timeout);
        }

        public bool Equals(GetSceneEmoteFromLocalDevelopmentSceneIntention other) =>
            EmoteHash.Equals(other.EmoteHash) && Loop == other.Loop && BodyShape.Equals(other.BodyShape);// && gltfRoot == other.gltfRoot;

        public readonly URN NewSceneEmoteURN() =>
            $"{SCENE_EMOTE_PREFIX}:{SceneData.SceneShortInfo.Name}-{EmoteHash}-{Loop.ToString().ToLower()}";

        public CancellationTokenSource CancellationTokenSource { get; }
    }
}
