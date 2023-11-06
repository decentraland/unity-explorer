using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Physics;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.DebugUtilities.Builders;
using DCL.DebugUtilities.UIBindings;
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
        private const float FEET_HEIGHT_CORRECTION = 0.09f;
        private bool disableWasToggled;
        private readonly ElementBinding<string> leftIKWeightBinding;
        private readonly ElementBinding<string> rightIKWeightBinding;
        private readonly ElementBinding<float> ikWeightChangeSpeed;
        private readonly ElementBinding<float> ikPositionChangeSpeed;
        private readonly ElementBinding<float> ikDistance;
        private readonly ElementBinding<float> spherecastWidth;

        private FeetIKSystem(World world, IDebugContainerBuilder debugBuilder) : base(world)
        {
            debugBuilder.AddWidget("Locomotion: Feet IK")
                        .AddSingleButton("Toggle Enable", () => disableWasToggled = true)
                        .AddCustomMarker("Left IK Weight", leftIKWeightBinding = new ElementBinding<string>("0"))
                        .AddCustomMarker("Right IK Weight", rightIKWeightBinding = new ElementBinding<string>("0"))
                        .AddFloatField("IK Change Speed", ikWeightChangeSpeed = new ElementBinding<float>(0))
                        .AddFloatField("IK Position Speed", ikPositionChangeSpeed = new ElementBinding<float>(0))
                        .AddFloatField("IK Distance", ikDistance = new ElementBinding<float>(0))
                        .AddFloatField("Spherecast Width", spherecastWidth = new ElementBinding<float>(0));
        }

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
            ManageDebugSettings(ref feetIKComponent, avatarBase, settings);

            if (feetIKComponent.IsDisabled) return;
            if (!feetIKComponent.Initialized)
                Initialize(ref feetIKComponent, avatarBase);

            // First: Raycast down from right/left constraints and update IK targets

            Transform rightLegConstraint = avatarBase.RightLegConstraint;
            Transform leftLegConstraint = avatarBase.LeftLegConstraint;

            ApplyLegIK(rightLegConstraint, rightLegConstraint.forward, avatarBase.RightLegIKTarget, ref feetIKComponent.Right, settings, dt);
            ApplyLegIK(leftLegConstraint, leftLegConstraint.forward, avatarBase.LeftLegIKTarget, ref feetIKComponent.Left, settings, dt);

            // Second: Calculate IK feet weight based on the constrained local-Y
            ApplyIKWeight(avatarBase.RightLegIK, rightLegConstraint.localPosition, ref feetIKComponent.Right, rigidTransform.IsGrounded, settings, dt);
            ApplyIKWeight(avatarBase.LeftLegIK, leftLegConstraint.localPosition, ref feetIKComponent.Left, rigidTransform.IsGrounded, settings, dt);

            // Fix/Remove those strings allocations
            leftIKWeightBinding.Value = $"{avatarBase.LeftLegIK.weight:F2}";
            rightIKWeightBinding.Value = $"{avatarBase.RightLegIK.weight:F2}";

            // Third: Calculate the "pull" distance and update the hips
            ApplyHipsHeightCorrection(dt, ref feetIKComponent, avatarBase, settings);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ManageDebugSettings(ref FeetIKComponent feetIKComponent, AvatarBase avatarBase, ICharacterControllerSettings settings)
        {
            if (disableWasToggled)
            {
                disableWasToggled = false;
                feetIKComponent.IsDisabled = !feetIKComponent.IsDisabled;
                avatarBase.FeetIKRig.weight = feetIKComponent.IsDisabled ? 0 : 1;
            }

            if (!feetIKComponent.Initialized)
            {
                ikWeightChangeSpeed.Value = settings.IKWeightSpeed;
                ikPositionChangeSpeed.Value = settings.IKPositionSpeed;
                ikDistance.Value = settings.FeetIKHipsPullMaxDistance;
                spherecastWidth.Value = settings.FeetIKSphereSize;
            }

            settings.IKWeightSpeed = ikWeightChangeSpeed.Value;
            settings.IKPositionSpeed = ikPositionChangeSpeed.Value;
            settings.FeetIKHipsPullMaxDistance = ikDistance.Value;
            settings.FeetIKSphereSize = spherecastWidth.Value;
        }

        private static void Initialize(ref FeetIKComponent feetIKComponent, AvatarBase avatarBase)
        {
            feetIKComponent.Initialized = true;
            feetIKComponent.Left.TargetInitialPosition = avatarBase.LeftLegIKTarget.localPosition;
            feetIKComponent.Left.TargetInitialRotation = avatarBase.LeftLegIKTarget.localRotation;
            feetIKComponent.Right.TargetInitialPosition = avatarBase.RightLegIKTarget.localPosition;
            feetIKComponent.Right.TargetInitialRotation = avatarBase.RightLegIKTarget.localRotation;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyIKWeight(
            TwoBoneIKConstraint ik,
            Vector3 rightLegPosition,
            ref FeetIKComponent.FeetComponent feetComponent,
            bool isGrounded,
            ICharacterControllerSettings settings,
            float dt)
        {
            int groundedWeight = isGrounded ? 1 : 0;

            // 0.08 is the distance between the ground and the foot bone when "standing still"
            // 0.10 is the height when we dont want the foot to have IK enabled
            // Games configure this weight trough Animation Curves on each animation, but since we cant do that here, we just do magic math
            float ikWeightRight = !feetComponent.isGrounded ? 0 : 1f - ((rightLegPosition.y - 0.08f) / 0.10f);
            float targetWeight = Mathf.RoundToInt(ikWeightRight) * groundedWeight;

            // We apply ik weight speed only if its increasing, decreasing it is instant, this avoids being partially snapped into the ground when suddenly jumping
            if (ik.weight < targetWeight)
                ik.weight = Mathf.MoveTowards(ik.weight, targetWeight, settings.IKWeightSpeed * dt);
            else
                ik.weight = targetWeight;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyLegIK(
            Transform legConstraintPosition,
            Vector3 legConstraintForward,
            Transform legIKTarget,
            ref FeetIKComponent.FeetComponent feetComponent,
            ICharacterControllerSettings settings,
            float dt)
        {
            float pullDist = settings.FeetIKHipsPullMaxDistance;
            Vector3 origin = legConstraintPosition.position;
            Vector3 rayOrigin = origin + (Vector3.up * pullDist);
            Vector3 rayDirection = Vector3.down;
            float rayDistance = pullDist * 2;

            Debug.DrawRay(rayOrigin, rayDirection * rayDistance, Color.red);

            if (Physics.SphereCast(rayOrigin, settings.FeetIKSphereSize, rayDirection, out RaycastHit hitInfo, rayDistance, PhysicsLayers.CHARACTER_ONLY_MASK))
            {
                legIKTarget.position = Vector3.MoveTowards(legIKTarget.position, hitInfo.point, settings.IKPositionSpeed * dt);
                var rotationCorrection = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
                legConstraintForward.y = 0;
                legIKTarget.rotation = rotationCorrection * Quaternion.LookRotation(legConstraintForward, Vector3.up);
                feetComponent.isGrounded = true;
                feetComponent.distance = FEET_HEIGHT_CORRECTION + hitInfo.distance - pullDist - legConstraintPosition.localPosition.y;
                return;
            }

            legIKTarget.localPosition = feetComponent.TargetInitialPosition;
            legIKTarget.localRotation = feetComponent.TargetInitialRotation;
            feetComponent.isGrounded = false;
            feetComponent.distance = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyHipsHeightCorrection(
            float dt,
            ref FeetIKComponent feetIKComponent,
            AvatarBase avatarBase,
            ICharacterControllerSettings settings)
        {
            // Get the most stretched feet distance
            float highestDist = Mathf.Max(feetIKComponent.Right.distance, feetIKComponent.Left.distance);

            // Calculate the target weight based ond both feet weight
            float weight = (avatarBase.RightLegIK.weight + avatarBase.LeftLegIK.weight) / 2;

            MultiPositionConstraintData data = avatarBase.HipsConstraint.data;
            Vector3 offset = data.offset;
            offset.y = Mathf.MoveTowards(offset.y, -highestDist, settings.IKPositionSpeed * dt);
            data.offset = offset;
            avatarBase.HipsConstraint.data = data;
            avatarBase.HipsConstraint.weight = weight;
        }
    }
}
