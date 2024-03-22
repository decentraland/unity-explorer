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
            UpdatePlayerFirstPersonQuery(World, camera.GetCameraComponent(World));
            UpdateAvatarsVisibilityStateQuery(World);
            UpdateWearableRendererBoundsQuery(World);
        }

        [Query]
        [All(typeof(PlayerComponent))]
        private void UpdatePlayerFirstPerson([Data] in CameraComponent camera, ref AvatarShapeComponent avatarShape)
        {
            UpdateVisibilityState(ref avatarShape, avatarShape.HiddenByModifierArea || camera.Mode == CameraMode.FirstPerson);
        }

        [Query]
        [None(typeof(PlayerComponent))]
        private void UpdateAvatarsVisibilityState(ref AvatarShapeComponent avatarShape)
        {
            UpdateVisibilityState(ref avatarShape, avatarShape.HiddenByModifierArea);
        }

        private void UpdateVisibilityState(ref AvatarShapeComponent avatarShape, bool shouldBeHidden)
        {
            if (avatarShape.IsVisible == shouldBeHidden)
            {
                if (shouldBeHidden)
                    Hide(ref avatarShape);
                else
                    Show(ref avatarShape);
            }
        }

        [Query]
        private void UpdateWearableRendererBounds(ref AvatarShapeComponent avatarShape)
        {
            foreach (CachedWearable wearable in avatarShape.InstantiatedWearables)
            {
                foreach (Renderer renderer in wearable.Renderers)
                {
                    if (!renderer.enabled) continue;
                    renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * 5);
                }
            }
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
            foreach (CachedWearable wearable in avatarShape.InstantiatedWearables)
            foreach (Renderer renderer in wearable.Renderers)
                renderer.enabled = toggle;
        }
    }
}
