﻿using Arch.Core;
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

        private HeadIKSystem(World world, IDebugContainerBuilder builder, ICharacterControllerSettings settings) : base(world)
        {
            this.settings = settings;
            dclInput = DCLInput.Instance;

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

            UpdateIKQuery(World, t, in camera.GetCameraComponent(World), World.Has<InWorldCameraComponent>(camera));
        }

        [Query]
        private void UpdatePreviewAvatarIK([Data] float dt, in CharacterPreviewComponent previewComponent, ref HeadIKComponent headIK,
            ref AvatarBase avatarBase, in CharacterEmoteComponent emoteComponent)
        {
            bool isEnabled = emoteComponent.CurrentEmoteReference == null && headIKIsEnabled && headIK.IsEnabled;
            avatarBase.HeadIKRig.weight = Mathf.MoveTowards(avatarBase.HeadIKRig.weight, isEnabled ? 1 : 0, settings.HeadIKWeightChangeSpeed * dt);

            if (!isEnabled) return;

            (Vector3 bottomLeft, Vector3 topRight) = GetImageScreenCorners(previewComponent.RenderImageRect);
            Vector3 viewportPos = previewComponent.Camera.WorldToViewportPoint(avatarBase.HeadPositionConstraint.position);

            Vector3 objectScreenPos = new Vector3(
                Mathf.Lerp(bottomLeft.x, topRight.x, viewportPos.x),
                Mathf.Lerp(bottomLeft.y, topRight.y, viewportPos.y),
                previewComponent.Settings.MinAvatarDepth);

            if(!dclInput.UI.Point.enabled)
                dclInput.UI.Point.Enable();

            Vector2 mousePos = dclInput.UI.Point.ReadValue<Vector2>();
            Vector3 mouseScreenPos = new Vector3(mousePos.x, mousePos.y, 0);

            var screenVector = objectScreenPos - mouseScreenPos;
            screenVector.y = -screenVector.y;
            screenVector.z = LerpAvatarDepth(previewComponent, screenVector);

            ApplyHeadLookAt.Execute(screenVector.normalized, avatarBase, dt * previewComponent.Settings.HeadMoveSpeed, settings, useFrontalReset: false);
        }

        private static float LerpAvatarDepth(CharacterPreviewComponent previewComponent, Vector3 screenVector)
        {
            float screenVector2DMagnitude = new Vector2(screenVector.x, screenVector.y).magnitude;
            float screenHalfDiagonal = new Vector2(Screen.width, Screen.height).magnitude / 2;
            float normalizedMagnitude = Mathf.Clamp01(screenVector2DMagnitude / screenHalfDiagonal);
            float minZ = previewComponent.Settings.MinAvatarDepth;
            float maxZ = previewComponent.Settings.MaxAvatarDepth;

            return Mathf.Lerp(minZ, maxZ, normalizedMagnitude);
        }

        private static (Vector3 bottomLeft, Vector3 topRight) GetImageScreenCorners(RectTransform imageRect)
        {
            Rect rect = imageRect.rect;
            var bottomLeftLocal = new Vector3(rect.x, rect.y, 0.0f);
            var topRightLocal = new Vector3(rect.xMax, rect.yMax, 0.0f);

            Matrix4x4 localToWorldMatrix = imageRect.transform.localToWorldMatrix;

            return (
                bottomLeft: RectTransformUtility.WorldToScreenPoint(null, localToWorldMatrix.MultiplyPoint(bottomLeftLocal)),
                topRight: RectTransformUtility.WorldToScreenPoint(null, localToWorldMatrix.MultiplyPoint(topRightLocal)));
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
            [Data] bool inWorldCameraActive,
            ref HeadIKComponent headIK,
            ref AvatarBase avatarBase,
            in ICharacterControllerSettings settings,
            in CharacterRigidTransform rigidTransform,
            in StunComponent stunComponent,
            in CharacterEmoteComponent emoteComponent,
            in CharacterPlatformComponent platformComponent
        )
        {
            bool isFeatureAndComponentEnabled = headIKIsEnabled && headIK.IsEnabled;

            bool isEnabled = !stunComponent.IsStunned
                             && rigidTransform.IsGrounded
                             && !rigidTransform.IsOnASteepSlope
                             && isFeatureAndComponentEnabled
                             && !(rigidTransform.MoveVelocity.Velocity.sqrMagnitude > 0.5f)
                             && !emoteComponent.IsPlayingEmote
                             && !platformComponent.PositionChanged;

            avatarBase.HeadIKRig.weight = Mathf.MoveTowards(avatarBase.HeadIKRig.weight, isEnabled ? 1 : 0, settings.HeadIKWeightChangeSpeed * dt);

            // TODO: When enabling and disabling we should reset the reference position
            if (!isEnabled || inWorldCameraActive) return;

            // TODO: Tie this to a proper look-at system to decide what to look at
            Vector3 targetDirection = cameraComponent.Camera.transform.forward;

            ApplyHeadLookAt.Execute(targetDirection, avatarBase, dt, settings);
        }
    }
}
