using AssetManagement;
using DCL.AvatarRendering.Wearables;
using ECS.StreamableLoading;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using System.Threading;

namespace DCL.AvatarRendering.Emotes
{
    public struct GetSceneEmoteFromRealmIntention : IEquatable<GetSceneEmoteFromRealmIntention>, IAssetIntention
    {
        public CancellationTokenSource CancellationTokenSource { get; }
        public string SceneId { get; }
        public SceneAssetBundleManifest AssetBundleManifest { get; }
        public string EmoteHash { get; }
        public bool Loop { get; }
        public AssetSource PermittedSources { get; }
        public BodyShape BodyShape { get; }
        public int Timeout { get; }
        public bool IsAssetBundleProcessed { get; set; }
        public float ElapsedTime { get; set; }

        public GetSceneEmoteFromRealmIntention(string sceneId,
            SceneAssetBundleManifest assetBundleManifest,
            string emoteHash,
            bool loop,
            BodyShape bodyShape,
            AssetSource permittedSources = AssetSource.ALL,
            int timeout = StreamableLoadingDefaults.TIMEOUT) : this()
        {
            SceneId = sceneId;
            AssetBundleManifest = assetBundleManifest;
            EmoteHash = emoteHash;
            Loop = loop;
            CancellationTokenSource = new CancellationTokenSource();
            PermittedSources = permittedSources;
            BodyShape = bodyShape;
            Timeout = timeout;
        }

        public bool Equals(GetSceneEmoteFromRealmIntention other) =>
            EmoteHash == other.EmoteHash && Loop == other.Loop && BodyShape.Equals(other.BodyShape);
    }
}
