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

namespace CrdtEcsBridge.RestrictedActions
{
    public class GlobalWorldActions : IGlobalWorldActions
    {
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly IEmotesMessageBus messageBus;

        public GlobalWorldActions(World world, Entity playerEntity, IEmotesMessageBus messageBus)
        {
            this.world = world;
            this.playerEntity = playerEntity;
            this.messageBus = messageBus;
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

        public async UniTask TriggerSceneEmoteAsync(string sceneId, SceneAssetBundleManifest abManifest, string emoteHash, bool loop, CancellationToken ct)
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

        public void TriggerEmote(URN urn, bool isLooping)
        {
            world.Add(playerEntity, new CharacterEmoteIntent { EmoteId = urn, Spatial = true, TriggerSource = TriggerSource.SCENE });
            messageBus.Send(urn, isLooping);
        }
    }
}
