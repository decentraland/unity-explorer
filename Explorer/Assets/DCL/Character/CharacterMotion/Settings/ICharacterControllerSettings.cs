using UnityEngine;

namespace DCL.CharacterMotion.Settings
{
    /// <summary>
    ///     Add this reference type as a component so we can change values on fly
    /// </summary>
    public interface ICharacterControllerSettings
    {
        float WalkSpeed { get; set; }
        float JogSpeed { get; set; }
        float RunSpeed { get; set; }
        float AirAcceleration { get; set; }
        float MaxAirAcceleration { get; set; }
        float Gravity { get; set; }
        float JogJumpHeight { get; set; }
        float RunJumpHeight { get; set; }
        float CharacterControllerRadius { get; }
        float GroundDrag { get; }
        float AirDrag { get; set; }
        float MinImpulse { get; }
        float JumpVelocityDrag { get; }
        float Acceleration { get; }
        float MaxAcceleration { get; }
        float AccelerationTime { get; }
        float StopTimeSec { get; set; }
        float LongJumpTime { get; set; }
        float LongJumpGravityScale { get; set; }
        float JumpGravityFactor { get; }
        float JumpGraceTime { get; }
        float JumpHeightStun { get; }
        float LongFallStunTime { get; }
        float NoSlipDistance { get; }
        float EdgeSlipSpeed { get; }
        float EdgeSlipSafeDistance { get; }
        float RotationSpeed { get; }
        float MovAnimBlendSpeed { get; }
        float JumpPadForce { get; }
        float AnimationSpeed { get; }
        public AnimationCurve AccelerationCurve { get; }
        float CameraFOVWhileRunning { get; set; }
        float FOVIncreaseSpeed { get; set; }
        float FOVDecreaseSpeed { get; set; }
        float FeetIKHipsPullMaxDistance { get; set; }
        float FeetIKSphereSize { get; set; }
        float FeetHeightCorrection { get; set; }
        float FeetHeightDisableIkDistance { get; set; }
        float HipsHeightCorrection { get; set; }
        float IKWeightSpeed { get; set; }
        float IKPositionSpeed { get; set; }
        Vector2 FeetIKVerticalAngleLimits { get; set; }
        Vector2 FeetIKTwistAngleLimits { get; set; }
        float HandsIKWallHitDistance { get; set; }
        float HandsIKWeightSpeed { get; set; }
        Vector3 HandsIKElbowOffset { get; set; }
        float AnimationFallSpeed { get; }
        float AnimationLongFallSpeed { get; }
        float PlatformRaycastLength { get; }
        float DownwardsSlopeJogRaycastDistance { get; }
        float DownwardsSlopeRunRaycastDistance { get; }
        float HeadIKVerticalAngleLimit { get; set; }
        float HeadIKHorizontalAngleLimit { get; set; }
        float HeadIKRotationSpeed { get; set; }
        AnimationCurve SlopeVelocityModifier { get; }
        float SlideAnimationBlendSpeed { get; }
        float MinSlopeAngle { get; }
        float MaxSlopeAngle { get; }
        float SlopeCharacterRotationDelay { get; }
        float WallSlideDetectionDistance { get; }
        float WallSlideMaxMoveSpeedMultiplier { get; }
        float StepOffset { get; set; }
    }
}
