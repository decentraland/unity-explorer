namespace ECS.CharacterMotion.Components
{
    public struct JumpInputComponent
    {
        /// <summary>
        ///     Normalized value [0;1] indicating how long we pressed the jump button,
        ///     0 means no jump
        /// </summary>
        public float Power;
    }

    public struct DeferredInput<T>
    {
        /// <summary>
        ///     Momentum Input access
        /// </summary>
        public T Update;

        /// <summary>
        ///     Input saved till Fixed Update
        /// </summary>
        public T FixedUpdate;
    }
}
