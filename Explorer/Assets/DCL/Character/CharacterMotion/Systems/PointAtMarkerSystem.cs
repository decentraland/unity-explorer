using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Assets;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.Profiles;
using DCL.Profiles.Helpers;
using DCL.Utilities;
using DCL.Web3.Identities;
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
        private const float MAX_POINT_AT_DISTANCE_SQR = 100f * 100f; // Max distance to show the marker (100 units)

        private readonly IObjectPool<PointAtMarkerHolder> markerPool;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly ObjectProxy<FriendsCache> friendsCache;

        private SingleInstanceEntity camera;

        internal PointAtMarkerSystem(
            World world,
            IObjectPool<PointAtMarkerHolder> markerPool,
            IWeb3IdentityCache web3IdentityCache,
            ObjectProxy<FriendsCache> friendsCache
        ) : base(world)
        {
            this.markerPool = markerPool;
            this.web3IdentityCache = web3IdentityCache;
            this.friendsCache = friendsCache;
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
            in Entity entity,
            in HandPointAtComponent pointAt,
            in Profile profile,
            in AvatarBase avatarBase)
        {
            // User must be pointing and either be the local player or a friend to show the marker
            if (!pointAt.IsPointing || (profile.UserId != web3IdentityCache.Identity?.Address && (!friendsCache.Configured || !friendsCache.StrictObject.Contains(profile.UserId))))
                return;

            float distanceSqr = (pointAt.WorldHitPoint - avatarBase.transform.position).sqrMagnitude;
            if (distanceSqr > MAX_POINT_AT_DISTANCE_SQR)
                return;

            PointAtMarkerHolder marker = markerPool.Get();

            Sprite sprite = profile.ProfilePicture?.Asset is { } spriteData
                ? spriteData.Sprite
                : ProfileUtils.DEFAULT_PROFILE_PIC.Sprite;

            marker.Setup(sprite, profile.UserId, distanceSqr);
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
            in AvatarBase avatarBase,
            ref PointAtMarkerHolder marker)
        {
            if (!pointAt.IsPointing)
                return;

            Sprite sprite = profile.ProfilePicture?.Asset is { } spriteData
                ? spriteData.Sprite
                : ProfileUtils.DEFAULT_PROFILE_PIC.Sprite;

            marker.Setup(sprite, profile.UserId, (pointAt.WorldHitPoint - avatarBase.transform.position).sqrMagnitude);
            marker.transform.position = pointAt.WorldHitPoint;
            marker.transform.LookAt(
                pointAt.WorldHitPoint + (Vector3)cameraForward, cameraUp);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ReleaseMarker(
            in Entity entity,
            in HandPointAtComponent pointAt,
            in AvatarBase avatarBase,
            ref PointAtMarkerHolder marker)
        {
            if (pointAt.IsPointing && (pointAt.WorldHitPoint - avatarBase.transform.position).sqrMagnitude <= MAX_POINT_AT_DISTANCE_SQR)
                return;

            markerPool.Release(marker);
            World.Remove<PointAtMarkerHolder>(entity);
        }
    }
}
