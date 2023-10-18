using UnityEngine;

namespace DCL.CharacterMotion.Settings
{
    /// <summary>
    ///     Add this reference type as a component so we can change values on fly
    /// </summary>
    public interface ICharacterControllerSettings
    {
        float WalkSpeed { get; }
        float JogSpeed { get; }
        float RunSpeed { get; }
        float AirAcceleration { get; }
        float MaxAirAcceleration { get; }
        float Gravity { get; }
        float JogJumpHeight { get; }
        float RunJumpHeight { get; }
        float CharacterControllerRadius { get; }
        float GroundDrag { get; }
        float AirDrag { get; }
        float MinImpulse { get; }
        float JumpVelocityDrag { get; }
        float Acceleration { get; }
        float MaxAcceleration { get; }
        float AccelerationTime { get; }
        float StopTimeSec { get; }
        float LongJumpTime { get; }
        float LongJumpGravityScale { get; }
        float JumpGravityFactor { get; }
        float JumpGraceTime { get; }
        float JumpHeightStun { get; }
        float LongFallStunTime { get; }
        float NoSlipDistance { get; }
        float EdgeSlipSpeed { get; }
        float RotationSpeed { get; }
        float MovAnimBlendSpeed { get; }
        float JumpPadForce { get; }
        float AnimationSpeed { get; }
        public AnimationCurve AccelerationCurve { get; }
    }
}
