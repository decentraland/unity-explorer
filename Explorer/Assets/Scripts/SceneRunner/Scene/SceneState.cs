namespace SceneRunner.Scene
{
    public enum SceneState : byte
    {
        /// <summary>
        ///     Scene is created but not started yet
        /// </summary>
        NotStarted,

        /// <summary>
        ///     Scene is running
        /// </summary>
        Running,

        /// <summary>
        ///     Scene communication has broken
        /// </summary>
        EngineError,

        /// <summary>
        ///     ECS World Execution error
        /// </summary>
        EcsError,

        /// <summary>
        ///     Error in the JS code
        /// </summary>
        JavaScriptError,

        /// <summary>
        ///     Scene is signaled for disposing
        /// </summary>
        Disposing,

        /// <summary>
        ///     The scene is disposed
        /// </summary>
        Disposed,
    }
}
