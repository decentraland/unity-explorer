using Arch.Core;
using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading;
using ECS.StreamableLoading.AssetBundles;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;
using Utility;

using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.AvatarRendering.Emotes
{
    public struct GetSceneEmoteFromRealmIntention : IEquatable<GetSceneEmoteFromRealmIntention>, IEmoteAssetIntention
    {
        private const string SCENE_EMOTE_PREFIX = "urn:decentraland:off-chain:scene-emote";

        public CancellationTokenSource CancellationTokenSource { get; }
        public string SceneId { get; }
        public SceneAssetBundleManifest AssetBundleManifest { get; }
        public string EmoteHash { get; }
        public bool Loop { get; }
        public AssetSource PermittedSources { get; }
        public BodyShape BodyShape { get; }
        public bool IsAssetBundleProcessed { get; set; }

        public LoadTimeout Timeout { get; }

        public GetSceneEmoteFromRealmIntention(
            string sceneId,
            SceneAssetBundleManifest assetBundleManifest,
            string emoteHash,
            bool loop,
            BodyShape bodyShape,
            AssetSource permittedSources = AssetSource.ALL,
            int timeout = StreamableLoadingDefaults.TIMEOUT
        ) : this()
        {
            SceneId = sceneId;
            AssetBundleManifest = assetBundleManifest;
            EmoteHash = emoteHash;
            Loop = loop;
            CancellationTokenSource = new CancellationTokenSource();
            PermittedSources = permittedSources;
            BodyShape = bodyShape;
            Timeout = new LoadTimeout(timeout);
        }

        public bool Equals(GetSceneEmoteFromRealmIntention other) =>
            EmoteHash == other.EmoteHash && Loop == other.Loop && BodyShape.Equals(other.BodyShape);

        public readonly URN NewSceneEmoteURN() =>
            $"{SCENE_EMOTE_PREFIX}:{SceneId}-{EmoteHash}-{Loop.ToString().ToLower()}";

        public void CreateAndAddPromiseToWorld(World world, IPartitionComponent partitionComponent, URLSubdirectory? customStreamingSubdirectory, IEmote emote)
        {
            var promise = AssetBundlePromise.Create(world,
                GetAssetBundleIntention.FromHash(typeof(GameObject),
                    this.EmoteHash + PlatformUtils.GetCurrentPlatform(),
                    permittedSources: this.PermittedSources,
                    customEmbeddedSubDirectory: customStreamingSubdirectory.Value,
                    cancellationTokenSource: this.CancellationTokenSource,
                    manifest: this.AssetBundleManifest),
                partitionComponent);

            world.Create(promise, emote, this.BodyShape);
        }
    }
}
