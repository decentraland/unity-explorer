using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Loading.Components;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Ipfs;
using DCL.Multiplayer.Emotes;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;
using Utility.Arch;
using Utility.Multithreading;
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
        private readonly bool isBuilderCollectionPreview;

        public GlobalWorldActions(World world, Entity playerEntity, IEmotesMessageBus messageBus, bool localSceneDevelopment, bool useRemoteAssetBundles, bool isBuilderCollectionPreview)
        {
            this.world = world;
            this.playerEntity = playerEntity;
            this.messageBus = messageBus;
            this.localSceneDevelopment = localSceneDevelopment;
            this.useRemoteAssetBundles = useRemoteAssetBundles;
            this.isBuilderCollectionPreview = isBuilderCollectionPreview;
        }

        public async UniTask<bool> MoveAndRotatePlayerAsync(Vector3 newPlayerPosition, Vector3? newCameraTarget, Vector3? newAvatarTarget, float duration, CancellationToken ct)
        {
            if (duration > 0f)
            {
                // Smooth movement over duration (through MovePlayerWithDurationSystem)
                Vector3 startPosition = world.Get<CharacterTransform>(playerEntity).Position;

                var completionSource = new UniTaskCompletionSource<bool>();
                world.AddOrSet(playerEntity, new PlayerMoveToWithDurationIntent(
                    startPosition,
                    newPlayerPosition,
                    newCameraTarget,
                    newAvatarTarget,
                    completionSource,
                    duration));

                return await completionSource.Task.AttachExternalCancellation(ct);
            }

            // Instant teleport (through TeleportCharacterSystem -> TeleportPlayerQuery)
            world.AddOrSet(playerEntity, new PlayerTeleportIntent(null, Vector2Int.zero, newPlayerPosition, CancellationToken.None, isPositionSet: true));
            // Fixes https://github.com/decentraland/unity-explorer/issues/6246
            // We need to add a delay before we can start transitioning in the animator, or we might encounter artifacts
            world.AddOrSet(playerEntity, new DisableAnimationTransitionOnTeleport(Time.frameCount + 20));

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

            // Instant teleport is always successful
            return true;
        }

        public void RotateCamera(Vector3? newCameraTarget, Vector3 newPlayerPosition)
        {
            if (newCameraTarget == null)
                return;

            // Rotate camera to look at new target (through ApplyCinemachineCameraInputSystem -> ForceLookAtQuery)
            SingleInstanceEntity camera = world.CacheCamera();
            world.AddOrSet(camera, new CameraLookAtIntent(newCameraTarget.Value, newPlayerPosition));
        }

        public void TriggerEmote(URN urn, bool isLooping, AvatarEmoteMask mask)
        {
            if (world.TryGet(playerEntity, out AvatarShapeComponent avatarShape) && !avatarShape.IsVisible) return;

            // If it's just Add() there are inconsistencies when the intent is processed at CharacterEmoteSystem for rapidly triggered emotes...
            world.AddOrSet(playerEntity, new CharacterEmoteIntent { EmoteId = urn, Spatial = true, TriggerSource = TriggerSource.SCENE, Mask = mask });
            messageBus.Send(urn, isLooping, mask);
        }

        public async UniTask TriggerSceneEmoteAsync(ISceneData sceneData, string src, string hash, bool loop, AvatarEmoteMask mask, CancellationToken ct, Action<URN, bool, AvatarEmoteMask>? onEmoteResolved = null)
        {
            world.AddOrSet(playerEntity, new CharacterWaitingSceneEmoteLoading(MultithreadingUtility.FrameCount));

            bool loadFromLocalScene = (localSceneDevelopment && !useRemoteAssetBundles) ||
                                      (isBuilderCollectionPreview && sceneData.IsWearableBuilderCollectionPreview);

            if (loadFromLocalScene)
            {
                if (src.ToLower().EndsWith(SCENE_EMOTE_NAMING))
                    await TriggerSceneEmoteFromLocalSceneAsync(sceneData, src, hash, loop, mask, ct, onEmoteResolved);
                else
                    ReportHub.LogError(ReportCategory.EMOTE, $"'{src}' scene emote cannot be played. It must follow the naming convention ending in '{SCENE_EMOTE_NAMING}'");
            }
            else
            {
                await TriggerSceneEmoteFromRealmAsync(
                    sceneData.SceneEntityDefinition.id ?? sceneData.SceneEntityDefinition.metadata.scene.DecodedBase.ToString(),
                    sceneData.SceneEntityDefinition.assetBundleManifestVersion,
                    hash, loop, mask, ct, onEmoteResolved);
            }

            world.Remove<CharacterWaitingSceneEmoteLoading>(playerEntity);
        }

        public void StopEmote()
        {
            // If the avatar is not visible, there is nothing to stop (matches TriggerEmote guard).
            if (world.TryGet(playerEntity, out AvatarShapeComponent avatarShape) && !avatarShape.IsVisible)
                return;

            // Stop full-body emote (global world only, masked emotes are handled by scene-side systems)
            if (world.TryGet(playerEntity, out CharacterEmoteComponent emoteComponent))
            {
                emoteComponent.StopEmote = true;
                world.Set(playerEntity, emoteComponent);
            }

            messageBus.SendStop();
        }

        private async UniTask TriggerSceneEmoteFromRealmAsync(string sceneId, AssetBundleManifestVersion sceneAssetBundleManifestVersion, string emoteHash, bool loop, AvatarEmoteMask mask, CancellationToken ct, Action<URN, bool, AvatarEmoteMask>? onEmoteResolved = null)
        {
            if (!world.TryGet(playerEntity, out AvatarShapeComponent avatarShape))
                throw new Exception("Cannot resolve body shape of current player because its missing AvatarShapeComponent");

            if (!avatarShape.IsVisible) return;

            var promise = SceneEmotePromise.Create(world,
                new GetSceneEmoteFromRealmIntention(sceneId, sceneAssetBundleManifestVersion, emoteHash, loop, avatarShape.BodyShape),
                PartitionComponent.TOP_PRIORITY);

            promise = await promise.ToUniTaskAsync(world, cancellationToken: ct);

            if (promise.Result is {Succeeded: true})
            {
                using var consumed = promise.Result!.Value.Asset.ConsumeEmotes();
                var value = consumed.Value[0];
                URN urn = value.GetUrn();
                bool isLooping = value.IsLooping();

                if (onEmoteResolved != null)
                    onEmoteResolved(urn, isLooping, mask);
                else
                    TriggerEmote(urn, isLooping, mask);
            }
        }

        private async UniTask TriggerSceneEmoteFromLocalSceneAsync(ISceneData sceneData, string emotePath, string emoteHash, bool loop, AvatarEmoteMask mask, CancellationToken ct, Action<URN, bool, AvatarEmoteMask>? onEmoteResolved = null)
        {
            if (!world.TryGet(playerEntity, out AvatarShapeComponent avatarShape))
                throw new Exception("Cannot resolve body shape of current player because its missing AvatarShapeComponent");

            var promise = LocalSceneEmotePromise.Create(world,
                new GetSceneEmoteFromLocalSceneIntention(sceneData, emotePath, emoteHash,
                    avatarShape.BodyShape, loop),
                PartitionComponent.TOP_PRIORITY);

            promise = await promise.ToUniTaskAsync(world, cancellationToken: ct);

            if (promise.Result is {Succeeded: true})
            {
                using ConsumedList<IEmote> consumed = promise.Result!.Value.Asset.ConsumeEmotes();
                var value = consumed.Value[0]!;
                URN urn = value.GetUrn();

                if (onEmoteResolved != null)
                    onEmoteResolved(urn, loop, mask);
                else
                    TriggerEmote(urn, loop, mask);
            }
        }
    }
}
