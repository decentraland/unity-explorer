using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character.Components;
using DCL.CharacterCamera;
using ECS.Abstract;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    public partial class AvatarShapeVisibilitySystem : BaseUnityLoopSystem
    {
        private SingleInstanceEntity camera;

        public AvatarShapeVisibilitySystem(World world) : base(world) { }

        public override void Initialize()
        {
            camera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            AddPlayerCachedVisibilityComponentQuery(World, camera.GetCameraComponent(World));
            AddOthersCachedVisibilityComponentQuery(World);

            UpdatePlayerFirstPersonQuery(World, camera.GetCameraComponent(World));
            UpdateAvatarsVisibilityStateQuery(World);
        }

        [Query]
        [All(typeof(AvatarShapeComponent), typeof(PlayerComponent))]
        [None(typeof(AvatarCachedVisibilityComponent))]
        private void AddPlayerCachedVisibilityComponent([Data] in CameraComponent camera, in Entity entity, ref AvatarShapeComponent avatarShape)
        {
            bool shouldBeHidden = avatarShape.HiddenByModifierArea || camera.Mode == CameraMode.FirstPerson;
            var cachedVisibility = InitializeCachedComponent(shouldBeHidden, ref avatarShape);
            World.Add(entity, cachedVisibility);
        }

        [Query]
        [All(typeof(AvatarShapeComponent))]
        [None(typeof(AvatarCachedVisibilityComponent), typeof(PlayerComponent))]
        private void AddOthersCachedVisibilityComponent(in Entity entity, ref AvatarShapeComponent avatarShape)
        {
            bool shouldBeHidden = avatarShape.HiddenByModifierArea;
            var cachedVisibility = InitializeCachedComponent(shouldBeHidden, ref avatarShape);
            World.Add(entity, cachedVisibility);
        }

        [Query]
        [All(typeof(PlayerComponent))]
        private void UpdatePlayerFirstPerson([Data] in CameraComponent camera, ref AvatarShapeComponent avatarShape, ref AvatarCachedVisibilityComponent avatarCachedVisibility)
        {
            bool shouldBeHidden = avatarShape.HiddenByModifierArea || camera.Mode == CameraMode.FirstPerson;
            UpdateVisibilityState(ref avatarShape, ref avatarCachedVisibility, shouldBeHidden);
        }

        [Query]
        [None(typeof(PlayerComponent))]
        private void UpdateAvatarsVisibilityState(ref AvatarShapeComponent avatarShape, ref AvatarCachedVisibilityComponent avatarCachedVisibility)
        {
            bool shouldBeHidden = avatarShape.HiddenByModifierArea;
            UpdateVisibilityState(ref avatarShape, ref avatarCachedVisibility, shouldBeHidden);
        }

        private void UpdateVisibilityState( ref AvatarShapeComponent avatarShape, ref AvatarCachedVisibilityComponent avatarCachedVisibility, bool shouldBeHidden)
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
