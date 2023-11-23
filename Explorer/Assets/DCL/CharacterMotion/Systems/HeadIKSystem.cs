using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.IK;
using DCL.CharacterMotion.Settings;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using ECS.Abstract;
using System.Runtime.CompilerServices;
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
        private SingleInstanceEntity settingsEntity;
        private bool isInitialized;

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
            isInitialized = false;
            camera = World.CacheCamera();
            settingsEntity = World.CacheCharacterSettings();
        }

        protected override void Update(float t)
        {
            UpdateDebugValues();
            UpdateIKQuery(World, t, in camera.GetCameraComponent(World));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateDebugValues()
        {
            ICharacterControllerSettings charSettings = settingsEntity.GetCharacterSettings(World);

            if (!isInitialized)
            {
                isInitialized = true;
                verticalLimit.Value = charSettings.HeadIKVerticalAngleLimit;
                horizontalLimit.Value = charSettings.HeadIKHorizontalAngleLimit;
                speed.Value = charSettings.HeadIKRotationSpeed;

                // TODO: Remove this once this system is properly tied up to a look-up system, this flag will just disable it by default
                disableWasToggled = true;
            }

            charSettings.HeadIKVerticalAngleLimit = verticalLimit.Value;
            charSettings.HeadIKHorizontalAngleLimit = horizontalLimit.Value;
            charSettings.HeadIKRotationSpeed = speed.Value;
        }

        [Query]
        private void UpdateIK(
            [Data] float dt,
            [Data] in CameraComponent cameraComponent,
            ref HeadIKComponent headIK,
            ref AvatarBase avatarBase,
            in ICharacterControllerSettings settings,
            in CharacterRigidTransform rigidTransform,
            in StunComponent stunComponent
        )
        {
            if (disableWasToggled)
            {
                headIK.IsDisabled = !headIK.IsDisabled;
                disableWasToggled = false;
            }

            bool isEnabled = !stunComponent.IsStunned
                             && rigidTransform.IsGrounded
                             && !rigidTransform.IsOnASteepSlope
                             && !headIK.IsDisabled;

            avatarBase.HeadIKRig.weight = Mathf.MoveTowards(avatarBase.HeadIKRig.weight, isEnabled ? 1 : 0, 2 * dt);

            // TODO: When enabling and disabling we should reset the reference position
            if (headIK.IsDisabled) return;

            // TODO: Tie this to a proper look-at system to decide what to look at
            Vector3 targetDirection = cameraComponent.Camera.transform.forward;

            ApplyHeadLookAt.Execute(targetDirection, avatarBase, dt, settings);
        }
    }
}
