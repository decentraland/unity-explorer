using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Physics;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using ECS.Abstract;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace DCL.CharacterMotion.Systems
{
    [LogCategory(ReportCategory.MOTION)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(InterpolateCharacterSystem))]
    public partial class HandsIKSystem : BaseUnityLoopSystem
    {
        private bool handsIkSystemIsEnabled = true;
        private readonly ElementBinding<float> wallDistance;
        private readonly ElementBinding<float> ikWeightSpeed;

        private SingleInstanceEntity settingsEntity;

        private HandsIKSystem(World world, IDebugContainerBuilder debugBuilder) : base(world)
        {
            debugBuilder.AddWidget("Locomotion: Hands IK")
                        .AddToggleField("Enabled", evt => { handsIkSystemIsEnabled = evt.newValue; }, true)
                        .AddFloatField("Wall Distance", wallDistance = new ElementBinding<float>(0))
                        .AddFloatField("IK Weight Speed", ikWeightSpeed = new ElementBinding<float>(0));
        }

        public override void Initialize()
        {
            settingsEntity = World.CacheCharacterSettings();

            ICharacterControllerSettings settings = settingsEntity.GetCharacterSettings(World);

            wallDistance.Value = settings.HandsIKWallHitDistance;
            ikWeightSpeed.Value = settings.HandsIKWeightSpeed;
        }

        protected override void Update(float t)
        {
            ICharacterControllerSettings settings = settingsEntity.GetCharacterSettings(World);
            settings.HandsIKWallHitDistance = wallDistance.Value;
            settings.HandsIKWeightSpeed = ikWeightSpeed.Value;

            UpdateIKQuery(World, t);
        }

        [Query]
        private void UpdateIK(
            [Data] float dt,
            ref HandsIKComponent handsIKComponent,
            ref AvatarBase avatarBase,
            in CharacterRigidTransform rigidTransform,
            in ICharacterControllerSettings settings,
            in CharacterEmoteComponent emoteComponent
        )
        {
            handsIKComponent.IsDisabled = !handsIkSystemIsEnabled;

            // To avoid using the Hands IK during any special state we update this
            bool isEnabled = !handsIKComponent.IsDisabled
                             && rigidTransform.IsGrounded
                             && (!rigidTransform.IsOnASteepSlope || rigidTransform.IsStuck)
                             && emoteComponent.CurrentEmoteReference == null;

            avatarBase.HandsIKRig.weight = Mathf.MoveTowards(avatarBase.HandsIKRig.weight, isEnabled ? 1 : 0, settings.HandsIKWeightSpeed * dt);

            if (handsIKComponent.IsDisabled) return;

            ApplyHandIK(avatarBase.LeftHandRaycast, avatarBase.LeftHandSubTarget, avatarBase.LeftHandIK, settings, dt);
            ApplyHandIK(avatarBase.RightHandRaycast, avatarBase.RightHandSubTarget, avatarBase.RightHandIK, settings, dt);

            Transform leftHint = avatarBase.LeftHandIK.data.hint;
            Vector3 leftPosition = settings.HandsIKElbowOffset;
            leftPosition.x = -leftPosition.x;
            leftHint.localPosition = leftPosition;

            Transform rightHint = avatarBase.RightHandIK.data.hint;
            Vector3 rightPosition = settings.HandsIKElbowOffset;
            rightHint.localPosition = rightPosition;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyHandIK(
            Transform raycastTransform,
            Transform handIKTarget,
            TwoBoneIKConstraint handIK,
            ICharacterControllerSettings settings,
            float dt)
        {
            Vector3 origin = raycastTransform.position;
            Vector3 rayOrigin = origin;
            Vector3 rayDirection = raycastTransform.forward;
            float rayDistance = settings.HandsIKWallHitDistance;

            var targetWeight = 0;

            Debug.DrawRay(rayOrigin, rayDirection * rayDistance, Color.blue, dt);
            if (Physics.SphereCast(rayOrigin, settings.FeetIKSphereSize, rayDirection, out RaycastHit hitInfo, rayDistance, PhysicsLayers.CHARACTER_ONLY_MASK))
            {
                handIKTarget.position = Vector3.MoveTowards(handIKTarget.position, hitInfo.point, settings.IKPositionSpeed * dt);
                handIKTarget.forward = -hitInfo.normal;
                targetWeight = 1;
            }

            handIK.weight = Mathf.MoveTowards(handIK.weight, targetWeight, settings.HandsIKWeightSpeed * dt);
        }
    }
}
