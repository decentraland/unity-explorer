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
using DCL.CharacterPreview.Components;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.InWorldCamera;
using DCL.Utilities;
using ECS.Abstract;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using DCL.AvatarRendering.DemoScripts.Components;
#endif

namespace DCL.CharacterMotion.Systems
{
    [LogCategory(ReportCategory.MOTION)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ChangeCharacterPositionGroup))]
    public partial class HeadIKSystem : BaseUnityLoopSystem
    {
        private bool headIKIsEnabled;
        private SingleInstanceEntity camera;
        private readonly ElementBinding<float> verticalLimit;
        private readonly ElementBinding<float> horizontalLimit;
        private readonly ElementBinding<float> horizontalReset;
        private readonly ElementBinding<float> speed;
        private SingleInstanceEntity settingsEntity;
        private readonly ObjectProxy<DCLInput> inputProxy;
        private readonly ICharacterControllerSettings settings;

        private HeadIKSystem(World world, IDebugContainerBuilder builder, ObjectProxy<DCLInput> inputProxy, ICharacterControllerSettings settings) : base(world)
        {
            this.inputProxy = inputProxy;
            this.settings = settings;

            verticalLimit = new ElementBinding<float>(0);
            horizontalLimit = new ElementBinding<float>(0);
            horizontalReset = new ElementBinding<float>(0);
            speed = new ElementBinding<float>(0);

            builder.TryAddWidget("Locomotion: Head IK")
                  ?.AddToggleField("Enabled", (evt) => { headIKIsEnabled = evt.newValue; }, true)
                   .AddFloatField("Vertical Limit", verticalLimit)
                   .AddFloatField("Horizontal Limit", horizontalLimit)
                   .AddFloatField("Horizontal Reset", horizontalReset)
                   .AddFloatField("Rotation Speed", speed);
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
            UpdatePreviewAvatarIKQuery(World, t, in camera.GetCameraComponent(World));
            if (!World.Has<InWorldCameraComponent>(camera))
                UpdateIKQuery(World, t, in camera.GetCameraComponent(World));
        }

        [Query]
        [All(typeof(CharacterPreviewComponent))]
        private void UpdatePreviewAvatarIK(
            [Data] float dt,
            [Data] in CameraComponent cameraComponent,
            in CharacterPreviewComponent previewComponent,
            ref HeadIKComponent headIK,
            ref AvatarBase avatarBase
        )
        {
            headIK.IsDisabled = !this.headIKIsEnabled;
            avatarBase.HeadIKRig.weight = 1;

            Vector3 viewportPos = previewComponent.Camera.WorldToViewportPoint(avatarBase.HeadPositionConstraint.position);

            Vector3[] corners = new Vector3[4];
            previewComponent.RenderImageRect.GetWorldCorners(corners);
            Vector3 bottomLeft = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector3 topRight = RectTransformUtility.WorldToScreenPoint(null, corners[2]);

            Vector3 objectScreenPos = new Vector3(
                Mathf.Lerp(bottomLeft.x, topRight.x, viewportPos.x),
                Mathf.Lerp(bottomLeft.y, topRight.y, viewportPos.y),
                300);

            Vector2 mousePos = Mouse.current.position.value;
            Vector3 endScreenPos = new Vector3(mousePos.x, mousePos.y, 0);

            var screenVector = (objectScreenPos - endScreenPos);
            screenVector.y = -screenVector.y;
            var targetDirection = screenVector.normalized;

            Execute(targetDirection, avatarBase, dt, settings);
        }

        private static void Execute(Vector3 targetDirection, AvatarBase avatarBase, float dt, ICharacterControllerSettings settings)
        {
            Transform reference = avatarBase.HeadPositionConstraint;
            Vector3 referenceAngle = Quaternion.LookRotation(reference.forward).eulerAngles;
            Vector3 targetAngle = Quaternion.LookRotation(targetDirection).eulerAngles;

            float horizontalAngle = Mathf.DeltaAngle(referenceAngle.y, targetAngle.y);

            Quaternion horizontalTargetRotation;
            Quaternion verticalTargetRotation;

            float rotationSpeed = settings.HeadIKRotationSpeed;

            //otherwise, calculate rotation within constraints
            {
                //clamp horizontal angle and apply rotation
                horizontalAngle = Mathf.Clamp(horizontalAngle, -settings.HeadIKHorizontalAngleLimit, settings.HeadIKHorizontalAngleLimit);
                horizontalTargetRotation = Quaternion.AngleAxis(horizontalAngle, Vector3.up);

                //calculate vertical angle difference between reference and target, clamped to maximum angle
                float verticalAngle = Mathf.DeltaAngle(referenceAngle.x, targetAngle.x);
                verticalAngle = Mathf.Clamp(verticalAngle, -settings.HeadIKVerticalAngleLimit, settings.HeadIKVerticalAngleLimit);

                //calculate vertical rotation
                verticalTargetRotation = horizontalTargetRotation * Quaternion.AngleAxis(verticalAngle, Vector3.right);
            }

            //apply horizontal rotation
            Quaternion newHorizontalRotation = Quaternion.RotateTowards(avatarBase.HeadLookAtTargetHorizontal.localRotation, horizontalTargetRotation, dt * rotationSpeed);
            avatarBase.HeadLookAtTargetHorizontal.localRotation = newHorizontalRotation;

            //apply vertical rotation
            Quaternion newVerticalRotation = Quaternion.RotateTowards(avatarBase.HeadLookAtTargetVertical.localRotation, verticalTargetRotation, dt * rotationSpeed);
            avatarBase.HeadLookAtTargetVertical.localRotation = newVerticalRotation;
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
#if UNITY_EDITOR
        // This prevents all random avatars from moving the head when the player's camera is moved
        [None(typeof(RandomAvatar))]
#endif
        private void UpdateIK(
            [Data] float dt,
            [Data] in CameraComponent cameraComponent,
            ref HeadIKComponent headIK,
            ref AvatarBase avatarBase,
            in ICharacterControllerSettings settings,
            in CharacterRigidTransform rigidTransform,
            in StunComponent stunComponent,
            in CharacterEmoteComponent emoteComponent,
            in CharacterPlatformComponent platformComponent
        )
        {
            headIK.IsDisabled = !this.headIKIsEnabled;

            bool isEnabled = !stunComponent.IsStunned
                             && rigidTransform.IsGrounded
                             && !rigidTransform.IsOnASteepSlope
                             && !headIK.IsDisabled
                             && !(rigidTransform.MoveVelocity.Velocity.sqrMagnitude > 0.5f)
                             && emoteComponent.CurrentEmoteReference == null
                             && !platformComponent.IsMovingPlatform;

            avatarBase.HeadIKRig.weight = Mathf.MoveTowards(avatarBase.HeadIKRig.weight, isEnabled ? 1 : 0, 2 * dt);

            // TODO: When enabling and disabling we should reset the reference position
            if (headIK.IsDisabled) return;

            // TODO: Tie this to a proper look-at system to decide what to look at
            Vector3 targetDirection = cameraComponent.Camera.transform.forward;

            ApplyHeadLookAt.Execute(targetDirection, avatarBase, dt, settings);
        }
    }
}
