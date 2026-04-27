using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.Profiles;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using LiveKit.Rooms.Streaming.Audio;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.VoiceChat.Nearby.Systems
{
    /// <summary>
    ///     Reads position from the avatar entity referenced by <see cref="NearbyAudioSourceComponent.AvatarEntity"/> and
    ///     drives the <see cref="LivekitAudioSource"/> transform + spatial angles each frame.
    ///     Defensively flags audio-source entities for cleanup when the linked avatar lost its <see cref="AvatarBase"/>
    ///     or its <see cref="Profile.UserId"/> drifted away from the bound <see cref="StreamKey"/>.
    /// </summary>
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(NearbyAudioBindingSystem))]
    public partial class NearbyAudioPositionSystem : BaseUnityLoopSystem
    {
        private readonly List<Entity> entitiesToCleanUp = new (4);

        private SingleInstanceEntity cameraEntity;
        private SingleInstanceEntity playerEntity;
        private bool isFirstPerson;

        internal NearbyAudioPositionSystem(World world) : base(world) { }

        public override void Initialize()
        {
            cameraEntity = World.CacheCamera();
            playerEntity = World.CachePlayer();
        }

        protected override void Update(float t)
        {
            entitiesToCleanUp.Clear();

            // The AudioListener is on the camera, but spatial gain should be relative to the player's head.
            // In ThirdPerson these positions differ, so we track both to reproject remote sources before applying spatial audio.
            (Transform listenerTransform, Vector3 playerHeadPos) = GetListenerAndHeadPositions();
            SyncPositionsAndSpatialAnglesQuery(World, listenerTransform, playerHeadPos);

            // Structural changes only after all ref/in/out reads have released — see CLAUDE.md §5.
            foreach (Entity entity in entitiesToCleanUp)
                World.Destroy(entity);
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
        private void SyncPositionsAndSpatialAngles([Data] Transform listenerTransform, [Data] Vector3 playerHeadPos,
            Entity audioEntity, ref NearbyAudioSourceComponent nearbyAudio)
        {
            if (nearbyAudio.LivekitAudioSource == null)
            {
                entitiesToCleanUp.Add(audioEntity);
                return;
            }

            Entity avatarEntity = nearbyAudio.AvatarEntity;

            if (!World.IsAlive(avatarEntity)
                || World.Has<DeleteEntityIntention>(avatarEntity)
                || !World.TryGet(avatarEntity, out Profile? profile)
                || profile == null
                || profile.UserId != nearbyAudio.Key.identity
                || !World.TryGet(avatarEntity, out AvatarBase? avatarBase)
                || avatarBase == null)
            {
                entitiesToCleanUp.Add(audioEntity);
                return;
            }

            Vector3 remoteAvatarHeadPos = avatarBase.HeadAnchorPoint.position;

            // reprojection, so gain is calculated relative to the head and not the camera position (audioListener is on the camera)
            Vector3 sourcePos = isFirstPerson ? remoteAvatarHeadPos : listenerTransform.position + (remoteAvatarHeadPos - playerHeadPos);
            nearbyAudio.LivekitAudioSource.transform.position = sourcePos;

            (float azimuth, float elevation) = CalculateSpatialAngles(listenerTransform, sourcePos);
            nearbyAudio.LivekitAudioSource.SetSpatialAngles(azimuth, elevation);

            // First successful position sync — release the start-mute set in NearbyAudioBindingSystem.
            if (nearbyAudio.LivekitAudioSource.AudioSource.mute)
                nearbyAudio.LivekitAudioSource.AudioSource.mute = false;
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
