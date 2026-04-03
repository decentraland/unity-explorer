using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Audio.Avatar;
using DCL.AvatarRendering.AvatarShape.Assets;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.Profiles;
using DCL.Profiles.Helpers;
using DCL.Settings.Settings;
using DCL.Utilities;
using DCL.Web3.Identities;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using System;
using System.Collections.Generic;
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
        private readonly PointAtMarkerVisibilitySettings pointAtMarkerVisibilitySettings;
        private readonly Dictionary<string, Sprite> latestThumbnailCache = new (StringComparer.InvariantCultureIgnoreCase);

        private SingleInstanceEntity camera;

        private PointAtMarkerSystem(
            World world,
            IObjectPool<PointAtMarkerHolder> markerPool,
            IWeb3IdentityCache web3IdentityCache,
            ObjectProxy<FriendsCache> friendsCache,
            PointAtMarkerVisibilitySettings pointAtMarkerVisibilitySettings
        ) : base(world)
        {
            this.markerPool = markerPool;
            this.web3IdentityCache = web3IdentityCache;
            this.friendsCache = friendsCache;
            this.pointAtMarkerVisibilitySettings = pointAtMarkerVisibilitySettings;
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
            Vector3 cameraPosition = cam.Camera.transform.position;

            CleanupThumbnailCacheQuery(World);
            SpawnMarkerQuery(World);
            UpdateMarkerQuery(World, cameraForward, cameraUp, cameraPosition);
            FadeOutMarkerQuery(World);
            ReleaseFadedMarkerQuery(World);
        }

        [Query]
        [None(typeof(PointAtMarkerHolder), typeof(PointAtMarkerFadingOut), typeof(DeleteEntityIntention))]
        private void SpawnMarker(
            in Entity entity,
            in HandPointAtComponent pointAt,
            in Profile profile,
            in AvatarBase avatarBase)
        {
            bool isLocalPlayer = profile.UserId == web3IdentityCache.Identity?.Address;
            PointAtMarkerVisibilitySettings.VisibilitySetting visibilitySetting = pointAtMarkerVisibilitySettings.MarkerVisibilitySetting;

            if (!pointAt.IsPointing
                || string.IsNullOrEmpty(profile.UserId)
                || (visibilitySetting == PointAtMarkerVisibilitySettings.VisibilitySetting.NONE && !isLocalPlayer)
                || (visibilitySetting == PointAtMarkerVisibilitySettings.VisibilitySetting.FRIENDS_ONLY && !isLocalPlayer && (!friendsCache.Configured || !friendsCache.StrictObject.Contains(profile.UserId))))
                return;

            float distanceSqr = (pointAt.WorldHitPoint - avatarBase.transform.position).sqrMagnitude;
            if (distanceSqr > MAX_POINT_AT_DISTANCE_SQR)
                return;

            PointAtMarkerHolder marker = markerPool.Get();

            if (!isLocalPlayer)
                avatarBase.AudioPlaybackController?.PlayAudioForType(
                    AvatarAudioSettings.AvatarAudioClipType.PointAt);

            Sprite? sprite = profile.ProfilePicture?.Asset.Sprite;
            if (sprite != null)
                latestThumbnailCache[profile.UserId] = sprite;
            else if (!latestThumbnailCache.TryGetValue(profile.UserId, out sprite))
                sprite = null;

            marker.Initialize(sprite, profile.UserNameColor);
            marker.FadeIn();

            World.Add(entity, marker);
        }

        [Query]
        [None(typeof(PointAtMarkerFadingOut), typeof(DeleteEntityIntention))]
        private void UpdateMarker(
            [Data] in float3 cameraForward,
            [Data] in float3 cameraUp,
            [Data] in Vector3 cameraPosition,
            in HandPointAtComponent pointAt,
            ref PointAtMarkerHolder marker)
        {
            if (!pointAt.IsPointing)
                return;

            marker.UpdateData((pointAt.WorldHitPoint - cameraPosition).sqrMagnitude);
            marker.transform.position = pointAt.WorldHitPoint;
            marker.transform.LookAt(
                pointAt.WorldHitPoint + (Vector3)cameraForward, cameraUp);
        }

        [Query]
        [None(typeof(PointAtMarkerFadingOut), typeof(DeleteEntityIntention))]
        private void FadeOutMarker(
            in Entity entity,
            in HandPointAtComponent pointAt,
            in AvatarBase avatarBase,
            ref PointAtMarkerHolder marker)
        {
            if (pointAt.IsPointing && (pointAt.WorldHitPoint - avatarBase.transform.position).sqrMagnitude <= MAX_POINT_AT_DISTANCE_SQR)
                return;

            marker.FadeOut();
            World.Add(entity, new PointAtMarkerFadingOut());
        }

        [Query]
        [All(typeof(PointAtMarkerFadingOut))]
        [None(typeof(DeleteEntityIntention))]
        private void ReleaseFadedMarker(
            in Entity entity,
            ref PointAtMarkerHolder marker)
        {
            if (!marker.IsFadedOut)
                return;

            markerPool.Release(marker);
            World.Remove<PointAtMarkerHolder, PointAtMarkerFadingOut>(entity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void CleanupThumbnailCache(in Profile profile)
        {
            latestThumbnailCache.Remove(profile.UserId);
        }
    }
}
