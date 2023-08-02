using ECS.Input.Component;

namespace ECS.CharacterMotion.Components
{
    public struct JumpInputComponent : IInputComponent
    {
        public PhysicalJumpButtonArguments PhysicalButtonArguments;

        public float CurrentHoldTime;
        public bool IsChargingJump;
    }
}
