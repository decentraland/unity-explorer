using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Assets;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Profiles.Helpers;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.Character.CharacterMotion.Systems
{
    [LogCategory(ReportCategory.MOTION)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(HandPointAtSystem))]
    public partial class PointAtMarkerSystem : BaseUnityLoopSystem
    {
        private readonly IObjectPool<PointAtMarkerHolder> markerPool;

        private SingleInstanceEntity camera;

        internal PointAtMarkerSystem(
            World world,
            IObjectPool<PointAtMarkerHolder> markerPool
        ) : base(world)
        {
            this.markerPool = markerPool;
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            CameraComponent cam = camera.GetCameraComponent(World);
            float3 cameraForward = cam.Camera.transform.forward;
            float3 cameraUp = cam.Camera.transform.up;

            SpawnMarkerQuery(World);
            UpdateMarkerQuery(World, cameraForward, cameraUp);
            ReleaseMarkerQuery(World);
        }

        [Query]
        [None(typeof(PointAtMarkerHolder), typeof(DeleteEntityIntention))]
        private void SpawnMarker(
            Entity entity,
            in HandPointAtComponent pointAt,
            in Profile profile)
        {
            if (!pointAt.IsPointing)
                return;

            PointAtMarkerHolder marker = markerPool.Get();

            Sprite sprite = profile.ProfilePicture?.Asset is { } spriteData
                ? spriteData.Sprite
                : ProfileUtils.DEFAULT_PROFILE_PIC.Sprite;

            marker.Setup(sprite, profile.UserId);
            marker.transform.position = pointAt.WorldHitPoint;

            World.Add(entity, marker);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateMarker(
            [Data] in float3 cameraForward,
            [Data] in float3 cameraUp,
            in HandPointAtComponent pointAt,
            in Profile profile,
            PointAtMarkerHolder marker)
        {
            if (!pointAt.IsPointing)
                return;

            Sprite sprite = profile.ProfilePicture?.Asset is { } spriteData
                ? spriteData.Sprite
                : ProfileUtils.DEFAULT_PROFILE_PIC.Sprite;

            marker.Setup(sprite, profile.UserId);
            marker.transform.position = pointAt.WorldHitPoint;
            marker.transform.LookAt(
                pointAt.WorldHitPoint + (Vector3)cameraForward, cameraUp);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ReleaseMarker(
            Entity entity,
            in HandPointAtComponent pointAt,
            PointAtMarkerHolder marker)
        {
            if (pointAt.IsPointing)
                return;

            markerPool.Release(marker);
            World.Remove<PointAtMarkerHolder>(entity);
        }
    }
}
