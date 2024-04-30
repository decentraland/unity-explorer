using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes;
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
        private bool headIKIsEnabled;
        private SingleInstanceEntity camera;
        private readonly ElementBinding<float> verticalLimit;
        private readonly ElementBinding<float> horizontalLimit;
        private readonly ElementBinding<float> horizontalReset;
        private readonly ElementBinding<float> speed;
        private SingleInstanceEntity settingsEntity;

        private HeadIKSystem(World world, IDebugContainerBuilder builder) : base(world)
        {
            builder.AddWidget("Locomotion: Head IK")
                   .AddToggleField("Enabled", (evt) => { headIKIsEnabled = evt.newValue; }, true)
                   .AddFloatField("Vertical Limit", verticalLimit = new ElementBinding<float>(0))
                   .AddFloatField("Horizontal Limit", horizontalLimit = new ElementBinding<float>(0))
                   .AddFloatField("Horizontal Reset", horizontalReset = new ElementBinding<float>(0))
                   .AddFloatField("Rotation Speed", speed = new ElementBinding<float>(0));
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
            settingsEntity = World.CacheCharacterSettings();

            ICharacterControllerSettings charSettings = settingsEntity.GetCharacterSettings(World);
            headIKIsEnabled = charSettings.HeadIKIsEnabled;
            verticalLimit.Value = charSettings.HeadIKVerticalAngleLimit;
            horizontalLimit.Value = charSettings.HeadIKHorizontalAngleLimit;
            horizontalReset.Value = charSettings.HeadIKHorizontalAngleReset;
            speed.Value = charSettings.HeadIKRotationSpeed;
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
            charSettings.HeadIKVerticalAngleLimit = verticalLimit.Value;
            charSettings.HeadIKHorizontalAngleLimit = horizontalLimit.Value;
            charSettings.HeadIKHorizontalAngleReset = horizontalReset.Value;
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
            in StunComponent stunComponent,
            in CharacterEmoteComponent emoteComponent
        )
        {
            headIK.IsDisabled = !this.headIKIsEnabled;

            bool isEnabled = !stunComponent.IsStunned
                             && rigidTransform.IsGrounded
                             && !rigidTransform.IsOnASteepSlope
                             && !headIK.IsDisabled
                             && !(rigidTransform.MoveVelocity.Velocity.sqrMagnitude > 0.5f)
                             && emoteComponent.CurrentEmoteReference == null;

            avatarBase.HeadIKRig.weight = Mathf.MoveTowards(avatarBase.HeadIKRig.weight, isEnabled ? 1 : 0, 2 * dt);

            // TODO: When enabling and disabling we should reset the reference position
            if (headIK.IsDisabled) return;

            // TODO: Tie this to a proper look-at system to decide what to look at
            Vector3 targetDirection = cameraComponent.Camera.transform.forward;

            ApplyHeadLookAt.Execute(targetDirection, avatarBase, dt, settings);
        }
    }
}
