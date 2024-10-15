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
    DCL.AvatarRendering.Emotes.GetSceneEmoteFromLocalDevelopmentSceneIntention>;

namespace CrdtEcsBridge.RestrictedActions
{
    public class GlobalWorldActions : IGlobalWorldActions
    {
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly IEmotesMessageBus messageBus;
        private readonly bool localSceneDevelopment;

        public bool LocalSceneDevelopment => localSceneDevelopment;

        public GlobalWorldActions(World world, Entity playerEntity, IEmotesMessageBus messageBus, bool localSceneDevelopment)
        {
            this.world = world;
            this.playerEntity = playerEntity;
            this.messageBus = messageBus;
            this.localSceneDevelopment = localSceneDevelopment;
        }

        public void MoveAndRotatePlayer(Vector3 newPlayerPosition, Vector3? newCameraTarget)
        {
            // Move player to new position (through InterpolateCharacterSystem -> TeleportPlayerQuery)
            world.AddOrSet(playerEntity, new PlayerTeleportIntent(newPlayerPosition, Vector2Int.zero));

            // Rotate player to look at camera target (through RotateCharacterSystem -> ForceLookAtQuery)
            if (newCameraTarget != null)
                world.AddOrSet(playerEntity, new PlayerLookAtIntent(newCameraTarget.Value));
        }

        public void RotateCamera(Vector3? newCameraTarget, Vector3 newPlayerPosition)
        {
            if (newCameraTarget == null || world == null)
                return;

            // Rotate camera to look at new target (through ApplyCinemachineCameraInputSystem -> ForceLookAtQuery)
            SingleInstanceEntity camera = world.CacheCamera();
            world.AddOrSet(camera, new CameraLookAtIntent(newCameraTarget.Value, newPlayerPosition));
        }

        private async UniTask TriggerSceneEmoteFromAssetBundleAsync(string sceneId, SceneAssetBundleManifest abManifest, string emoteHash, bool loop, CancellationToken ct)
        {
            if (!world.TryGet(playerEntity, out AvatarShapeComponent avatarShape))
                throw new Exception("Cannot resolve body shape of current player because its missing AvatarShapeComponent");

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
                new GetSceneEmoteFromLocalDevelopmentSceneIntention(sceneData, emotePath, emoteHash,
                    avatarShape.BodyShape, loop),
                PartitionComponent.TOP_PRIORITY);

            promise = await promise.ToUniTaskAsync(world, cancellationToken: ct);
            var consumed = promise.Result!.Value.Asset.ConsumeEmotes();
            var value = consumed.Value[0]!;
            URN urn = value.GetUrn();

            TriggerEmote(urn, loop);
        }

        public async UniTask TriggerSceneEmoteAsync(ISceneData sceneData, string src, string hash, bool loop, CancellationToken ct)
        {
            if (localSceneDevelopment)
                await TriggerSceneEmoteFromLocalSceneAsync(sceneData,src,hash, loop, ct);
            else
                await TriggerSceneEmoteFromAssetBundleAsync(
                    sceneData.SceneEntityDefinition.id ?? sceneData.SceneEntityDefinition.metadata.scene.DecodedBase.ToString(),
                    sceneData.AssetBundleManifest, hash, loop, ct);
        }

        public void TriggerEmote(URN urn, bool isLooping)
        {
            world.Add(playerEntity, new CharacterEmoteIntent { EmoteId = urn, Spatial = true, TriggerSource = TriggerSource.SCENE });
            messageBus.Send(urn, isLooping);
        }
    }
}
