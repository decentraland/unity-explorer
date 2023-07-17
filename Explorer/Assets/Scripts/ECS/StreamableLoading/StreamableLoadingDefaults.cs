namespace ECS.StreamableLoading
{
    public static class StreamableLoadingDefaults
    {
        public const int ATTEMPTS_COUNT = 3;
        public const int TIMEOUT = 60;
    }

    public enum DeferredLoadingState : byte
    {
        /// <summary>
        ///     The state was not evaluated yet
        /// </summary>
        NotEvaluated,

        /// <summary>
        ///     The state was evaluated as Allowed
        /// </summary>
        Allowed,

        /// <summary>
        ///     The state was evaluated as Forbidden
        /// </summary>
        Forbidden,
    }
}
