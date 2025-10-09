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

            ref HiddenPlayerComponent attachedHiddenComponent = ref World.TryGetRef<HiddenPlayerComponent>(entity, out bool isHiddenComponentAttached);

            if (isBlocked && (!isHiddenComponentAttached || (isHiddenComponentAttached && !attachedHiddenComponent.Reason.HasFlag(HiddenPlayerComponent.HiddenReason.BLOCKED))))
            {
                if (!isHiddenComponentAttached)
                    World.Add(entity, new HiddenPlayerComponent { Reason = HiddenPlayerComponent.HiddenReason.BLOCKED } );
                else
                    attachedHiddenComponent.Reason |= HiddenPlayerComponent.HiddenReason.BLOCKED;
            }
            else if (!isBlocked && isHiddenComponentAttached && attachedHiddenComponent.Reason.HasFlag(HiddenPlayerComponent.HiddenReason.BLOCKED))
            {
                attachedHiddenComponent.Reason &= ~HiddenPlayerComponent.HiddenReason.BLOCKED;
                if (attachedHiddenComponent.Reason == 0)
                    World.TryRemove<HiddenPlayerComponent>(entity);
            }
        }

        [Query]
        [None(typeof(PlayerComponent))]
        private void BanAvatars(in Entity entity, ref AvatarShapeComponent avatarShapeComponent)
        {
            if (!includeBannedUsersFromScene) return;

            bool isBanned = BannedUsersFromCurrentScene.Instance.IsUserBanned(avatarShapeComponent.ID);

            ref HiddenPlayerComponent attachedHiddenComponent = ref World.TryGetRef<HiddenPlayerComponent>(entity, out bool isHiddenComponentAttached);

            if (isBanned && (!isHiddenComponentAttached || (isHiddenComponentAttached && !attachedHiddenComponent.Reason.HasFlag(HiddenPlayerComponent.HiddenReason.BANNED))))
            {
                if (!isHiddenComponentAttached)
                    World.Add(entity, new HiddenPlayerComponent { Reason = HiddenPlayerComponent.HiddenReason.BANNED } );
                else
                    attachedHiddenComponent.Reason |= HiddenPlayerComponent.HiddenReason.BANNED;
            }
            else if (!isBanned && isHiddenComponentAttached && attachedHiddenComponent.Reason.HasFlag(HiddenPlayerComponent.HiddenReason.BANNED))
            {
                attachedHiddenComponent.Reason &= ~HiddenPlayerComponent.HiddenReason.BANNED;
                if (attachedHiddenComponent.Reason == 0)
                    World.TryRemove<HiddenPlayerComponent>(entity);
            }
        }

        [Query]
        private void UpdateAvatarsVisibilityState(in Entity entity, ref AvatarShapeComponent avatarShape, ref AvatarCachedVisibilityComponent avatarCachedVisibility)
        {
            bool shouldBeHidden = avatarShape.HiddenByModifierArea || World.Has<HiddenPlayerComponent>(entity);
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
