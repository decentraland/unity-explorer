namespace ECS.Input.Component
{
    public struct JumpInputComponent : InputComponent
    {
        /// <summary>
        ///     Helper struct to correctly fire the jump on a fixed update
        /// </summary>
        public PhysicalJumpButtonArguments PhysicalButtonArguments;

        /// <summary>
        ///     Time that the jump button has been held and charging
        /// </summary>
        public float CurrentHoldTime;

        public bool IsChargingJump;
    }
}
