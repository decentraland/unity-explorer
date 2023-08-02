using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using ECS.Abstract;
using ECS.CharacterMotion.Components;
using ECS.CharacterMotion.Settings;
using ECS.Unity.Transforms.Components;
using UnityEngine;

namespace ECS.CharacterMotion.Systems
{
    /// <summary>
    ///     Rotates character outside of physics update as it does not impact collisions or any other interactions
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CameraGroup))]
    public partial class RotateCharacterSystem : BaseUnityLoopSystem
    {
        private SingleInstanceEntity camera;

        internal RotateCharacterSystem(World world) : base(world) { }

        public override void Initialize()
        {
            camera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            LerpRotationQuery(World, in camera.GetCameraComponent(World));
        }

        [Query]
        private void LerpRotation(
            [Data] in CameraComponent camera,
            ref ICharacterControllerSettings characterControllerSettings,
            ref TransformComponent transform,
            ref MovementInputComponent input)
        {
            Transform cameraTransform = camera.Camera.transform;
            Vector3 forward = cameraTransform.forward;
            forward.y = 0;
            Vector3 right = cameraTransform.right;
            right.y = 0;

            Vector3 targetForward = ((forward * input.Axes.y) + (right * input.Axes.x)).normalized;

            Transform characterTransform = transform.Transform;
            Vector3 characterForward = characterTransform.forward;

            characterTransform.forward = Vector3.Slerp(characterForward, targetForward, characterControllerSettings.RotationAngularSpeed * Time.deltaTime);
        }
    }
}
