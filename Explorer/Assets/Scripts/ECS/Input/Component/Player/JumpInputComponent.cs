using ECS.Input.Component;

namespace ECS.CharacterMotion.Components
{
    public struct JumpInputComponent : InputComponent
    {
        public PhysicalJumpButtonArguments PhysicalButtonArguments;

        public float CurrentHoldTime;
        public bool IsChargingJump;

    }

}
