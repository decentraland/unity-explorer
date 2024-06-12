using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Emotes;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;
using SceneEmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetSceneEmoteFromRealmIntention>;

namespace CrdtEcsBridge.RestrictedActions
{
    public class GlobalWorldActions : IGlobalWorldActions
    {
        private readonly World world;
        private readonly Entity playerEntity;

        public GlobalWorldActions(
            World world,
            Entity playerEntity)
        {
            this.world = world;
            this.playerEntity = playerEntity;
        }

        public void MoveAndRotatePlayer(Vector3 newPlayerPosition, Vector3? newCameraTarget)
        {
            // Move player to new position (through InterpolateCharacterSystem -> TeleportPlayerQuery)
            world.Add(playerEntity, new PlayerTeleportIntent(newPlayerPosition, Vector2Int.zero));

            // Rotate player to look at camera target (through RotateCharacterSystem -> ForceLookAtQuery)
            if (newCameraTarget != null)
                world.Add(playerEntity, new PlayerLookAtIntent(newCameraTarget.Value));
        }

        public void RotateCamera(Vector3? newCameraTarget, Vector3 newPlayerPosition)
        {
            if (newCameraTarget == null || world == null)
                return;

            // Rotate camera to look at new target (through ApplyCinemachineCameraInputSystem -> ForceLookAtQuery)
            var camera = world.CacheCamera();
            world.Add(camera, new CameraLookAtIntent(newCameraTarget.Value, newPlayerPosition));
        }

        public async UniTask TriggerSceneEmoteAsync(SceneAssetBundleManifest abManifest, string hash, bool loop, CancellationToken ct)
        {
            if (!world.TryGet(playerEntity, out AvatarShapeComponent avatarShape))
                throw new Exception("Cannot resolve body shape of current player because its missing AvatarShapeComponent");

            var promise = SceneEmotePromise.Create(world,
               new GetSceneEmoteFromRealmIntention(abManifest, hash, loop, avatarShape.BodyShape),
               PartitionComponent.TOP_PRIORITY);

            promise = await promise.ToUniTaskAsync(world, cancellationToken: ct);

            URN urn = promise.Result!.Value.Asset.Emotes[0].GetUrn();

            world.Add(playerEntity, new CharacterEmoteIntent { EmoteId = urn, Spatial = true });
        }
    }
}
