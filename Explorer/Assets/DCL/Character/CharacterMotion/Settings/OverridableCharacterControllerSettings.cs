using DCL.SDKComponents.AvatarLocomotion.Components;
using DCL.SDKComponents.AvatarLocomotion.Systems;
using UnityEngine;

namespace DCL.CharacterMotion.Settings
{
    public class OverridableCharacterControllerSettings : ICharacterControllerSettings
    {
        private readonly ICharacterControllerSettings impl;

        private AvatarLocomotionOverrides currentOverrides;

        public OverridableCharacterControllerSettings(ICharacterControllerSettings impl)
        {
            this.impl = impl;
        }

        public void ApplyOverrides(in AvatarLocomotionOverrides locomotionOverrides) =>
            currentOverrides = locomotionOverrides;

        private float GetOverrideOrValue(AvatarLocomotionOverrides.OverrideID id, float value)
        {
            AvatarLocomotionOverridesHelper.TryOverride(in currentOverrides, id, ref value);
            return value;
        }

        public float WalkSpeed
        {
            get => GetOverrideOrValue(AvatarLocomotionOverrides.OverrideID.WALK_SPEED, impl.WalkSpeed);
            set => impl.WalkSpeed = value;
        }

        public float JogSpeed
        {
            get => GetOverrideOrValue(AvatarLocomotionOverrides.OverrideID.JOG_SPEED, impl.JogSpeed);
            set => impl.JogSpeed = value;
        }

        public float RunSpeed
        {
            get => GetOverrideOrValue(AvatarLocomotionOverrides.OverrideID.RUN_SPEED, impl.RunSpeed);
            set => impl.RunSpeed = value;
        }

        public float AirAcceleration
        {
            get => impl.AirAcceleration;
            set => impl.AirAcceleration = value;
        }

        public float MaxAirAcceleration
        {
            get => impl.MaxAirAcceleration;
            set => impl.MaxAirAcceleration = value;
        }

        public float Gravity
        {
            get => impl.Gravity;
            set => impl.Gravity = value;
        }

        public float JogJumpHeight
        {
            get => GetOverrideOrValue(AvatarLocomotionOverrides.OverrideID.JUMP_HEIGHT, impl.JogJumpHeight);
            set => impl.JogJumpHeight = value;
        }

        public float RunJumpHeight
        {
            get => GetOverrideOrValue(AvatarLocomotionOverrides.OverrideID.RUN_JUMP_HEIGHT, impl.RunJumpHeight);
            set => impl.RunJumpHeight = value;
        }

        public float CharacterControllerRadius => impl.CharacterControllerRadius;

        public float GroundDrag => impl.GroundDrag;

        public float AirDrag
        {
            get => impl.AirDrag;
            set => impl.AirDrag = value;
        }

        public float MinImpulse => impl.MinImpulse;

        public float JumpVelocityDrag => impl.JumpVelocityDrag;

        public float Acceleration => impl.Acceleration;

        public float MaxAcceleration => impl.MaxAcceleration;

        public float AccelerationTime => impl.AccelerationTime;

        public float StopTimeSec
        {
            get => impl.StopTimeSec;
            set => impl.StopTimeSec = value;
        }

        public float LongJumpTime
        {
            get => impl.LongJumpTime;
            set => impl.LongJumpTime = value;
        }

        public float LongJumpGravityScale
        {
            get => impl.LongJumpGravityScale;
            set => impl.LongJumpGravityScale = value;
        }

        public float JumpGravityFactor => impl.JumpGravityFactor;

        public float JumpGraceTime => impl.JumpGraceTime;

        public float JumpHeightStun => impl.JumpHeightStun;

        public float LongFallStunTime => GetOverrideOrValue(AvatarLocomotionOverrides.OverrideID.HARD_LANDING_COOLDOWN, impl.LongFallStunTime);

        public float NoSlipDistance => impl.NoSlipDistance;

        public float EdgeSlipSpeed => impl.EdgeSlipSpeed;

        public float EdgeSlipSafeDistance => impl.EdgeSlipSafeDistance;

        public float RotationSpeed => impl.RotationSpeed;

        public float MoveAnimBlendMaxWalkSpeed => impl.MoveAnimBlendMaxWalkSpeed;

        public float MoveAnimBlendMaxJogSpeed => impl.MoveAnimBlendMaxJogSpeed;

        public float MoveAnimBlendMaxRunSpeed => impl.MoveAnimBlendMaxRunSpeed;

        public float MoveAnimBlendSpeed => impl.MoveAnimBlendSpeed;

        public float JumpPadForce => impl.JumpPadForce;

        public float AnimationSpeed => impl.AnimationSpeed;

        public AnimationCurve AccelerationCurve => impl.AccelerationCurve;

        public float CameraFOVWhileRunning
        {
            get => impl.CameraFOVWhileRunning;
            set => impl.CameraFOVWhileRunning = value;
        }

        public float FOVIncreaseSpeed
        {
            get => impl.FOVIncreaseSpeed;
            set => impl.FOVIncreaseSpeed = value;
        }

