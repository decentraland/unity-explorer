using UnityEngine;

namespace DCL.CharacterMotion.Settings
{
    [CreateAssetMenu(menuName = "Create Character Controller Settings", fileName = "CharacterControllerSettings", order = 0)]
    public class CharacterControllerSettings : ScriptableObject, ICharacterControllerSettings
    {
        [field: SerializeField] [field: Header("General config")] public float WalkSpeed { get; set; } = 1;
        [field: SerializeField] public float JogSpeed { get; set; } = 3;
        [field: SerializeField] public float RunSpeed { get; set; } = 5;
        [field: SerializeField] public float Gravity { get; set; } = -9.8f;
        [field: SerializeField] public float JogJumpHeight { get; set; } = 3f;
        [field: SerializeField] public float RunJumpHeight { get; set; } = 5f;
        [field: SerializeField] public float CharacterControllerRadius { get; private set; } = 0.5f;

        [field: SerializeField, Header("Impulse Specifics")] public float GroundDrag { get; private set; } = 0.5f;
        [field: SerializeField] public float AirDrag { get; set; } = 0.25f;
        [field: SerializeField] public float MinImpulse { get; private set; } = 1f;

        [field: SerializeField, Header("Velocity Drag")] public float JumpVelocityDrag { get; private set; } = 3f;

        [field: SerializeField, Header("Smooth acceleration")] public AnimationCurve AccelerationCurve { get; private set; }
        [field: SerializeField] public float Acceleration { get; private set; } = 5;
        [field: SerializeField] public float MaxAcceleration { get; private set; } = 25f;
        [field: SerializeField] public float AccelerationTime { get; private set; } = 0.5f;
        [field: SerializeField] public float AirAcceleration { get; set; } = 7;
        [field: SerializeField] public float MaxAirAcceleration { get; set; } = 10;

        [field: SerializeField] [field: Header("De-acceleration dampening")] public float StopTimeSec { get; set; } = 0.12f;

        [field: SerializeField] [field: Header("Long Jump")] public float LongJumpTime { get; set; } = 0.5f;
        [field: SerializeField] public float LongJumpGravityScale { get; set; } = 0.5f;

        [field: SerializeField, Header("Faster Jumps")] public float JumpGravityFactor { get; private set; } = 2;

        [field: SerializeField, Header("Coyote timer")] public float JumpGraceTime { get; private set; } = 0.15f;

        [field: SerializeField, Header("Hard fall stun")] public float JumpHeightStun { get; private set; } = 10f;
        [field: SerializeField] public float LongFallStunTime { get; private set; } = 0.75f;

        [field: SerializeField] [field: Header("Edges and Slopes")] public float NoSlipDistance { get; private set; } = 0.1f;
        [field: SerializeField] public float EdgeSlipSpeed { get; private set; } = 1.2f;
        [field: SerializeField] public float EdgeSlipSafeDistance { get; private set; } = 0.4f;
        [field: SerializeField] public float DownwardsSlopeJogRaycastDistance { get; private set; } = 0.45f;
        [field: SerializeField] public float DownwardsSlopeRunRaycastDistance { get; private set; } = 0.55f;
        [field: SerializeField] public float SlideAnimationBlendSpeed { get; private set; } = 7;
        [field: SerializeField] public float MinSlopeAngle { get; private set; } = 50;
        [field: SerializeField] public float MaxSlopeAngle { get; private set; } = 80;
        [field: SerializeField] public float SlopeCharacterRotationDelay { get; private set; } = 0.25f;
        [field: SerializeField] public AnimationCurve SlopeVelocityModifier { get; private set; }
        [field: SerializeField] public float WallSlideDetectionDistance { get; private set; } = 0.5f;
        [field: SerializeField] public float WallSlideMaxMoveSpeedMultiplier { get; private set; }
        [field: SerializeField] public float StepOffset { get; set; } = 0.35f;
        [field: SerializeField, Header("Animation")] public float RotationSpeed { get; private set; } = 360f;
        [field: SerializeField] public float MovAnimBlendSpeed { get; private set; } = 3f;
        [field: SerializeField] public float AnimationFallSpeed { get; private set; } = -5f;
        [field: SerializeField] public float AnimationLongFallSpeed { get; private set; } = -12f;

        [field: SerializeField] [field: Header("Platforms")] public float PlatformRaycastLength { get; private set; } = 0.3f;

        [field: SerializeField] [field: Header("Camera")] public float CameraFOVWhileRunning { get; set; } = 15;
        [field: SerializeField] public float FOVIncreaseSpeed { get; set; } = 15;
        [field: SerializeField] public float FOVDecreaseSpeed { get; set; } = 15;

        [field: SerializeField] [field: Header("Feet IK")] public float FeetIKHipsPullMaxDistance { get; set; } = 0.5f;
        [field: SerializeField] public float FeetIKSphereSize { get; set; } = 0.15f;
        [field: SerializeField] public float IKWeightSpeed { get; set; } = 2f;
        [field: SerializeField] public float IKPositionSpeed { get; set; } = 1f;
        [field: SerializeField] public Vector2 FeetIKVerticalAngleLimits { get; set; } = new (-50, 20);
        [field: SerializeField] public Vector2 FeetIKTwistAngleLimits { get; set; } = new (-30, 15);

        [field: SerializeField] public float FeetHeightCorrection { get; set; } = 0.08f;
        [field: SerializeField] public float FeetHeightDisableIkDistance { get; set; } = 0.1f;
        [field: SerializeField] public float HipsHeightCorrection { get; set; } = 0.05f;
        [field: SerializeField] [field: Header("Hands IK")] public float HandsIKWallHitDistance { get; set; } = 0.5f;
        [field: SerializeField] public float HandsIKWeightSpeed { get; set; } = 0.5f;
        [field: SerializeField] public Vector3 HandsIKElbowOffset { get; set; } = Vector3.zero;
        [field: SerializeField] [field: Header("Head IK")] public float HeadIKVerticalAngleLimit { get; set; } = 75;
        [field: SerializeField] public float HeadIKHorizontalAngleLimit { get; set; } = 120;
        [field: SerializeField] public float HeadIKRotationSpeed { get; set; } = 45;

        [field: SerializeField, Header("Cheat/Debug/Misc")] public float JumpPadForce { get; private set; } = 50f;
        [field: SerializeField] public float AnimationSpeed { get; private set; } = 1;
    }
}
