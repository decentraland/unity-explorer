using Arch.Core;
using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.Ipfs;
using ECS.Prioritization.Components;
using ECS.StreamableLoading;
using ECS.StreamableLoading.AssetBundles;
using System;
using System.Threading;
using UnityEngine;

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
            AssetSource permittedSources = AssetSource.All,
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

        public void CreateAndAddPromiseToWorld(World world, IPartitionComponent partitionComponent, URLSubdirectory? customStreamingSubdirectory, bool useLocalAssetBundles, IEmote emote)
        {
            AssetBundlePromise promise = CreateBundlePromise(world, SceneAssetBundleManifestVersion, EmoteHash, SceneId, customStreamingSubdirectory.Value,
                CancellationTokenSource, partitionComponent, PermittedSources);

            world.Create(promise, emote, this.BodyShape);
        }

        internal static AssetBundlePromise CreateBundlePromise(World world, AssetBundleManifestVersion manifest, string emoteHash, string sceneId,
            URLSubdirectory customStreamingSubdirectory, CancellationTokenSource cancellationTokenSource, IPartitionComponent partitionComponent,
            AssetSource permittedSources = AssetSource.All) =>

            // Scene emotes are scene content — resolve to the digest-bearing name so URL and cache identity match the scene's ABs.
            AssetBundlePromise.Create(world,
                GetAssetBundleIntention.FromHash(
                    manifest.GetCdnRequestHash(emoteHash),
                    manifest,
                    typeof(GameObject),
                    parentEntityID: sceneId,
                    permittedSources: permittedSources,
                    customEmbeddedSubDirectory: customStreamingSubdirectory,
                    cancellationTokenSource: cancellationTokenSource),
                partitionComponent);

        public bool IsTimeout(float deltaTime)
        {
            // Timeout access returns a temporary value. We need to reassign the field or we lose the changes
            Timeout = new LoadTimeout(Timeout.Timeout, Timeout.ElapsedTime + deltaTime);
            bool result = Timeout.IsTimeout;
            return result;
        }
    }
}