        public float FOVDecreaseSpeed
        {
            get => impl.FOVDecreaseSpeed;
            set => impl.FOVDecreaseSpeed = value;
        }

        public float FeetIKHipsPullMaxDistance
        {
            get => impl.FeetIKHipsPullMaxDistance;
            set => impl.FeetIKHipsPullMaxDistance = value;
        }

        public float FeetIKSphereSize
        {
            get => impl.FeetIKSphereSize;
            set => impl.FeetIKSphereSize = value;
        }

        public float FeetHeightCorrection
        {
            get => impl.FeetHeightCorrection;
            set => impl.FeetHeightCorrection = value;
        }

        public float FeetHeightDisableIkDistance
        {
            get => impl.FeetHeightDisableIkDistance;
            set => impl.FeetHeightDisableIkDistance = value;
        }

        public float HipsHeightCorrection
        {
            get => impl.HipsHeightCorrection;
            set => impl.HipsHeightCorrection = value;
        }

        public float IKWeightSpeed
        {
            get => impl.IKWeightSpeed;
            set => impl.IKWeightSpeed = value;
        }

        public float IKPositionSpeed
        {
            get => impl.IKPositionSpeed;
            set => impl.IKPositionSpeed = value;
        }

        public Vector2 FeetIKVerticalAngleLimits
        {
            get => impl.FeetIKVerticalAngleLimits;
            set => impl.FeetIKVerticalAngleLimits = value;
        }

        public Vector2 FeetIKTwistAngleLimits
        {
            get => impl.FeetIKTwistAngleLimits;
            set => impl.FeetIKTwistAngleLimits = value;
        }

        public Vector3 FeetIKLeftOffset
        {
            get => impl.FeetIKLeftOffset;
            set => impl.FeetIKLeftOffset = value;
        }

        public Vector3 FeetIKRightOffset
        {
            get => impl.FeetIKRightOffset;
            set => impl.FeetIKRightOffset = value;
        }

        public Vector3 FeetIKLeftRotationOffset
        {
            get => impl.FeetIKLeftRotationOffset;
            set => impl.FeetIKLeftRotationOffset = value;
        }

        public Vector3 FeetIKRightRotationOffset
        {
            get => impl.FeetIKRightRotationOffset;
            set => impl.FeetIKRightRotationOffset = value;
        }

        public float HandsIKWallHitDistance
        {
            get => impl.HandsIKWallHitDistance;
            set => impl.HandsIKWallHitDistance = value;
        }

        public float HandsIKWeightSpeed
        {
            get => impl.HandsIKWeightSpeed;
            set => impl.HandsIKWeightSpeed = value;
        }

        public Vector3 HandsIKElbowOffset
        {
            get => impl.HandsIKElbowOffset;
            set => impl.HandsIKElbowOffset = value;
        }

        public float AnimationFallSpeed => impl.AnimationFallSpeed;

        public float AnimationLongFallSpeed => impl.AnimationLongFallSpeed;

        public float PlatformRaycastLength => impl.PlatformRaycastLength;

        public float DownwardsSlopeJogRaycastDistance => impl.DownwardsSlopeJogRaycastDistance;

        public float DownwardsSlopeRunRaycastDistance => impl.DownwardsSlopeRunRaycastDistance;

        public bool HeadIKIsEnabled
        {
            get => impl.HeadIKIsEnabled;
            set => impl.HeadIKIsEnabled = value;
        }

        public float HeadIKVerticalAngleLimit
        {
            get => impl.HeadIKVerticalAngleLimit;
            set => impl.HeadIKVerticalAngleLimit = value;
        }

        public float HeadIKHorizontalAngleLimit
        {
            get => impl.HeadIKHorizontalAngleLimit;
            set => impl.HeadIKHorizontalAngleLimit = value;
        }

        public float HeadIKHorizontalAngleReset
        {
            get => impl.HeadIKHorizontalAngleReset;
            set => impl.HeadIKHorizontalAngleReset = value;
        }

        public float HeadIKRotationSpeed
        {
            get => impl.HeadIKRotationSpeed;
            set => impl.HeadIKRotationSpeed = value;
        }

        public AnimationCurve SlopeVelocityModifier => impl.SlopeVelocityModifier;

        public float SlideAnimationBlendSpeed => impl.SlideAnimationBlendSpeed;

        public float MinSlopeAngle => impl.MinSlopeAngle;

        public float MaxSlopeAngle => impl.MaxSlopeAngle;

        public bool EnableCharacterRotationBySlope => impl.EnableCharacterRotationBySlope;

        public float SlopeCharacterRotationDelay => impl.SlopeCharacterRotationDelay;

        public float WallSlideDetectionDistance => impl.WallSlideDetectionDistance;

        public float WallSlideMaxMoveSpeedMultiplier => impl.WallSlideMaxMoveSpeedMultiplier;

        public float StepOffset
        {
            get => impl.StepOffset;
            set => impl.StepOffset = value;
        }

        public float HeadIKWeightChangeSpeed => impl.HeadIKWeightChangeSpeed;
    }
}
