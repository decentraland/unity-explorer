using UnityEngine;

namespace DCL.CharacterMotion.Settings
{
    /// <summary>
    ///     Add this reference type as a component so we can change values on fly
    /// </summary>
    public interface ICharacterControllerSettings
    {
        float WalkSpeed { get; }
        float RunSpeed { get; }
        float GroundAcceleration { get; }
        float AirAcceleration { get; }
        float RotationAngularSpeed { get; }
        float Gravity { get; }
        Vector2 JumpHeight { get; }
        float AirDrag { get; }
        float HoldJumpTime { get; }
    }
}
