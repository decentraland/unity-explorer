using ECS.Input.Component;

namespace ECS.CharacterMotion.Components
{
    public struct JumpInputComponent : PhysicalKeyComponent
    {
        /// <summary>
        ///     Normalized value [0;1] indicating how long we pressed the jump button,
        ///     0 means no jump
        /// </summary>
        public float Power;

        public PhysicalButtonArguments PhysicalButtonArguments { get; set; }
    }

}
