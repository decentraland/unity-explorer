using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character;
using DCL.Character.Components;
using DCL.CharacterCamera;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.VoiceChat.Nearby.Systems
{
    /// <summary>
    ///     Reads position from the avatar entity referenced by <see cref="NearbyAudioSourceComponent.AvatarEntity"/>
    ///     and drives the <see cref="LivekitAudioSource"/> transform + spatial angles each frame.
    ///     Per-frame mute enforcement: <see cref="NearbyMuteService.IsMuted"/> is queried for every audio entity
    ///     and written to <see cref="AudioSource.mute"/>. This is self-healing on toggle (no event plumbing) and
    ///     subsumes the old "first-sync unmute" hack — the binding system starts with <c>mute = true</c> so the
    ///     first successful tick recomputes it (avoids the world-origin burst between <c>Play()</c> and sync).
    ///     Carries no lifecycle responsibility — structural changes for audio entities are owned by
    ///     <see cref="NearbyAudioCleanupSystem"/>.
    /// </summary>
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(NearbyAudioBindingSystem))]
    public partial class NearbyAudioPositionSystem : BaseUnityLoopSystem
    {
        private readonly NearbyMuteService muteService;

        private SingleInstanceEntity cameraEntity;
        private SingleInstanceEntity playerEntity;
        private bool isFirstPerson;

        internal NearbyAudioPositionSystem(World world, NearbyMuteService muteService) : base(world)
        {
            this.muteService = muteService;
        }

        public override void Initialize()
        {
            cameraEntity = World.CacheCamera();
            playerEntity = World.CachePlayer();
        }

        protected override void Update(float t)
        {
            // The AudioListener is on the camera, but spatial gain should be relative to the player's head.
            // In ThirdPerson these positions differ, so we track both to reproject remote sources before applying spatial audio.
            (Transform listenerTransform, Vector3 playerHeadPos) = GetListenerAndHeadPositions();
            SyncPositionsAndSpatialAnglesQuery(World, listenerTransform, playerHeadPos);
        }

        private (Transform listenerTransform, Vector3 playerHeadPos) GetListenerAndHeadPositions()
        {
            ref readonly CameraComponent cam = ref cameraEntity.GetCameraComponent(World);
            isFirstPerson = cam.Mode == CameraMode.FirstPerson;

            Vector3 headPos = cam.Camera.transform.position;
            if (!isFirstPerson && World.TryGet(playerEntity, out PlayerComponent playerComp))
                headPos = playerComp.CameraFocus.position;

            return (cam.Camera.transform, headPos);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void SyncPositionsAndSpatialAngles([Data] Transform listenerTransform, [Data] Vector3 playerHeadPos, ref NearbyAudioSourceComponent nearbyAudio)
        {
            // Stale avatar entity reference — NearbyAudioCleanupSystem will tear this audio entity down in CleanUpGroup.
            if (!World.TryGet(nearbyAudio.AvatarEntity, out AvatarBase? avatarBase)) return;

            Vector3 remoteAvatarHeadPos = avatarBase!.HeadAnchorPoint.position;

            // reprojection, so gain is calculated relative to the head and not the camera position (audioListener is on the camera)
            Vector3 sourcePos = isFirstPerson ? remoteAvatarHeadPos : listenerTransform.position + (remoteAvatarHeadPos - playerHeadPos);
            nearbyAudio.LivekitAudioSource.transform.position = sourcePos;

            (float azimuth, float elevation) = CalculateSpatialAngles(listenerTransform, sourcePos);
            nearbyAudio.LivekitAudioSource.SetSpatialAngles(azimuth, elevation);

            // Per-frame mute enforcement — self-healing on toggle, also unmutes the binding-time start-mute on first successful tick (when IsMuted is false).
            nearbyAudio.LivekitAudioSource.AudioSource.mute = muteService.IsMuted(nearbyAudio.Key.identity);
        }

        private static (float azimuth, float elevation) CalculateSpatialAngles(Transform listenerTransform, Vector3 sourcePosition)
        {
            Vector3 local = listenerTransform.InverseTransformPoint(sourcePosition);

            float horizontalDist = math.sqrt((local.x * local.x) + (local.z * local.z));
            float elevation = math.atan2(local.y, horizontalDist);

            float azimuth = math.atan2(local.x, local.z);

            return (azimuth, elevation);
        }
    }
}
