using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using DCL.Quality;
using DCL.Rendering.Avatar;
using ECS.Abstract;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    public partial class AvatarShapeVisibilitySystem : BaseUnityLoopSystem
    {
        private readonly OutlineRendererFeature? outlineFeature;
        private SingleInstanceEntity camera;
        private Plane[] planes;

        private GameObject playerCamera;
        private readonly float ditheringLimit;

        public AvatarShapeVisibilitySystem(World world, IRendererFeaturesCache outlineFeature, float startFadeDithering) : base(world)
        {
            this.outlineFeature = outlineFeature.GetRendererFeature<OutlineRendererFeature>();
            planes = new Plane[6];

            //Add a small delta to be able to avoid rounding problems
            ditheringLimit = startFadeDithering + 0.1f;
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
            if (outlineFeature != null && outlineFeature.isActive && IsWithinCameraDistance(camera.GetCameraComponent(World).Camera, avatarBase.HeadAnchorPoint, 64.0f) && IsVisibleInCamera(camera.GetCameraComponent(World).Camera, avatarBase.AvatarSkinnedMeshRenderer.bounds))
            {
                OutlineRendererFeature.m_OutlineRenderers.AddRange(avatarShape.OutlineCompatibleRenderers);
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
        private void UpdateMainPlayerAvatarVisibilityOnCameraDistance(in AvatarCustomSkinningComponent skinningComponent, in PlayerComponent playerComponent)
        {
            float currentDistance = (playerComponent.CameraFocus.position - playerCamera.transform.position).magnitude;

            if (currentDistance <= ditheringLimit)
                skinningComponent.SetFadingDistance(currentDistance);
        }

        [Query]
        [None(typeof(PlayerComponent))]
        private void UpdateNonPlayerAvatarVisibilityOnCameraDistance(in AvatarCustomSkinningComponent skinningComponent, in AvatarBase avatarBase)
        {
            float currentDistance = (avatarBase.HeadAnchorPoint.position - playerCamera.transform.position).magnitude;

            if (currentDistance <= ditheringLimit)
                skinningComponent.SetFadingDistance(currentDistance);
        }

        [Query]
        private void UpdateAvatarsVisibilityState(in Entity entity, ref AvatarShapeComponent avatarShape, ref AvatarCachedVisibilityComponent avatarCachedVisibility)
        {
            bool shouldBeHidden = avatarShape.HiddenByModifierArea || World.Has<BlockedPlayerComponent>(entity);
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
