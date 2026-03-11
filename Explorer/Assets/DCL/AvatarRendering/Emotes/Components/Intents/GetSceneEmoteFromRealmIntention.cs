using Arch.Core;
using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.Ipfs;
using DCL.Utility;
using ECS.Prioritization.Components;
using ECS.StreamableLoading;
using ECS.StreamableLoading.AssetBundles;
using System;
using System.Threading;
using UnityEngine;
using Utility;

using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.AvatarRendering.Emotes
{
    public struct GetSceneEmoteFromRealmIntention : IEquatable<GetSceneEmoteFromRealmIntention>, IEmoteAssetIntention
    {
        public static readonly string SCENE_EMOTE_PREFIX = "urn:decentraland:off-chain:scene-emote";

        public CancellationTokenSource CancellationTokenSource { get; }
        public string SceneId { get; }
        public string EmoteHash { get; }
        public bool Loop { get; }
        public AssetSource PermittedSources { get; }
        public BodyShape BodyShape { get; }

        public AssetBundleManifestVersion SceneAssetBundleManifestVersion;

        public LoadTimeout Timeout { get; private set; }

        public GetSceneEmoteFromRealmIntention(
            string sceneId,
            AssetBundleManifestVersion sceneAssetBundleManifestVersion,
            string emoteHash,
            bool loop,
            BodyShape bodyShape,
            AssetSource permittedSources = AssetSource.ALL,
            int timeout = StreamableLoadingDefaults.TIMEOUT
        ) : this()
        {
            SceneId = sceneId;
            EmoteHash = emoteHash;
            Loop = loop;
            CancellationTokenSource = new CancellationTokenSource();
            PermittedSources = permittedSources;
            BodyShape = bodyShape;
            Timeout = new LoadTimeout(timeout, 0);
            SceneAssetBundleManifestVersion = sceneAssetBundleManifestVersion;
        }

        public bool Equals(GetSceneEmoteFromRealmIntention other) =>
            EmoteHash == other.EmoteHash && Loop == other.Loop && BodyShape.Equals(other.BodyShape);

        public readonly URN NewSceneEmoteURN() =>
            $"{SCENE_EMOTE_PREFIX}:{SceneId}-{EmoteHash}-{Loop.ToString().ToLower()}";

        public void CreateAndAddPromiseToWorld(World world, IPartitionComponent partitionComponent, URLSubdirectory? customStreamingSubdirectory, IEmote emote)
        {
            var promise = AssetBundlePromise.Create(world,
                GetAssetBundleIntention.FromHash(
                    this.EmoteHash + PlatformUtils.GetCurrentPlatform(),
                    typeof(GameObject),
                    assetBundleManifestVersion: SceneAssetBundleManifestVersion,
                    parentEntityID: SceneId,
                    permittedSources: this.PermittedSources,
                    customEmbeddedSubDirectory: customStreamingSubdirectory.Value,
                    cancellationTokenSource: this.CancellationTokenSource),
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
