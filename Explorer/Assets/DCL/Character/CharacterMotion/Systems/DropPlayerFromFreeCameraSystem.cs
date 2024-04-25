using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using ECS.Abstract;
using ECS.Unity.Transforms.Components;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DCL.CharacterMotion.Systems
{
    /// <summary>
    ///     Debug system to drop the player from the free camera's position.
    ///     It's not for production purposes
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(CameraGroup))]
    [UpdateAfter(typeof(InterpolateCharacterSystem))] // prevent conflicts with interpolation
    public partial class DropPlayerFromFreeCameraSystem : BaseUnityLoopSystem
    {
        private readonly InputAction dropAction;
        private SingleInstanceEntity camera;

        internal DropPlayerFromFreeCameraSystem(World world, InputAction dropAction) : base(world)
        {
            this.dropAction = dropAction;
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            if (dropAction.WasPerformedThisFrame())
                DropQuery(World, ref camera.GetCameraComponent(World));
        }

        [Query]
        private void Drop([Data] ref CameraComponent cameraComponent, ref CharacterTransform playerTransform, ref CharacterController characterController)
        {
            Vector3 delta = cameraComponent.Camera.transform.position - playerTransform.Position;
            characterController.Move(delta);

            // Cheat camera input to switch to third person
            cameraComponent.Mode = CameraMode.ThirdPerson;
        }
    }
}
