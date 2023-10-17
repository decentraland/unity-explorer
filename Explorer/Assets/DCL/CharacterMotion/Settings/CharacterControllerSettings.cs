using UnityEngine;

namespace DCL.CharacterMotion.Settings
{
    [CreateAssetMenu(menuName = "Create Character Controller Settings", fileName = "CharacterControllerSettings", order = 0)]
    public class CharacterControllerSettings : ScriptableObject, ICharacterControllerSettings
    {
        [field: SerializeField, Header("General config")] public float WalkSpeed { get; private set; } = 1;
        [field: SerializeField] public float JogSpeed { get; private set; } = 3;
        [field: SerializeField] public float RunSpeed { get; private set; } = 5;
        [field: SerializeField] public float AirAcceleration { get; private set; } = 5;
        [field: SerializeField] public float Gravity { get; private set; } = -9.8f;
        [field: SerializeField] public float JogJumpHeight { get; private set; } = 3f;
        [field: SerializeField] public float RunJumpHeight { get; private set; } = 5f;
        [field: SerializeField] public float CharacterControllerRadius { get; private set; } = 0.5f;

        [field: SerializeField, Header("Impulse Specifics")] public float GroundDrag { get; private set; } = 0.5f;
        [field: SerializeField] public float AirDrag { get; private set; } = 0.25f;
        [field: SerializeField] public float MinImpulse { get; private set; } = 1f;

        [field: SerializeField, Header("Velocity Drag")] public float JumpVelocityDrag { get; private set; } = 3f;

        [field: SerializeField, Header("Smooth acceleration")] public AnimationCurve accelerationCurve;
        [field: SerializeField] public float Acceleration { get; private set; } = 5;
        [field: SerializeField] public float MaxAcceleration { get; private set; } = 25f;
        [field: SerializeField] public float AccelerationTime { get; private set; } = 0.5f;

        [field: SerializeField, Header("De-acceleration dampening")] public float StopTimeSec { get; private set; } = 0.12f;

        [field: SerializeField, Header("Long Jump")] public float LongJumpTime { get; private set; } = 0.5f;
        [field: SerializeField] public float LongJumpGravityScale { get; private set; } = 0.5f;

        [field: SerializeField, Header("Faster Jumps")] public float JumpGravityFactor { get; private set; } = 2;

        [field: SerializeField, Header("Coyote timer")] public float JumpGraceTime { get; private set; } = 0.15f;

        [field: SerializeField, Header("Hard fall stun")] public float JumpHeightStun { get; private set; } = 10f;
        [field: SerializeField] public float LongFallStunTime { get; private set; } = 0.75f;

        [field: SerializeField, Header("Edge slip")] public float NoSlipDistance { get; private set; } = 0.1f;
        [field: SerializeField] public float EdgeSlipSpeed { get; private set; } = 1.2f;

        [field: SerializeField, Header("Animation")] public float RotationSpeed { get; private set; } = 360f;
        [field: SerializeField] public float MovAnimBlendSpeed { get; private set; } = 3f;

        [field: SerializeField, Header("Cheat/Debug/Misc")] public float JumpPadForce { get; private set; } = 50f;
        [field: SerializeField] public float AnimationSpeed { get; private set; } = 1;
    }
}
