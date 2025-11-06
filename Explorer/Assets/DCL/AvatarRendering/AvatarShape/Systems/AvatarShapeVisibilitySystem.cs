using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using DCL.Friends.UserBlocking;
using DCL.Quality;
using DCL.Rendering.RenderGraphs.RenderFeatures.AvatarOutline;
using DCL.SceneBannedUsers;
using DCL.Utilities;
using ECS.Abstract;
using System.Runtime.CompilerServices;
using UnityEngine;
using Utility.Arch;

namespace DCL.AvatarRendering.AvatarShape
{
    [UpdateInGroup(typeof(CameraGroup))]
    public partial class AvatarShapeVisibilitySystem : BaseUnityLoopSystem
    {
        private readonly RendererFeature_AvatarOutline? outlineFeature;
        private SingleInstanceEntity camera;
        private Plane[] planes;

        private GameObject playerCamera;

        private readonly float startFadeDithering;
        private readonly float endFadeDithering;
        private readonly ObjectProxy<IUserBlockingCache> userBlockingCacheProxy;
        private readonly bool includeBannedUsersFromScene;

        public AvatarShapeVisibilitySystem(World world, ObjectProxy<IUserBlockingCache> userBlockingCacheProxy, IRendererFeaturesCache outlineFeature, float startFadeDithering, float endFadeDitheringm, bool includeBannedUsersFromScene) : base(world)
        {
            this.userBlockingCacheProxy = userBlockingCacheProxy;
            this.outlineFeature = outlineFeature.GetRendererFeature<RendererFeature_AvatarOutline>();
            planes = new Plane[6];

            this.startFadeDithering = startFadeDithering;
            this.endFadeDithering = endFadeDithering;
            this.includeBannedUsersFromScene = includeBannedUsersFromScene;
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
            playerCamera = camera.GetCameraComponent(World).Camera.gameObject;
        }

        protected override void Update(float t)
        {
            AddPlayerCachedVisibilityComponentQuery(World, camera.GetCameraComponent(World));
            AddOthersCachedVisibilityComponentQuery(World);

            UpdateMainPlayerVisibilityStateQuery(World, camera.GetCameraComponent(World));
            UpdateMainPlayerAvatarVisibilityOnCameraDistanceQuery(World);
            UpdateNonPlayerAvatarVisibilityOnCameraDistanceQuery(World);
            BlockAvatarsQuery(World);
            BanAvatarsQuery(World);
            UpdateAvatarsVisibilityStateQuery(World);
            GetAvatarsVisibleWithOutlineQuery(World);
        }

