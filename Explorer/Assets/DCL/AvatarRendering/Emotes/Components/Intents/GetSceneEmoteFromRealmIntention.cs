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

        public LoadTimeout Timeout { get; private set; }

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
            Timeout = new LoadTimeout(timeout, 0);
        }

        public bool Equals(GetSceneEmoteFromRealmIntention other) =>
            EmoteHash == other.EmoteHash && Loop == other.Loop && BodyShape.Equals(other.BodyShape);

        public readonly URN NewSceneEmoteURN() =>
            $"{SCENE_EMOTE_PREFIX}:{SceneId}-{EmoteHash}-{Loop.ToString().ToLower()}";

        public static bool TryParseFromURN(URN urn, out string sceneId, out string emoteHash, out bool loop)
        {
            string urnStr = urn.ToString();
            
            sceneId = string.Empty;
            emoteHash = string.Empty;
            loop = false;

            if (string.IsNullOrEmpty(urnStr))
                return false;

            string prefixWithColon = $"{SCENE_EMOTE_PREFIX}:";
            if (!urnStr.StartsWith(prefixWithColon, StringComparison.Ordinal))
                return false;

            string payload = urnStr.Substring(prefixWithColon.Length);

            // payload format: {sceneId}-{emoteHash}-{loop}
            int lastDash = payload.LastIndexOf('-');
            if (lastDash <= 0 || lastDash == payload.Length - 1)
                return false;

            string loopStr = payload.Substring(lastDash + 1);
            if (!bool.TryParse(loopStr, out loop))
                return false;

            string rest = payload.Substring(0, lastDash);

            int secondLastDash = rest.LastIndexOf('-');
            if (secondLastDash <= 0 || secondLastDash == rest.Length - 1)
                return false;

            emoteHash = rest.Substring(secondLastDash + 1);
            sceneId = rest.Substring(0, secondLastDash);

            return !string.IsNullOrEmpty(sceneId) && !string.IsNullOrEmpty(emoteHash);
        }

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

        public bool IsTimeout(float deltaTime)
        {
            // Timeout access returns a temporary value. We need to reassign the field or we lose the changes
            Timeout = new LoadTimeout(Timeout.Timeout, Timeout.ElapsedTime + deltaTime);
            bool result = Timeout.IsTimeout;
            return result;
        }
    }
}
