using ECS.Input.Component;

namespace DCL.CharacterMotion.Components
{
    public struct JumpInputComponent : IInputComponent
    {
        public PhysicalJumpButtonArguments PhysicalButtonArguments;

        public float CurrentHoldTime;
        public bool IsChargingJump;
    }
}
