using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Physics;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using ECS.Abstract;
using System.Drawing;
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
        private bool feetIkIsEnabled = true;
        private readonly ElementBinding<float> ikWeightChangeSpeed;
        private readonly ElementBinding<float> ikPositionChangeSpeed;
        private readonly ElementBinding<float> ikDistance;
        private readonly ElementBinding<float> spherecastWidth;
        private readonly ElementBinding<float> twistLimitX;
        private readonly ElementBinding<float> twistLimitY;
        private SingleInstanceEntity settingsEntity;

        private FeetIKSystem(World world, IDebugContainerBuilder debugBuilder) : base(world)
        {
            debugBuilder.AddWidget("Locomotion: Feet IK")
                        .AddToggleField("Enabled", evt => { feetIkIsEnabled = evt.newValue; }, true)
                        .AddFloatField("IK Change Speed", ikWeightChangeSpeed = new ElementBinding<float>(0))
                        .AddFloatField("IK Position Speed", ikPositionChangeSpeed = new ElementBinding<float>(0))
                        .AddFloatField("IK Distance", ikDistance = new ElementBinding<float>(0))
                        .AddFloatField("Spherecast Width", spherecastWidth = new ElementBinding<float>(0))
                        .AddFloatField("Twist Limit X", twistLimitX = new ElementBinding<float>(0))
                        .AddFloatField("Twist Limit Y", twistLimitY = new ElementBinding<float>(0));
        }

        public override void Initialize()
        {
            settingsEntity = World.CacheCharacterSettings();

            ICharacterControllerSettings settings = settingsEntity.GetCharacterSettings(World);

            Vector2 twistLimits = settings.FeetIKTwistAngleLimits;
            ikWeightChangeSpeed.Value = settings.IKWeightSpeed;
            ikPositionChangeSpeed.Value = settings.IKPositionSpeed;
            ikDistance.Value = settings.FeetIKHipsPullMaxDistance;
            spherecastWidth.Value = settings.FeetIKSphereSize;
            twistLimitX.Value = twistLimits.x;
            twistLimitY.Value = twistLimits.y;
        }

        protected override void Update(float t)
        {
            ICharacterControllerSettings settings = settingsEntity.GetCharacterSettings(World);

            Vector2 twistLimits = settings.FeetIKTwistAngleLimits;
            settings.IKWeightSpeed = ikWeightChangeSpeed.Value;
            settings.IKPositionSpeed = ikPositionChangeSpeed.Value;
            settings.FeetIKHipsPullMaxDistance = ikDistance.Value;
            settings.FeetIKSphereSize = spherecastWidth.Value;
            twistLimits.x = twistLimitX.Value;
            twistLimits.y = twistLimitY.Value;
            settings.FeetIKTwistAngleLimits = twistLimits;

            UpdateIKQuery(World, t);
        }

        [Query]
        private void UpdateIK(
            [Data] float dt,
            ref FeetIKComponent feetIKComponent,
            ref AvatarBase avatarBase,
            in CharacterRigidTransform rigidTransform,
            in ICharacterControllerSettings settings,
            in StunComponent stunComponent
        )
        {
            // Debug stuff and enable/disable mechanic
            UpdateToggleStatus(ref feetIKComponent, avatarBase);
            if (feetIKComponent.IsDisabled) return;
            if (!feetIKComponent.Initialized)
                InitializeFeetComponent(ref feetIKComponent, avatarBase);

            Transform rightLegConstraint = avatarBase.RightLegConstraint;
            Transform leftLegConstraint = avatarBase.LeftLegConstraint;

            // Enable flags: when disabled we lerp the IK weight towards 0
            bool isEnabled = rigidTransform.IsGrounded
                             && (!rigidTransform.IsOnASteepSlope || rigidTransform.IsStuck)
                             && !stunComponent.IsStunned;

            // && !emotes?

            // First: Raycast down from right/left constraints and update IK targets
            ApplyLegIK(rightLegConstraint, rightLegConstraint.forward, avatarBase.RightLegIKTarget, ref feetIKComponent.Right, settings, dt, settings.FeetIKVerticalAngleLimits, settings.FeetIKTwistAngleLimits);
            ApplyLegIK(leftLegConstraint, leftLegConstraint.forward, avatarBase.LeftLegIKTarget, ref feetIKComponent.Left, settings, dt, settings.FeetIKVerticalAngleLimits, new Vector2(settings.FeetIKTwistAngleLimits.y, settings.FeetIKTwistAngleLimits.x));

            // Second: Calculate IK feet weight based on the constrained local-Y
            ApplyIKWeight(avatarBase.RightLegIK, rightLegConstraint.localPosition, ref feetIKComponent.Right, isEnabled, settings, dt);
            ApplyIKWeight(avatarBase.LeftLegIK, leftLegConstraint.localPosition, ref feetIKComponent.Left, isEnabled, settings, dt);

            // Third: Calculate the "pull" distance and update the hips, in order to extend the feet downwards so the character can have one feet a step higher depending on the ground complexity
            ApplyHipsHeightCorrection(dt, ref feetIKComponent, avatarBase, settings);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateToggleStatus(ref FeetIKComponent feetIKComponent, AvatarBase avatarBase)
        {
            feetIKComponent.IsDisabled = !feetIkIsEnabled;
            avatarBase.FeetIKRig.weight = feetIKComponent.IsDisabled ? 0 : 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void InitializeFeetComponent(ref FeetIKComponent feetIKComponent, AvatarBase avatarBase)
        {
            feetIKComponent.Initialized = true;
            feetIKComponent.Left.TargetInitialPosition = avatarBase.LeftLegIKTarget.localPosition;
            feetIKComponent.Left.TargetInitialRotation = avatarBase.LeftLegIKTarget.localRotation;
            feetIKComponent.Right.TargetInitialPosition = avatarBase.RightLegIKTarget.localPosition;
            feetIKComponent.Right.TargetInitialRotation = avatarBase.RightLegIKTarget.localRotation;
            avatarBase.FeetIKRig.weight = 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyIKWeight(
            TwoBoneIKConstraint ik,
            Vector3 rightLegPosition,
            ref FeetIKComponent.FeetComponent feetComponent,
            bool isEnabled,
            ICharacterControllerSettings settings,
            float dt)
        {
            // The feet bone never touches the ground at Idle position, this is the average height of the idle animation and we use it to decide whether the bone is on the ground or not
            // FEET_HEIGHT_CORRECTION is the distance between the ground and the foot bone when "standing still"
            // FEET_HEIGHT_DISABLE_IK is the height when we dont want the foot to have IK enabled
            // Games configure this weight trough Animation Curves on each animation, but since we cant do that here, we just do magic math
            float ikWeightBasedOnAnimation = !feetComponent.IsGrounded ? 0 : 1f - ((rightLegPosition.y - settings.FeetHeightCorrection) / settings.FeetHeightDisableIkDistance);
            ikWeightBasedOnAnimation = feetComponent.IsInsideMesh ? 1 : ikWeightBasedOnAnimation;
            float targetWeight = Mathf.RoundToInt(ikWeightBasedOnAnimation) * (isEnabled ? 1 : 0);

            // We apply ik weight speed only if its increasing, decreasing it is instant, this avoids being partially snapped into the ground when suddenly jumping
            if (ik.weight < targetWeight)
                ik.weight = Mathf.MoveTowards(ik.weight, targetWeight, settings.IKWeightSpeed * dt);
            else
                ik.weight = targetWeight;
        }

        // We do a SphereCast downwards from the current feet position (after animated) and then apply the position and rotation to the IK Target
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyLegIK(
            Transform legConstraintPosition,
            Vector3 legConstraintForward,
            Transform legIKTarget,
            ref FeetIKComponent.FeetComponent feetComponent,
            ICharacterControllerSettings settings,
            float dt,
            Vector2 verticalLimits, Vector2 twistLimits)
        {
            float pullDist = settings.FeetIKHipsPullMaxDistance;
            Vector3 origin = legConstraintPosition.position;

            // Since we are pulling the hips down in order to have our feet past our human limit, we add that height to the raycast origin to avoid casting from within meshes
            Vector3 rayOrigin = origin + (Vector3.up * pullDist);
            Vector3 rayDirection = Vector3.down;
            float rayDistance = pullDist * 2;

            if (Physics.SphereCast(rayOrigin, settings.FeetIKSphereSize, rayDirection, out RaycastHit hitInfo, rayDistance, PhysicsLayers.CHARACTER_ONLY_MASK))
            {
                // lerp towards the target position
                legIKTarget.position = Vector3.MoveTowards(legIKTarget.position, hitInfo.point, settings.IKPositionSpeed * dt);
                var rotationCorrection = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
                legConstraintForward.y = 0;
                Quaternion targetRotation = rotationCorrection * Quaternion.LookRotation(legConstraintForward, Vector3.up);

                // we first apply the rotation
                legIKTarget.rotation = targetRotation;

                // then we limit the angles using the local rotations
                ApplyRotationLimit(legIKTarget, verticalLimits, twistLimits);

                feetComponent.IsGrounded = true;
                feetComponent.IsInsideMesh = hitInfo.point.y > origin.y;
                feetComponent.Distance = settings.FeetHeightCorrection + hitInfo.distance - pullDist - legConstraintPosition.localPosition.y;
                return;
            }

            legIKTarget.localPosition = feetComponent.TargetInitialPosition;
            legIKTarget.localRotation = feetComponent.TargetInitialRotation;
            feetComponent.IsGrounded = false;
            feetComponent.Distance = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyRotationLimit(Transform legIKTarget, Vector2 verticalLimits, Vector2 twistLimits)
        {
            Vector3 eulerAngles = legIKTarget.localEulerAngles;

            // euler angles range from 0 to 360, so we use DeltaAngle to turn them into 180 to -180
            eulerAngles.x = Mathf.Clamp(-Mathf.DeltaAngle(eulerAngles.x, 0), verticalLimits.x, verticalLimits.y);

            // left and right legs have their limits inverted so we deal with that logic with Min and Max
            eulerAngles.z = Mathf.Clamp(-Mathf.DeltaAngle(eulerAngles.z, 0), Mathf.Min(twistLimits.x, twistLimits.y),
                Mathf.Max(twistLimits.x, twistLimits.y));

            // we dont have a horizontal limit since the feet horizontal angle is based on the animation

            legIKTarget.localEulerAngles = eulerAngles;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyHipsHeightCorrection(
            float dt,
            ref FeetIKComponent feetIKComponent,
            AvatarBase avatarBase,
            ICharacterControllerSettings settings)
        {
            // Get the most stretched feet distance
            float highestDist = Mathf.Max(feetIKComponent.Right.Distance, feetIKComponent.Left.Distance) - settings.HipsHeightCorrection;

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
