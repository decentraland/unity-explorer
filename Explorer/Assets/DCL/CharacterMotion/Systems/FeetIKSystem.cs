using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Physics;
using DCL.AvatarRendering.AvatarShape;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace DCL.CharacterMotion.Systems
{
    [LogCategory(ReportCategory.MOTION)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(InterpolateCharacterSystem))]
    public partial class FeetIKSystem : BaseUnityLoopSystem
    {
        private RaycastHit raycastHit;

        public FeetIKSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            UpdateIKQuery(World, t);
        }

        [Query]
        private void UpdateIK(
            [Data] float dt,
            ref FeetIKComponent feetIKComponent,
            ref AvatarBase avatarBase,
            in CharacterRigidTransform rigidTransform,
            in ICharacterControllerSettings settings
        )
        {
            if (!feetIKComponent.Initialized)
                Initialize(ref feetIKComponent, avatarBase);

            Vector3 rightLegConstraint = avatarBase.RightLegConstraint.position;
            Vector3 leftLegConstraint = avatarBase.LeftLegConstraint.position;

            // First: Raycast down from right/left constraints and update IK targets

            ApplyLegIK(rightLegConstraint, avatarBase.RightLegConstraint.forward, avatarBase.RightLegIKTarget, ref raycastHit, ref feetIKComponent.Right, settings, dt);
            ApplyLegIK(leftLegConstraint, avatarBase.LeftLegConstraint.forward, avatarBase.LeftLegIKTarget, ref raycastHit, ref feetIKComponent.Left, settings, dt);

            // Second: Calculate IK feet weight based on the constrained local-Y
            ApplyIKWeight(avatarBase.RightLegIK, avatarBase.RightLegConstraint.localPosition, feetIKComponent.Right, rigidTransform.IsGrounded, settings, dt);
            ApplyIKWeight(avatarBase.LeftLegIK, avatarBase.LeftLegConstraint.localPosition, feetIKComponent.Left, rigidTransform.IsGrounded, settings, dt);

            // Third: Calculate the "pull" distance and update the hips
            float highestDist = Mathf.Max(feetIKComponent.Right.distance, feetIKComponent.Left.distance);
            float weight = (avatarBase.RightLegIK.weight + avatarBase.LeftLegIK.weight) / 2;
            MultiPositionConstraintData data = avatarBase.HipsConstraint.data;
            Vector3 offset = data.offset;
            offset.y = Mathf.MoveTowards(offset.y, -highestDist, settings.IKPositionSpeed * dt);
            data.offset = offset;
            avatarBase.HipsConstraint.data = data;
            avatarBase.HipsConstraint.weight = weight;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyIKWeight(TwoBoneIKConstraint ik, Vector3 rightLegPosition, FeetIKComponent.FeetComponent feetComponent,
            bool isGrounded, ICharacterControllerSettings settings, float dt)
        {
            int groundedWeight = isGrounded ? 1 : 0;
            float ikWeightRight = !feetComponent.isGrounded ? 0 : 1f - ((rightLegPosition.y - 0.08f) / 0.10f);
            float targetWeight = Mathf.RoundToInt(ikWeightRight) * groundedWeight;

            // We apply ik weight speed only if its increasing, decreasing it is instant
            if (ik.weight < targetWeight)
                ik.weight = Mathf.MoveTowards(ik.weight, targetWeight, settings.IKWeightSpeed * dt);
            else
                ik.weight = targetWeight;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyLegIK(
            Vector3 legConstraintPosition,
            Vector3 legConstraintForward,
            Transform legIKTarget,
            ref RaycastHit hitInfo,
            ref FeetIKComponent.FeetComponent feetComponent,
            ICharacterControllerSettings settings,
            float dt)
        {
            float pullDist = settings.FeetIKHipsPullMaxDistance;
            Vector3 origin = legConstraintPosition;
            Vector3 rayOrigin = origin + (Vector3.up * pullDist);
            Vector3 rayDirection = Vector3.down;
            float rayDistance = pullDist * 2;

            Debug.DrawRay(rayOrigin, rayDirection * rayDistance, Color.red);

            if (Physics.SphereCast(rayOrigin, settings.FeetIKSphereSize, rayDirection, out hitInfo, rayDistance, PhysicsLayers.CHARACTER_ONLY_MASK))
            {
                legIKTarget.position = Vector3.MoveTowards(legIKTarget.position, hitInfo.point, settings.IKPositionSpeed * dt);
                var rotationCorrection = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
                legConstraintForward.y = 0;
                legIKTarget.rotation = rotationCorrection * Quaternion.LookRotation(legConstraintForward, Vector3.up);
                feetComponent.isGrounded = true;
                feetComponent.distance = hitInfo.distance - pullDist;
                return;
            }

            legIKTarget.localPosition = feetComponent.TargetInitialPosition;
            legIKTarget.localRotation = feetComponent.TargetInitialRotation;
            feetComponent.isGrounded = false;
            feetComponent.distance = 0;
        }

        private static void Initialize(ref FeetIKComponent feetIKComponent, AvatarBase avatarBase)
        {
            feetIKComponent.Initialized = true;
            feetIKComponent.Left.TargetInitialPosition = avatarBase.LeftLegIKTarget.localPosition;
            feetIKComponent.Left.TargetInitialRotation = avatarBase.LeftLegIKTarget.localRotation;
            feetIKComponent.Right.TargetInitialPosition = avatarBase.RightLegIKTarget.localPosition;
            feetIKComponent.Right.TargetInitialRotation = avatarBase.RightLegIKTarget.localRotation;
        }
    }
}
