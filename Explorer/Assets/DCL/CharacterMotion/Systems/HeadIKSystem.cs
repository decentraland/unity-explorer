using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.DebugUtilities.Builders;
using DCL.DebugUtilities.UIBindings;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using System;
using UnityEngine;

namespace DCL.CharacterMotion.Systems
{
    [LogCategory(ReportCategory.MOTION)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(InterpolateCharacterSystem))]
    public partial class HeadIKSystem : BaseUnityLoopSystem
    {
        private bool disableWasToggled;
        private SingleInstanceEntity camera;
        private readonly ElementBinding<float> verticalLimit;
        private readonly ElementBinding<float> horizontalLimit;
        private readonly ElementBinding<float> speed;

        private HeadIKSystem(World world, IDebugContainerBuilder builder) : base(world)
        {
            builder.AddWidget("Locomotion: Head IK")
                   .AddSingleButton("Toggle Enable", () => disableWasToggled = true)
                   .AddFloatField("Vertical Limit", verticalLimit = new ElementBinding<float>(0))
                   .AddFloatField("Horizontal Limit", horizontalLimit = new ElementBinding<float>(0))
                   .AddFloatField("Rotation Speed", speed = new ElementBinding<float>(0));
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            UpdateIKQuery(World, t, in camera.GetCameraComponent(World));
        }

        [Query]
        private void UpdateIK(
            [Data] float dt,
            [Data] in CameraComponent cameraComponent,
            ref HeadIKComponent headIK,
            ref AvatarBase avatarBase,
            in ICharacterControllerSettings settings
        )
        {
            if (!headIK.IsInitialized)
            {
                headIK.IsInitialized = true;
                verticalLimit.Value = settings.HeadIKVerticalAngleLimit;
                horizontalLimit.Value = settings.HeadIKHorizontalAngleLimit;
                speed.Value = settings.HeadIKRotationSpeed;
            }

            settings.HeadIKVerticalAngleLimit = verticalLimit.Value;
            settings.HeadIKHorizontalAngleLimit = horizontalLimit.Value;
            settings.HeadIKRotationSpeed = speed.Value;

            if (disableWasToggled)
            {
                headIK.IsDisabled = !headIK.IsDisabled;
                avatarBase.HeadIKRig.weight = headIK.IsDisabled ? 0 : 1;
                disableWasToggled = false;
            }

            if (headIK.IsDisabled) return;

            Transform reference = avatarBase.HeadPositionConstraint;

            Vector3 referenceAngle = Quaternion.LookRotation(reference.forward).eulerAngles;
            Vector3 targetAngle = Quaternion.LookRotation(cameraComponent.Camera.transform.forward).eulerAngles;

            float horizontalAngle = Mathf.DeltaAngle(referenceAngle.y, targetAngle.y);
            float verticalAngle = Mathf.DeltaAngle(referenceAngle.x, targetAngle.x);

            horizontalAngle = Mathf.Clamp(horizontalAngle, -settings.HeadIKHorizontalAngleLimit, settings.HeadIKHorizontalAngleLimit);
            verticalAngle = Mathf.Clamp(verticalAngle, -settings.HeadIKVerticalAngleLimit, settings.HeadIKVerticalAngleLimit);

            Quaternion rotation = avatarBase.HeadLookAtTarget.localRotation;
            Quaternion targetRotation = Quaternion.AngleAxis(horizontalAngle, Vector3.up) * Quaternion.AngleAxis(verticalAngle, Vector3.right);

            // Quaternion Lerp or MoveTowards use the shortest path, this is wrong
            rotation = Quaternion.RotateTowards(rotation, targetRotation, dt * settings.HeadIKRotationSpeed);
            avatarBase.HeadLookAtTarget.localRotation = rotation;
        }
    }
}
