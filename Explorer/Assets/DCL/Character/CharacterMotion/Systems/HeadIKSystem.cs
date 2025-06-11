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
        private readonly ICharacterControllerSettings settings;
        private readonly DCLInput dclInput;
        private readonly Vector3[] previewImageCorners = new Vector3[4];

        private HeadIKSystem(World world, IDebugContainerBuilder builder, ICharacterControllerSettings settings, DCLInput dclInput) : base(world)
        {
            this.settings = settings;
            this.dclInput = dclInput;

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
            UpdatePreviewAvatarIKQuery(World, t);
            if (!World.Has<InWorldCameraComponent>(camera))
                UpdateIKQuery(World, t, in camera.GetCameraComponent(World));
        }

        [Query]
        private void UpdatePreviewAvatarIK([Data] float dt, in CharacterPreviewComponent previewComponent, ref HeadIKComponent headIK, ref AvatarBase avatarBase)
        {
            headIK.IsDisabled = !this.headIKIsEnabled;

            Vector3 viewportPos = previewComponent.Camera.WorldToViewportPoint(avatarBase.HeadPositionConstraint.position);

            previewComponent.RenderImageRect.GetWorldCorners(previewImageCorners);
            Vector3 bottomLeft = RectTransformUtility.WorldToScreenPoint(null, previewImageCorners[0]);
            Vector3 topRight = RectTransformUtility.WorldToScreenPoint(null, previewImageCorners[2]);

            Vector3 objectScreenPos = new Vector3(
                Mathf.Lerp(bottomLeft.x, topRight.x, viewportPos.x),
                Mathf.Lerp(bottomLeft.y, topRight.y, viewportPos.y),
                previewComponent.Settings.AvatarDepth);

            if(!dclInput.UI.Point.enabled)
                dclInput.UI.Point.Enable();

            Vector2 mousePos = dclInput.UI.Point.ReadValue<Vector2>();
            Vector3 mouseScreenPos = new Vector3(mousePos.x, mousePos.y, 0);

            var screenVector = objectScreenPos - mouseScreenPos;
            screenVector.y = -screenVector.y;

            ApplyHeadLookAt.Execute(screenVector.normalized, avatarBase, dt * previewComponent.Settings.HeadMoveSpeed, settings, useFrontalReset: false);
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
