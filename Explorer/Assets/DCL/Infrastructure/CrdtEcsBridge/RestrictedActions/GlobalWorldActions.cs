using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Loading.Components;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Emotes;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;
using Utility.Arch;
using SceneEmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetSceneEmoteFromRealmIntention>;
using LocalSceneEmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetSceneEmoteFromLocalSceneIntention>;

namespace CrdtEcsBridge.RestrictedActions
{
    public class GlobalWorldActions : IGlobalWorldActions
    {
        private const string SCENE_EMOTE_NAMING = "_emote.glb";

        private readonly World world;
        private readonly Entity playerEntity;
        private readonly IEmotesMessageBus messageBus;
        private readonly bool localSceneDevelopment;
        private readonly bool useRemoteAssetBundles;

        public GlobalWorldActions(World world, Entity playerEntity, IEmotesMessageBus messageBus, bool localSceneDevelopment, bool useRemoteAssetBundles)
        {
            this.world = world;
            this.playerEntity = playerEntity;
            this.messageBus = messageBus;
            this.localSceneDevelopment = localSceneDevelopment;
            this.useRemoteAssetBundles = useRemoteAssetBundles;
        }

        public void MoveAndRotatePlayer(Vector3 newPlayerPosition, Vector3? newCameraTarget, Vector3? newAvatarTarget)
        {
            // Move player to new position (through TeleportCharacterSystem -> TeleportPlayerQuery)
            world.AddOrSet(playerEntity, new PlayerTeleportIntent(null, Vector2Int.zero, newPlayerPosition, CancellationToken.None, isPositionSet: true));

            // Update avatar rotation (through RotateCharacterSystem -> ForceLookAtQuery)
            if (newAvatarTarget != null)
            {
                Vector3 lookAtDirection = newAvatarTarget.Value - newPlayerPosition;
                lookAtDirection.y = 0;
                world.AddOrSet(playerEntity, new PlayerLookAtIntent(newPlayerPosition + lookAtDirection.normalized));
            }
            else if (newCameraTarget != null)
            {
                world.AddOrSet(playerEntity, new PlayerLookAtIntent(newCameraTarget.Value));
            }
        }

        public void RotateCamera(Vector3? newCameraTarget, Vector3 newPlayerPosition)
        {
            if (newCameraTarget == null)
                return;

            // Rotate camera to look at new target (through ApplyCinemachineCameraInputSystem -> ForceLookAtQuery)
            SingleInstanceEntity camera = world.CacheCamera();
            world.AddOrSet(camera, new CameraLookAtIntent(newCameraTarget.Value, newPlayerPosition));
        }

        public void TriggerEmote(URN urn, bool isLooping)
        {
            if (world.TryGet(playerEntity, out AvatarShapeComponent avatarShape) && !avatarShape.IsVisible) return;

            world.Add(playerEntity, new CharacterEmoteIntent { EmoteId = urn, Spatial = true, TriggerSource = TriggerSource.SCENE });
            messageBus.Send(urn, isLooping);
        }

        public async UniTask TriggerSceneEmoteAsync(ISceneData sceneData, string src, string hash, bool loop, CancellationToken ct)
        {
            if (localSceneDevelopment && !useRemoteAssetBundles)
            {
                // For consistent behavior, we only play local scene emotes if they have the same requirements we impose on the Asset
                // Bundle Converter, otherwise creators may end up seeing scene emotes playing locally that won't play in deployed scenes
                if (src.ToLower().EndsWith(SCENE_EMOTE_NAMING))
                    await TriggerSceneEmoteFromLocalSceneAsync(sceneData, src, hash, loop, ct);
            }
            else
            {
                await TriggerSceneEmoteFromRealmAsync(
                    sceneData.SceneEntityDefinition.id ?? sceneData.SceneEntityDefinition.metadata.scene.DecodedBase.ToString(),
                    sceneData.AssetBundleManifest, hash, loop, ct);
            }
        }

        private async UniTask TriggerSceneEmoteFromRealmAsync(string sceneId, SceneAssetBundleManifest abManifest, string emoteHash, bool loop, CancellationToken ct)
        {
            if (!world.TryGet(playerEntity, out AvatarShapeComponent avatarShape))
                throw new Exception("Cannot resolve body shape of current player because its missing AvatarShapeComponent");

            if (!avatarShape.IsVisible) return;

            var promise = SceneEmotePromise.Create(world,
                new GetSceneEmoteFromRealmIntention(sceneId, abManifest, emoteHash, loop, avatarShape.BodyShape),
                PartitionComponent.TOP_PRIORITY);

            promise = await promise.ToUniTaskAsync(world, cancellationToken: ct);

            using var consumed = promise.Result!.Value.Asset.ConsumeEmotes();
            var value = consumed.Value[0]!;
            URN urn = value.GetUrn();
            bool isLooping = value.IsLooping();

            TriggerEmote(urn, isLooping);
        }

        private async UniTask TriggerSceneEmoteFromLocalSceneAsync(ISceneData sceneData, string emotePath, string emoteHash, bool loop, CancellationToken ct)
        {
            if (!world.TryGet(playerEntity, out AvatarShapeComponent avatarShape))
                throw new Exception("Cannot resolve body shape of current player because its missing AvatarShapeComponent");

            var promise = LocalSceneEmotePromise.Create(world,
                new GetSceneEmoteFromLocalSceneIntention(sceneData, emotePath, emoteHash,
                    avatarShape.BodyShape, loop),
                PartitionComponent.TOP_PRIORITY);

            promise = await promise.ToUniTaskAsync(world, cancellationToken: ct);
            var consumed = promise.Result!.Value.Asset.ConsumeEmotes();
            var value = consumed.Value[0]!;
            URN urn = value.GetUrn();

            TriggerEmote(urn, loop);
        }
    }
}
