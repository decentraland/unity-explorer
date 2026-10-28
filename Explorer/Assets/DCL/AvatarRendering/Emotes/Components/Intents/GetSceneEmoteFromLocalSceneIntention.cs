using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.ECSComponents;
using DCL.Ipfs;
using ECS.Prioritization.Components;
using ECS.StreamableLoading;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.GLTF;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;

using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;
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

        /// <summary>
        ///     When set, the emote is loaded as an asset bundle from the local asset-bundle server under this
        ///     scene manifest, and falls back to the raw GLB if the bundle fails. When null, the raw GLB is loaded.
        /// </summary>
        public AssetBundleManifestVersion? LocalBundleManifest { get; set; }

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
            if (LocalBundleManifest == null)
            {
                CreateRawGltfPromise(world, EmotePath, EmoteHash, partitionComponent, emote, BodyShape);
                return;
            }

            // Scene emotes are scene content — resolve to the digest-bearing name so URL and cache identity match the scene's ABs.
            var promise = AssetBundlePromise.Create(world,
                GetAssetBundleIntention.FromHash(
                    LocalBundleManifest.GetCdnRequestHash(EmoteHash),
                    LocalBundleManifest,
                    typeof(GameObject),
                    parentEntityID: SceneData.SceneEntityDefinition.id ?? string.Empty,
                    customEmbeddedSubDirectory: customStreamingSubdirectory ?? default,
                    cancellationTokenSource: CancellationTokenSource),
                partitionComponent);

            world.Create(promise, emote, BodyShape, new SceneEmoteRawGltfFallback(EmotePath, EmoteHash, partitionComponent));
        }

        public static void CreateRawGltfPromise(World world, string emotePath, string emoteHash, IPartitionComponent partitionComponent, IEmote emote, BodyShape bodyShape)
        {
            // Local scene emotes always load as Legacy: gltFast cannot generate Mecanim clips at runtime
            var promise = GltfPromise.Create(world,
                GetGLTFIntention.Create(emotePath, emoteHash, mecanimAnimationClips: false),
                partitionComponent);

            world.Create(promise, emote, bodyShape);
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