        public bool IsVisibleInCamera(Camera camera, Bounds bounds)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, planes);
            return GeometryUtility.TestPlanesAABB(planes, bounds);
        }

        public bool IsWithinCameraDistance(Camera camera, Transform objectTransform, float maxDistancesquared)
        {
            var diff = camera.transform.position - objectTransform.position;
            float distance = diff.sqrMagnitude;
            return distance <= maxDistancesquared;
        }

        [Query]
        private void GetAvatarsVisibleWithOutline(in AvatarBase avatarBase, ref AvatarShapeComponent avatarShape)
        {
            if (outlineFeature != null && outlineFeature.isActive && (avatarShape.IsPreview || IsWithinCameraDistance(camera.GetCameraComponent(World).Camera, avatarBase.HeadAnchorPoint, 64.0f) && IsVisibleInCamera(camera.GetCameraComponent(World).Camera, avatarBase.AvatarSkinnedMeshRenderer.bounds)))
            {
                RendererFeature_AvatarOutline.m_AvatarOutlineRenderers.AddRange(avatarShape.OutlineCompatibleRenderers);
            }
        }

        [Query]
        [All(typeof(AvatarShapeComponent), typeof(PlayerComponent))]
        [None(typeof(AvatarCachedVisibilityComponent))]
        private void AddPlayerCachedVisibilityComponent([Data] in CameraComponent cameraComponent, in Entity entity, ref AvatarShapeComponent avatarShape)
        {
            bool shouldBeHidden = avatarShape.HiddenByModifierArea || cameraComponent.Mode == CameraMode.FirstPerson;
            var cachedVisibility = InitializeCachedComponent(shouldBeHidden, ref avatarShape);
            World.Add(entity, cachedVisibility);
        }

        [Query]
        [All(typeof(AvatarShapeComponent))]
        [None(typeof(AvatarCachedVisibilityComponent), typeof(PlayerComponent), typeof(PBAvatarShape))]
        private void AddOthersCachedVisibilityComponent(in Entity entity, ref AvatarShapeComponent avatarShape)
        {
            bool shouldBeHidden = avatarShape.HiddenByModifierArea;
            var cachedVisibility = InitializeCachedComponent(shouldBeHidden, ref avatarShape);
            World.Add(entity, cachedVisibility);
        }

        [Query]
        private void UpdateMainPlayerAvatarVisibilityOnCameraDistance(in AvatarCustomSkinningComponent skinningComponent, in PlayerComponent playerComponent, ref AvatarCachedVisibilityComponent avatarCachedVisibility, ref AvatarShapeComponent avatarShapeComponent)
        {
            if (avatarShapeComponent.IsDirty)
            {
                avatarCachedVisibility.ResetDitherState();
                return;
            }

            float currentDistance = (playerComponent.CameraFocus.position - playerCamera.transform.position).magnitude;

            if (avatarCachedVisibility.ShouldUpdateDitherState(currentDistance, startFadeDithering, endFadeDithering))
                skinningComponent.SetFadingDistance(currentDistance);
        }

        [Query]
        [None(typeof(PlayerComponent))]
        private void UpdateNonPlayerAvatarVisibilityOnCameraDistance(in AvatarCustomSkinningComponent skinningComponent, in AvatarBase avatarBase, ref AvatarCachedVisibilityComponent avatarCachedVisibility, ref AvatarShapeComponent avatarShapeComponent)
        {
            if (avatarShapeComponent.IsDirty)
            {
                avatarCachedVisibility.ResetDitherState();
                return;
            }

            float currentDistance = (avatarBase.HeadAnchorPoint.position - playerCamera.transform.position).magnitude;

            if (avatarCachedVisibility.ShouldUpdateDitherState(currentDistance, startFadeDithering, endFadeDithering))
                skinningComponent.SetFadingDistance(currentDistance);
        }

        [Query]
        private void BlockAvatars(in Entity entity, ref AvatarShapeComponent avatarShapeComponent)
        {
            if (!userBlockingCacheProxy.Configured) return;

            if (avatarShapeComponent.InstantiatedWearables.Count == 0) return;

            bool isBlocked = userBlockingCacheProxy.Object!.UserIsBlocked(avatarShapeComponent.ID);

            SetHiddenComponent(entity, isBlocked, HiddenPlayerComponent.HiddenReason.BLOCKED);
        }

        [Query]
        [None(typeof(PlayerComponent))]
        private void BanAvatars(in Entity entity, ref AvatarShapeComponent avatarShapeComponent)
        {
            if (!includeBannedUsersFromScene) return;

            bool isBanned = BannedUsersFromCurrentScene.Instance.IsUserBanned(avatarShapeComponent.ID);

            SetHiddenComponent(entity, isBanned, HiddenPlayerComponent.HiddenReason.BANNED);
        }

        private void SetHiddenComponent(Entity entity, bool hiddenValue, HiddenPlayerComponent.HiddenReason hiddenReason)
        {
            ref HiddenPlayerComponent attachedHiddenComponent = ref World.TryGetRef<HiddenPlayerComponent>(entity, out bool isHiddenComponentAttached);

            if (hiddenValue && (!isHiddenComponentAttached || (isHiddenComponentAttached && !attachedHiddenComponent.Reason.HasFlag(hiddenReason))))
            {
                if (!isHiddenComponentAttached)
                    World.Add(entity, new HiddenPlayerComponent { Reason = hiddenReason } );
                else
                    attachedHiddenComponent.Reason |= hiddenReason;
            }
            else if (!hiddenValue && isHiddenComponentAttached && attachedHiddenComponent.Reason.HasFlag(hiddenReason))
            {
                attachedHiddenComponent.Reason &= ~hiddenReason;
                if (attachedHiddenComponent.Reason == 0)
                    World.TryRemove<HiddenPlayerComponent>(entity);
            }
        }

        [Query]
        private void UpdateMainPlayerVisibilityState([Data] in CameraComponent cameraComponent, ref AvatarShapeComponent avatarShape, in PlayerComponent playerComponent, ref AvatarCachedVisibilityComponent avatarCachedVisibility)
        {
            float currentDistance = (playerComponent.CameraFocus.position - playerCamera.transform.position).magnitude;
            bool shouldBeHidden = cameraComponent.Mode == CameraMode.FirstPerson && currentDistance < startFadeDithering;
            UpdateVisibilityState(ref avatarShape, ref avatarCachedVisibility, shouldBeHidden);
        }


        [Query]
        private void UpdateAvatarsVisibilityState(in Entity entity, ref AvatarShapeComponent avatarShape, ref AvatarCachedVisibilityComponent avatarCachedVisibility, ref CameraComponent cameraComponent)
        {
            bool shouldBeHidden = avatarShape.HiddenByModifierArea || World.Has<HiddenPlayerComponent>(entity) || cameraComponent.Mode == CameraMode.FirstPerson;
            UpdateVisibilityState(ref avatarShape, ref avatarCachedVisibility, shouldBeHidden);
        }

        private void UpdateVisibilityState(ref AvatarShapeComponent avatarShape, ref AvatarCachedVisibilityComponent avatarCachedVisibility, bool shouldBeHidden)
        {
            if (avatarCachedVisibility.IsVisible == shouldBeHidden)
                return;

            if (shouldBeHidden)
                Hide(ref avatarShape);
            else
                Show(ref avatarShape);

            avatarCachedVisibility.IsVisible = shouldBeHidden;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Show(ref AvatarShapeComponent avatarShape)
        {
            ToggleAvatarShape(ref avatarShape, true);
            avatarShape.IsVisible = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Hide(ref AvatarShapeComponent avatarShape)
        {
            ToggleAvatarShape(ref avatarShape, false);
            avatarShape.IsVisible = false;
        }

        private static void ToggleAvatarShape(ref AvatarShapeComponent avatarShape, bool toggle)
        {
            foreach (var wearable in avatarShape.InstantiatedWearables)
            foreach (Renderer renderer in wearable.Renderers)
                renderer.enabled = toggle;
        }

        private AvatarCachedVisibilityComponent InitializeCachedComponent(bool shouldBeHidden, ref AvatarShapeComponent avatarShape)
        {
            var cachedVisibility = new AvatarCachedVisibilityComponent();
            UpdateVisibilityState(ref avatarShape, ref cachedVisibility, shouldBeHidden);
            return cachedVisibility;
        }
    }
}
